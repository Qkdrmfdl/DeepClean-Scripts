using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

public class ToiletBrushCleaner : MonoBehaviour
{
    [Header("청소 설정")]
    [SerializeField] private string cleanableTag = "Cleanable_ToiletBrush";
    [SerializeField] private string toolTagPrefix = "ToiletBrush";

    [Tooltip("완전히 청소하는 데 필요한 왕복 횟수")]
    public int totalScrubCount = 3;

    [Header("범위 설정")]
    [Tooltip("얼룩을 감지하는 반경 (m)")]
    public float reachDistance = 0.4f;

    [Tooltip("얼룩 콜라이더 표면에서 이 거리(m) 이내에 브러시 헤드가 있으면 계속 스크러빙 인식. " +
             "얼룩 pivot이 아니라 실제 콜라이더 기준이라 콜라이더 center 오프셋이 커도 정상 동작. " +
             "잠금(reachDistance)보다 넉넉히 크게 둬서 히스테리시스 확보")]
    public float scrubDistance = 0.6f;

    [Header("스크러빙 감지 설정")]
    [Tooltip("스크럽으로 인정할 브러시 헤드 최소 이동 속도 (m/s). 이보다 느리면 무시 (트래킹 떨림 제거)")]
    public float minScrubSpeed = 0.1f;

    [Tooltip("한 스트로크(한 방향 쓸기)로 인정할 최소 이동 거리 (m). 이보다 짧으면 떨림으로 보고 왕복 카운트 안 함")]
    public float minStrokeLength = 0.03f;

    [Tooltip("왕복 1회 인정 후 다음 인정까지 최소 간격 (초) — 연타/떨림 방지")]
    public float directionChangeDelay = 0.12f;

    [Header("잡기 설정")]
    [Tooltip("트리거 임계값 (0~1)")]
    public float triggerThreshold = 0.7f;

    [Tooltip("그립 임계값 (0~1)")]
    public float gripThreshold = 0.7f;

    [Header("햅틱")]
    public float grabHapticAmplitude = 0.3f;
    public float grabHapticDuration = 0.1f;
    public float scrubHapticAmplitude = 0.35f;
    public float scrubHapticDuration = 0.08f;
    public float removeHapticAmplitude = 0.9f;
    public float removeHapticDuration = 0.25f;

    [Header("솔 위치")]
    [Tooltip("솔 머리 부분의 Transform (없으면 루트 기준)")]
    public Transform brushHead;

