using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

[DefaultExecutionOrder(1000)]
public class ScraperCleaner : MonoBehaviour
{
    [Header("ToolSwitcher 참조")]
    public ToolSwitcher toolSwitcher;

    [Header("레이캐스트 설정")]
    [Tooltip("전방 레이 거리 (벽 껌용)")]
    public float forwardRayDistance = 10f;
    [Tooltip("하향 레이 거리 (바닥 껌용)")]
    public float downwardRayDistance = 2f;
    [Tooltip("스피어캐스트 반지름 (0이면 일반 레이캐스트)")]
    public float spherecastRadius = 0.05f;
    public string gumTag = "Cleanable_Scraper";

    [Header("타겟 하이라이트")]
    public Color highlightColor = Color.cyan;

    [Header("잡기 설정")]
    public float triggerThreshold = 0.7f;
    public float gripThreshold = 0.7f;

    [Header("스크래핑 설정")]
    [Tooltip("제거에 필요한 스크래핑(한 방향 획) 횟수")]
    public int requiredScrapes = 10;
    [Tooltip("한 획(스트로크)으로 인정할 최소 이동 거리 (m). 이보다 짧은 잔진동은 카운트 안 됨")]
    public float minStrokeLength = 0.10f;
    [Tooltip("스크래핑으로 인정할 최소 속도 (m/s). 느린 드리프트/손떨림 무시")]
    public float minScrapeSpeed = 0.15f;
    [Tooltip("1회 인정 후 다음 인정까지 최소 간격 (초) — 연타/떨림 방지")]
    public float scrapeCooldown = 0.18f;
    [Tooltip("이 거리 이내에서만 스크래핑 인식 (잠금 해제와 별개)")]
    public float scrapeDistance = 1.5f;

    [Header("햅틱")]
    public float grabHapticAmplitude = 0.3f;
    public float grabHapticDuration = 0.1f;
    public float scrapeHapticAmplitude = 0.35f;
    public float scrapeHapticDuration = 0.08f;
    public float removeHapticAmplitude = 0.9f;
    public float removeHapticDuration = 0.25f;

