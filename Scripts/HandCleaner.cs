using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Hand 버튼 선택 시에만 활성화.
/// 오른쪽 컨트롤러로 쓰레기를 트리거+그랩으로 집고,
/// 왼쪽 쓰레기통에 드래그하면 청소 완료.
/// 손을 놓으면 쓰레기가 원래 위치로 복귀.
/// </summary>
[DefaultExecutionOrder(1000)]
public class HandCleaner : MonoBehaviour
{
    [Header("ToolSwitcher 참조")]
    public ToolSwitcher toolSwitcher;

    [Header("컨트롤러 / 쓰레기통")]
    [Tooltip("오른쪽 컨트롤러 Transform (비우면 NearFarInteractor Transform 자동 사용)")]
    public Transform rightControllerTransform;
    [Tooltip("왼쪽 컨트롤러에 부착된 쓰레기통 GameObject (Hand 모드일 때만 표시)")]
    public GameObject trashBinObject;

    [Header("감지 범위")]
    [Tooltip("자석 당김이 시작되는 반경 (m)")]
    public float nearDetectRadius = 0.4f;
    [Tooltip("이 거리 이하면 쓰레기가 컨트롤러에 부착됨")]
    public float attachThreshold = 0.06f;
    [Tooltip("쓰레기통에 이 거리 이하로 들어오면 청소 완료")]
    public float trashBinRadius = 0.2f;

    [Header("이동 속도")]
    public float magnetSpeed = 3f;
    public float followSpeed = 20f;
    public float returnSpeed = 2f;

    [Header("하이라이트")]
    public Color highlightColor = Color.yellow;

    [Header("햅틱")]
    public float grabHapticAmplitude = 0.4f;
    public float disposeHapticAmplitude = 0.8f;
    public float hapticDuration = 0.15f;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private AudioClip disposeSound;

    // ── 상태 머신 ──────────────────────────────
    enum PickupState { Idle, Pulling, Holding, Returning }
    PickupState state = PickupState.Idle;

    private GameObject heldTrash;
    private Vector3 trashOriginalPosition;

    private GameObject highlightedTrash;
    private Renderer highlightRenderer;
    private Color originalEmission;
    private bool wasEmissionEnabled;

    private NearFarInteractor nearFarInteractor;
    private InputDevice rightDevice;
    private InputDevice leftDevice;

    [System.Serializable]
    public class HoldOffsetOverride
    {
        [Tooltip("오브젝트 이름에 이 문자열이 포함되면 아래 오프셋 사용 (대소문자 무시)")]
        public string nameKeyword;
        [Tooltip("컨트롤러 로컬 좌표 기준 오프셋 (기본값: 0, -0.05, 0.05)")]
        public Vector3 holdOffset = new Vector3(0f, -0.05f, 0.05f);
    }

    [Header("오브젝트별 부착 오프셋 (이름 키워드로 매칭)")]
    [Tooltip("비어 있으면 모든 오브젝트에 기본 오프셋 적용")]
    public HoldOffsetOverride[] holdOffsetOverrides = new HoldOffsetOverride[]
    {
        new HoldOffsetOverride { nameKeyword = "box", holdOffset = new Vector3(0f, -0.1f, 0.35f) }
    };

    private static readonly Vector3 k_HoldOffset = new Vector3(0f, -0.05f, 0.05f);
    private Vector3 currentHoldOffset = new Vector3(0f, -0.05f, 0.05f);

    // ── trashBinObject에서 캐싱 ────────────────
    private Transform trashBinTransform;

    void Start()
    {
        if (toolSwitcher == null)
            toolSwitcher = FindFirstObjectByType<ToolSwitcher>();

        nearFarInteractor = toolSwitcher != null ? toolSwitcher.RightNearFarInteractor : null;

        if (rightControllerTransform == null && nearFarInteractor != null)
            rightControllerTransform = nearFarInteractor.transform;

        if (trashBinObject != null)
        {
            trashBinTransform = trashBinObject.transform;
            trashBinObject.SetActive(false); // 시작 시 숨김
        }

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        CacheInputDevices();
    }

    void CacheInputDevices()
    {
        var list = new List<InputDevice>();

        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, list);
        if (list.Count > 0) rightDevice = list[0];