    [Header("효과음")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip scrubSound;

    [Header("연동")]
    public ToolSwitcher toolSwitcher;

    [Header("디버그")]
    public bool showDebugInfo = true;

    // 컨트롤러
    private InputDevice rightDevice;
    private bool isHolding = false;

    // 타겟
    private GameObject lockedTarget = null;

    // 얼룩별 진행도 / DecalProjector / Collider 캐시
    private Dictionary<GameObject, int> stainScrubProgress = new Dictionary<GameObject, int>();
    private Dictionary<GameObject, DecalProjector> stainDecalCache = new Dictionary<GameObject, DecalProjector>();
    private Dictionary<GameObject, Collider> stainColliderCache = new Dictionary<GameObject, Collider>();

    // 모션 감지 — 브러시 '헤드'의 실제 이동을 본다 (컨트롤러 병진 속도 X).
    // 변기는 손목만 돌려 문지르는 경우가 많아 컨트롤러 자체는 거의 안 움직이지만 브러시 끝은 크게 쓸린다.
    // 방향 무관: "스트로크(한 방향 쓸기)"의 기준 방향을 latch 해두고, 그 반대로 꺾이면 왕복 1회.
    // 한 방향으로 쭉 드는 동작은 꺾임이 없으므로 절대 카운트되지 않는다.
    private Vector3 prevBrushPos;
    private bool hasPrevBrushPos = false;
    private Vector3 strokeDir = Vector3.zero;   // 현재 스트로크의 기준 방향 (단위 벡터)
    private float strokeLen = 0f;               // 기준 방향으로 이동한 누적 거리
    private float directionChangeTimer = 0f;
    private bool isScrubbing = false;
    private float scrubStopTimer = 0f;

    [Tooltip("문지르기를 멈춘 뒤 이 시간(초) 안에 다시 문지르기 시작하면 효과음을 끊지 않고 이어서 재생한다. (진공청소기와 동일한 방식)")]
    [SerializeField] private float scrubStopDelay = 2f;

    void Start()
    {
        if (toolSwitcher == null)
            toolSwitcher = FindFirstObjectByType<ToolSwitcher>();

        var chars = InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(chars, devices);
        if (devices.Count > 0) rightDevice = devices[0];

        prevBrushPos = BrushPosition;
        hasPrevBrushPos = true;

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

        if (scrubStopTimer > 0f)
        {
            scrubStopTimer -= Time.deltaTime;
            if (scrubStopTimer <= 0f) { isScrubbing = false; audioSource?.Stop(); }
        }

        if (directionChangeTimer > 0f)
            directionChangeTimer -= Time.deltaTime;

        if (!IsActiveTool())
        {
            FullReset();
            StopScrubAudioImmediate();
            prevBrushPos = BrushPosition;
            return;
        }

        UpdateHoldState();

        if (isHolding)
        {
            if (lockedTarget == null)
                ScanForTarget();

            if (lockedTarget != null && IsWithinScrubRange())
            {
                DetectScrubbing();
            }
            else
            {
                // 잠근 얼룩에서 멀어졌으면 잠금을 풀어 다음 프레임에 가까운 얼룩을 다시 찾게 한다
                // (안 그러면 다른 얼룩 위에서 문질러도 계속 옛 얼룩만 붙잡고 있음)
                lockedTarget = null;
                ResetMotion();
            }
        }
        else
        {
            lockedTarget = null;
            ResetMotion();
        }

        // 다음 프레임 이동량 계산용 — 모든 경로에서 갱신 (안 하면 범위 밖→안 복귀 시 이동량이 폭발)
        prevBrushPos = BrushPosition;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(BrushPosition, reachDistance);
    }

    // ===== 입력 =====

    void UpdateHoldState()
    {
        if (!rightDevice.isValid) return;

        rightDevice.TryGetFeatureValue(CommonUsages.trigger, out float trigVal);
        rightDevice.TryGetFeatureValue(CommonUsages.grip, out float gripVal);

        bool nowHolding = trigVal >= triggerThreshold && gripVal >= gripThreshold;

        if (nowHolding && !isHolding)
        {
            rightDevice.SendHapticImpulse(0, grabHapticAmplitude, grabHapticDuration);
            if (showDebugInfo) Debug.Log("[ToiletBrush] 잡음");
        }
        else if (!nowHolding && isHolding)
        {
            // 진행도는 유지, 잠금만 해제
            lockedTarget = null;
            ResetMotion();
            if (showDebugInfo) Debug.Log("[ToiletBrush] 놓음 (진행도 유지)");
        }

        isHolding = nowHolding;
    }

    // ===== 타겟 탐색 =====

    void ScanForTarget()
    {
        Collider[] hits = Physics.OverlapSphere(BrushPosition, reachDistance);

        GameObject nearest = null;
        float nearestDist = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag(cleanableTag)) continue;

            // pivot이 아니라 콜라이더 표면까지의 거리로 가장 가까운 얼룩 선택
            float dist = Vector3.Distance(BrushPosition, hit.ClosestPoint(BrushPosition));
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = hit.gameObject;
            }
        }

        if (nearest != null)
            LockTarget(nearest);
    }

    void LockTarget(GameObject stain)
    {
        if (lockedTarget == stain) return;

        lockedTarget = stain;

        if (!stainDecalCache.ContainsKey(stain))
        {
            var dp = stain.GetComponent<DecalProjector>();
            if (dp != null)
                stainDecalCache[stain] = dp;
            else
                Debug.LogWarning($"[ToiletBrush] DecalProjector 없음: {stain.name}");
        }

        if (!stainColliderCache.ContainsKey(stain))
            stainColliderCache[stain] = stain.GetComponent<Collider>();

        int progress = stainScrubProgress.GetValueOrDefault(stain, 0);
        if (showDebugInfo) Debug.Log($"[ToiletBrush] 타겟 잠금: {stain.name} ({progress}/{totalScrubCount})");
    }

    // ===== 스크러빙 감지 (스트로크 방향 반전 기반, 방향 무관) =====

    void SetScrubAudio(bool playing)
    {
        if (audioSource == null || scrubSound == null) return;
        if (playing)
        {
            scrubStopTimer = 0f;
            if (!isScrubbing)
            {
                isScrubbing = true;
                audioSource.clip = scrubSound;
                audioSource.loop = true;
                audioSource.Play();
            }
        }
        else if (isScrubbing && scrubStopTimer <= 0f)
        {
            scrubStopTimer = scrubStopDelay;
        }
    }

    // 도구를 바꾸는 등 확실히 멈춰야 하는 경우엔 유예 시간 없이 즉시 정지
    void StopScrubAudioImmediate()
    {
        scrubStopTimer = 0f;
        if (isScrubbing)
        {
            isScrubbing = false;
            audioSource?.Stop();
        }
    }

    void DetectScrubbing()
    {
        Vector3 curPos = BrushPosition;

        if (!hasPrevBrushPos)
        {
            prevBrushPos = curPos;
            hasPrevBrushPos = true;
            SetScrubAudio(false);
            return;
        }

        // 이번 프레임 브러시 헤드 이동 벡터
        Vector3 move = curPos - prevBrushPos;
        float dist = move.magnitude;

        if (dist > 0.5f) { SetScrubAudio(false); return; }   // 트래킹 순간 튐 무시

        float speed = dist / Mathf.Max(Time.deltaTime, 0.0001f);
        if (speed < minScrubSpeed)
        {
            SetScrubAudio(false);
            return;   // 느림/정지 — 스트로크 상태는 유지 (잠깐 멈췄다 이어가도 됨)
        }
        SetScrubAudio(true);

        Vector3 dir = move / dist;

        // 첫 스트로크 방향 latch
        if (strokeDir == Vector3.zero)
        {
            strokeDir = dir;
            strokeLen = dist;
            return;
        }

        // 현재 이동을 "스트로크 기준 방향"에 투영 (부호 있음)
        float along = Vector3.Dot(move, strokeDir);

        if (along >= 0f)
        {
            // 기준 방향과 같은 쪽 → 스트로크 계속
            strokeLen += along;
        }
        else
        {
            // 기준 방향과 반대로 꺾임 = 한 스트로크 종료 → 왕복 1회
            if (strokeLen >= minStrokeLength && directionChangeTimer <= 0f)
            {
                directionChangeTimer = directionChangeDelay;
                OnScrubCycleComplete();
            }
            else if (showDebugInfo)
            {
                Debug.Log($"[ToiletBrush] 방향 전환 감지했지만 스트로크 짧음({strokeLen:F3}m < {minStrokeLength}m) — 카운트 안 함");
            }
            // 새 스트로크(반대 방향) 시작
            strokeDir = dir;
            strokeLen = dist;
        }
    }

    void OnScrubCycleComplete()
    {
        if (lockedTarget == null) return;

        if (!stainScrubProgress.ContainsKey(lockedTarget))
            stainScrubProgress[lockedTarget] = 0;

        stainScrubProgress[lockedTarget]++;
        int count = stainScrubProgress[lockedTarget];

        rightDevice.SendHapticImpulse(0, scrubHapticAmplitude, scrubHapticDuration);

        // fadeFactor를 3번에 걸쳐 0으로
        if (stainDecalCache.TryGetValue(lockedTarget, out DecalProjector dp))
        {
            float newFade = 1f - ((float)count / totalScrubCount);
            dp.fadeFactor = Mathf.Max(0f, newFade);
        }

        if (showDebugInfo) Debug.Log($"[ToiletBrush] 문지르기 {count}/{totalScrubCount}");

        if (count >= totalScrubCount)
            RemoveStain();
    }

    void RemoveStain()
    {
        if (lockedTarget == null) return;

        rightDevice.SendHapticImpulse(0, removeHapticAmplitude, removeHapticDuration);

        MoneyManager moneyManager = FindFirstObjectByType<MoneyManager>();
        if (moneyManager != null)
            moneyManager.OnToiletStainCleaned();

        if (showDebugInfo) Debug.Log($"[ToiletBrush] 얼룩 제거: {lockedTarget.name}");

        stainScrubProgress.Remove(lockedTarget);
        stainDecalCache.Remove(lockedTarget);
        stainColliderCache.Remove(lockedTarget);
        Destroy(lockedTarget);
        lockedTarget = null;
        ResetMotion();
    }

    // ===== 유틸 =====

    void FullReset()
    {
        lockedTarget = null;
        isHolding = false;
        ResetMotion();
    }

    void ResetMotion()
    {
        strokeDir = Vector3.zero;
        strokeLen = 0f;
        SetScrubAudio(false);
    }

    bool IsWithinScrubRange()
    {
        if (lockedTarget == null) return false;

        // 얼룩 pivot이 아니라 실제 콜라이더 표면까지의 거리로 판정한다.
        // (변기 얼룩 데칼은 콜라이더 center 오프셋이 커서 pivot 기준으로 재면
        //  가로로 문지를 때 거리 임계를 넘나들어 스크럽이 계속 끊긴다 — 이게 "위아래만 되던" 원인)
        Vector3 refPoint;
        if (stainColliderCache.TryGetValue(lockedTarget, out Collider col) && col != null)
            refPoint = col.ClosestPoint(BrushPosition);   // 브러시가 콜라이더 안이면 BrushPosition 그대로 → 거리 0
        else
            refPoint = lockedTarget.transform.position;

        return Vector3.Distance(BrushPosition, refPoint) <= scrubDistance;
    }

    bool IsActiveTool()
    {
        return toolSwitcher != null && toolSwitcher.GetCurrentToolTag().StartsWith(toolTagPrefix);
    }

    Vector3 BrushPosition => brushHead != null ? brushHead.position : transform.position;
}