    [Header("효과음")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip scrapeSound;

    [Header("디버그")]
    public bool showDebugInfo = true;

    private InputDevice rightDevice;
    private bool isHolding = false;
    private bool isScraping = false;
    private float scrapeStopTimer = 0f;
    [SerializeField] private float scrapeStopDelay = 0.25f;

    // 타겟
    private GameObject lockedTarget = null;
    private Renderer targetRenderer;
    private Color originalEmission;
    private bool wasEmissionEnabled;
    private bool isHighlighting = false;

    // 껌별 진행도 / 스케일 저장
    private Dictionary<GameObject, int> gumScrapeProgress = new();
    private Dictionary<GameObject, Vector3> gumOriginalScale = new();

    // 모션 감지 — 스트로크(한 방향 획) 방향을 latch 해두고, 그 반대로 꺾이면 1회.
    // 한 방향으로 쭉 미는 동작은 꺾임이 없어 카운트되지 않고, 짧은 잔진동은 minStrokeLength에서 걸러진다.
    // (ToiletBrushCleaner와 동일 방식)
    private Vector3 prevPosition;
    private Vector3 strokeDir = Vector3.zero;   // 현재 스트로크 기준 방향 (단위 벡터)
    private float strokeLen = 0f;               // 기준 방향으로 이동한 누적 거리
    private float cooldownTimer = 0f;

    void Start()
    {
        if (toolSwitcher == null)
            toolSwitcher = FindFirstObjectByType<ToolSwitcher>();

        var chars = InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(chars, devices);
        if (devices.Count > 0) rightDevice = devices[0];

        prevPosition = GetRayOrigin().position;
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (!rightDevice.isValid)
        {
            var chars = InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(chars, devices);
            if (devices.Count > 0) rightDevice = devices[0];
        }

        if (scrapeStopTimer > 0f)
        {
            scrapeStopTimer -= Time.deltaTime;
            if (scrapeStopTimer <= 0f) { isScraping = false; audioSource?.Stop(); }
        }

        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (!IsActiveTool())
        {
            FullReset();
            prevPosition = GetRayOrigin().position;
            return;
        }

        UpdateHoldState();

        // 잠금 중이 아닐 때만 레이로 탐색
        if (lockedTarget == null)
            ScanForTarget();

        // 잠금 상태면 하이라이트 항상 유지
        if (lockedTarget != null && !isHighlighting)
            ApplyHighlight();

        if (lockedTarget != null && isHolding && IsWithinScrapeRange())
            DetectScraping();
        else
            ResetMotion();

        prevPosition = GetRayOrigin().position;
    }

    void UpdateHoldState()
    {
        if (!rightDevice.isValid) return;

        rightDevice.TryGetFeatureValue(CommonUsages.trigger, out float trigVal);
        rightDevice.TryGetFeatureValue(CommonUsages.grip, out float gripVal);

        bool nowHolding = trigVal >= triggerThreshold && gripVal >= gripThreshold;

        if (nowHolding && !isHolding)
        {
            rightDevice.SendHapticImpulse(0, grabHapticAmplitude, grabHapticDuration);
            if (showDebugInfo) Debug.Log("[ScraperCleaner] 잡음");
        }
        else if (!nowHolding && isHolding)
        {
            // 잠금 해제 (하이라이트 포함) — 진행도는 유지
            lockedTarget = null;
            ClearHighlight();
            ResetMotion();
            if (showDebugInfo) Debug.Log("[ScraperCleaner] 놓음 (진행도 유지)");
        }

        isHolding = nowHolding;
    }

    void ScanForTarget()
    {
        Transform origin = GetRayOrigin();
        GameObject found = CastForGum(origin.position, origin.forward, forwardRayDistance)
                        ?? CastForGum(origin.position, Vector3.down, downwardRayDistance);

        if (found != null)
            LockTarget(found);
        else
            ClearHighlight();
    }

    GameObject CastForGum(Vector3 origin, Vector3 direction, float distance)
    {
        Ray ray = new Ray(origin, direction);
        RaycastHit[] hits = spherecastRadius > 0f
            ? Physics.SphereCastAll(ray, spherecastRadius, distance)
            : Physics.RaycastAll(ray, distance);

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
            if (hit.collider.CompareTag(gumTag))
                return hit.collider.gameObject;

        return null;
    }

    void LockTarget(GameObject gum)
    {
        if (lockedTarget == gum) return;

        ClearHighlight();
        lockedTarget = gum;

        if (!gumOriginalScale.ContainsKey(gum))
            gumOriginalScale[gum] = gum.transform.localScale;

        if (gumScrapeProgress.TryGetValue(gum, out int savedCount))
        {
            float t = (float)savedCount / requiredScrapes;
            gum.transform.localScale = Vector3.Lerp(gumOriginalScale[gum], gumOriginalScale[gum] * 0.1f, t);
        }

        ApplyHighlight();
        if (showDebugInfo) Debug.Log($"[ScraperCleaner] 타겟 잠금: {gum.name} (진행 {gumScrapeProgress.GetValueOrDefault(gum)}/{requiredScrapes})");
    }

    void ApplyHighlight()
    {
        if (lockedTarget == null) return;
        targetRenderer = lockedTarget.GetComponent<Renderer>();
        if (targetRenderer == null) return;

        Material mat = targetRenderer.material;
        if (!isHighlighting)
        {
            wasEmissionEnabled = mat.IsKeywordEnabled("_EMISSION");
            originalEmission = mat.GetColor("_EmissionColor");
        }

        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", highlightColor * 0.1f);
        isHighlighting = true;
    }

    void ClearHighlight()
    {
        if (!isHighlighting || targetRenderer == null) return;

        Material mat = targetRenderer.material;
        mat.SetColor("_EmissionColor", originalEmission);
        if (!wasEmissionEnabled) mat.DisableKeyword("_EMISSION");

        isHighlighting = false;
        targetRenderer = null;
    }

    void SetScrapeAudio(bool playing)
    {
        if (audioSource == null || scrapeSound == null) return;
        if (playing)
        {
            scrapeStopTimer = 0f;
            if (!isScraping)
            {
                isScraping = true;
                audioSource.clip = scrapeSound;
                audioSource.loop = true;
                audioSource.Play();
            }
        }
        else if (isScraping && scrapeStopTimer <= 0f)
        {
            scrapeStopTimer = scrapeStopDelay;
        }
    }

    void DetectScraping()
    {
        Vector3 move = GetRayOrigin().position - prevPosition;
        float dist = move.magnitude;

        if (dist > 0.5f) { SetScrapeAudio(false); return; }   // 트래킹 순간 튐 무시

        float speed = dist / Mathf.Max(Time.deltaTime, 0.0001f);
        if (speed < minScrapeSpeed)
        {
            SetScrapeAudio(false);
            return;   // 느림/정지 — 스트로크 상태 유지 (잠깐 멈췄다 이어가도 됨)
        }
        SetScrapeAudio(true);

        Vector3 dir = move / dist;

        // 첫 스트로크 방향 latch
        if (strokeDir == Vector3.zero)
        {
            strokeDir = dir;
            strokeLen = dist;
            return;
        }

        float along = Vector3.Dot(move, strokeDir);   // 기준 방향 투영 (부호 있음)

        if (along >= 0f)
        {
            strokeLen += along;   // 같은 방향으로 계속
        }
        else
        {
            // 기준 방향과 반대로 꺾임 = 한 획 종료
            if (strokeLen >= minStrokeLength && cooldownTimer <= 0f)
            {
                cooldownTimer = scrapeCooldown;
                OnScrape();
            }
            else if (showDebugInfo && strokeLen < minStrokeLength)
            {
                Debug.Log($"[ScraperCleaner] 방향 전환했지만 획이 짧음({strokeLen:F3}m < {minStrokeLength}m) — 카운트 안 함");
            }
            // 새 스트로크(반대 방향) 시작
            strokeDir = dir;
            strokeLen = dist;
        }
    }

    void OnScrape()
    {
        if (lockedTarget == null) return;

        if (!gumScrapeProgress.ContainsKey(lockedTarget))
            gumScrapeProgress[lockedTarget] = 0;

        gumScrapeProgress[lockedTarget]++;
        int count = gumScrapeProgress[lockedTarget];

        rightDevice.SendHapticImpulse(0, scrapeHapticAmplitude, scrapeHapticDuration);

        if (gumOriginalScale.TryGetValue(lockedTarget, out Vector3 origScale))
        {
            float t = (float)count / requiredScrapes;
            lockedTarget.transform.localScale = Vector3.Lerp(origScale, origScale * 0.1f, t);
        }

        if (showDebugInfo) Debug.Log($"[ScraperCleaner] 스크래핑 {count}/{requiredScrapes}");

        if (count >= requiredScrapes)
            RemoveGum();
    }

    void RemoveGum()
    {
        if (lockedTarget == null) return;

        rightDevice.SendHapticImpulse(0, removeHapticAmplitude, removeHapticDuration);

        MoneyManager moneyManager = FindFirstObjectByType<MoneyManager>();
        if (moneyManager != null) moneyManager.OnGumCleaned();

        if (showDebugInfo) Debug.Log($"[ScraperCleaner] 껌 제거: {lockedTarget.name}");

        gumScrapeProgress.Remove(lockedTarget);
        gumOriginalScale.Remove(lockedTarget);
        ClearHighlight();
        Destroy(lockedTarget);
        lockedTarget = null;
        ResetMotion();
    }

    void FullReset()
    {
        lockedTarget = null;
        ClearHighlight();
        isHolding = false;
        ResetMotion();
    }

    void ResetMotion()
    {
        strokeDir = Vector3.zero;
        strokeLen = 0f;
        SetScrapeAudio(false);
    }

    Transform GetRayOrigin()
    {
        if (toolSwitcher != null && toolSwitcher.rightHandController != null)
            return toolSwitcher.rightHandController;
        return transform;
    }

    bool IsWithinScrapeRange()
    {
        if (lockedTarget == null) return false;
        return Vector3.Distance(GetRayOrigin().position, lockedTarget.transform.position) <= scrapeDistance;
    }

    bool IsActiveTool()
    {
        return toolSwitcher != null && toolSwitcher.GetCurrentToolTag() == "Scraper";
    }
}