        list.Clear();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, list);
        if (list.Count > 0) leftDevice = list[0];
    }

    void Update()
    {
        if (!rightDevice.isValid || !leftDevice.isValid)
            CacheInputDevices();

        bool handMode = toolSwitcher != null && toolSwitcher.IsHandModeActive;

        // 쓰레기통 표시/숨김
        if (trashBinObject != null && trashBinObject.activeSelf != handMode)
            trashBinObject.SetActive(handMode);

        // Hand 모드가 아니면 모두 초기화
        if (!handMode)
        {
            ClearHighlight();
            if (state == PickupState.Pulling || state == PickupState.Holding)
                StartReturning();
            if (state == PickupState.Returning)
                UpdateReturning();
            return;
        }

        switch (state)
        {
            case PickupState.Idle:      UpdateIdle();      break;
            case PickupState.Pulling:   UpdatePulling();   break;
            case PickupState.Holding:   UpdateHolding();   break;
            case PickupState.Returning: UpdateReturning(); break;
        }
    }

    // ── Idle: 주변 쓰레기 탐색 + 하이라이트 ───────────────────────────
    void UpdateIdle()
    {
        Transform origin = ControllerOrigin();
        GameObject nearest = FindNearestTrash(origin.position, nearDetectRadius);

        if (nearest != highlightedTrash)
        {
            ClearHighlight();
            if (nearest != null) ApplyHighlight(nearest);
        }

        if (highlightedTrash != null && IsGrabInputDown())
            StartPulling(highlightedTrash);
    }

    // ── Pulling: 자석처럼 컨트롤러 쪽으로 당김 ────────────────────────
    void UpdatePulling()
    {
        if (heldTrash == null) { state = PickupState.Idle; return; }

        if (!IsGrabInputHeld())
        {
            StartReturning();
            return;
        }

        Vector3 target = ControllerOrigin().TransformPoint(currentHoldOffset);
        heldTrash.transform.position = Vector3.MoveTowards(
            heldTrash.transform.position, target, magnetSpeed * Time.deltaTime);

        if (Vector3.Distance(heldTrash.transform.position, target) < attachThreshold)
        {
            state = PickupState.Holding;
            SendHaptic(rightDevice, grabHapticAmplitude, hapticDuration);
        }
    }

    // ── Holding: 컨트롤러에 붙어 따라오고, 쓰레기통 체크 ─────────────
    void UpdateHolding()
    {
        if (heldTrash == null) { state = PickupState.Idle; return; }

        if (!IsGrabInputHeld())
        {
            StartReturning();
            return;
        }

        Vector3 target = ControllerOrigin().TransformPoint(currentHoldOffset);
        heldTrash.transform.position = Vector3.Lerp(
            heldTrash.transform.position, target, followSpeed * Time.deltaTime);

        if (trashBinTransform == null)
        {
            Debug.LogWarning("[HandCleaner] trashBinObject가 연결되지 않았습니다.");
            return;
        }

        float distToBin = Vector3.Distance(heldTrash.transform.position, trashBinTransform.position);
        Debug.Log($"[HandCleaner] 쓰레기↔쓰레기통: {distToBin:F3}m (판정: {trashBinRadius}m)");

        if (distToBin < trashBinRadius)
            DisposeTrash();
    }

    // ── Returning: 원래 위치로 복귀 ────────────────────────────────────
    void UpdateReturning()
    {
        if (heldTrash == null) { state = PickupState.Idle; return; }

        heldTrash.transform.position = Vector3.MoveTowards(
            heldTrash.transform.position, trashOriginalPosition, returnSpeed * Time.deltaTime);

        if (Vector3.Distance(heldTrash.transform.position, trashOriginalPosition) < 0.01f)
        {
            heldTrash.transform.position = trashOriginalPosition;
            RestoreRigidbody(heldTrash);
            heldTrash = null;
            state = PickupState.Idle;
        }
    }

    // ── 동작 ──────────────────────────────────────────────────────────
    Vector3 ResolveHoldOffset(GameObject trash)
    {
        if (holdOffsetOverrides != null)
        {
            string objName = trash.name.ToLower();
            foreach (var ov in holdOffsetOverrides)
            {
                if (!string.IsNullOrEmpty(ov.nameKeyword) &&
                    objName.Contains(ov.nameKeyword.ToLower()))
                    return ov.holdOffset;
            }
        }
        return k_HoldOffset;
    }

    void StartPulling(GameObject trash)
    {
        ClearHighlight();
        heldTrash = trash;
        trashOriginalPosition = trash.transform.position;
        currentHoldOffset = ResolveHoldOffset(trash);
        state = PickupState.Pulling;

        var rb = heldTrash.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        if (audioSource != null && pickupSound != null)
            audioSource.PlayOneShot(pickupSound);
    }

    void StartReturning()
    {
        if (heldTrash == null) { state = PickupState.Idle; return; }
        state = PickupState.Returning;
    }

    void DisposeTrash()
    {
        var moneyManager = FindFirstObjectByType<MoneyManager>();
        if (moneyManager != null)
            moneyManager.OnTrashCleaned();

        SendHaptic(rightDevice, disposeHapticAmplitude, hapticDuration);
        SendHaptic(leftDevice,  disposeHapticAmplitude, hapticDuration);

        if (audioSource != null && disposeSound != null)
            audioSource.PlayOneShot(disposeSound);

        Destroy(heldTrash);
        heldTrash = null;
        state = PickupState.Idle;
    }

    static void RestoreRigidbody(GameObject obj)
    {
        var rb = obj.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;
    }

    // ── 유틸 ──────────────────────────────────────────────────────────
    Transform ControllerOrigin() =>
        rightControllerTransform != null ? rightControllerTransform : transform;

    GameObject FindNearestTrash(Vector3 center, float radius)
    {
        Collider[] cols = Physics.OverlapSphere(center, radius);
        GameObject nearest = null;
        float minDist = float.MaxValue;

        foreach (var col in cols)
        {
            GameObject candidate = FindCleanableInHierarchy(col.gameObject);
            if (candidate == null) continue;

            float dist = Vector3.Distance(center, candidate.transform.position);
            if (dist < minDist) { minDist = dist; nearest = candidate; }
        }

        return nearest;
    }

    GameObject FindCleanableInHierarchy(GameObject obj)
    {
        if (obj.CompareTag("Cleanable_Hand")) return obj;
        foreach (Transform child in obj.transform)
        {
            var result = FindCleanableInHierarchy(child.gameObject);
            if (result != null) return result;
        }
        return null;
    }

    void ApplyHighlight(GameObject target)
    {
        highlightedTrash = target;
        highlightRenderer = target.GetComponent<Renderer>();
        if (highlightRenderer == null) return;

        Material mat = highlightRenderer.material;
        wasEmissionEnabled = mat.IsKeywordEnabled("_EMISSION");
        originalEmission = mat.GetColor("_EmissionColor");
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", highlightColor * 0.15f);
    }

    void ClearHighlight()
    {
        if (highlightRenderer == null) { highlightedTrash = null; return; }

        Material mat = highlightRenderer.material;
        mat.SetColor("_EmissionColor", originalEmission);
        if (!wasEmissionEnabled) mat.DisableKeyword("_EMISSION");

        highlightRenderer = null;
        highlightedTrash = null;
    }

    // 트리거 + 그랩 동시 입력 감지 ─────────────────────────────────────
    bool IsGrabInputHeld()
    {
        if (!rightDevice.isValid) return false;
        rightDevice.TryGetFeatureValue(CommonUsages.triggerButton, out bool trigger);
        rightDevice.TryGetFeatureValue(CommonUsages.gripButton,   out bool grip);
        return trigger && grip;
    }

    bool IsGrabInputDown()
    {
        // 이번 프레임에 처음 동시 입력됐을 때만 true (엣지 감지는 Pulling 진입 시 1회)
        return IsGrabInputHeld();
    }

    static void SendHaptic(InputDevice device, float amplitude, float duration)
    {
        if (device.isValid)
            device.SendHapticImpulse(0, amplitude, duration);
    }

    // ── Gizmos (Scene 뷰 시각화) ──────────────────────────────────────
    void OnDrawGizmos()
    {
        if (trashBinTransform != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawSphere(trashBinTransform.position, trashBinRadius);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(trashBinTransform.position, trashBinRadius);
        }

        Transform origin = rightControllerTransform != null ? rightControllerTransform : transform;
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.12f);
        Gizmos.DrawSphere(origin.position, nearDetectRadius);
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.7f);
        Gizmos.DrawWireSphere(origin.position, nearDetectRadius);
    }
}
