using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// 운전대 오브젝트에 부착하는 클릭 핸들러.
/// XR Ray로 운전대를 조준한 상태에서 트리거 또는 그립 아무거나 당기면 AutoDrive.StartDriving() 호출.
/// (기본 selectEntered + 호버 중 컨트롤러 버튼 폴링 → 프로젝트 입력 바인딩과 무관하게 둘 다 동작)
///
/// 설정 방법:
///   1. 운전대 메시 오브젝트에 이 스크립트 부착
///   2. Auto Drive 필드에 AutoDrive 컴포넌트가 있는 오브젝트 할당
///      (비워두면 씬에서 자동 검색)
///   3. Collider 없으면 자동으로 BoxCollider 추가됨
///   4. 운전 시작 후 Interactable 자동 비활성화 (재클릭 방지)
///
/// 연계 스크립트:
///   AutoDrive → 목적지 도착 후 ExitCarAfterDelay() 자동 실행 (2초 대기 후 하차)
/// </summary>
[RequireComponent(typeof(Collider))]
public class SteeringWheelInteraction : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("같은 차 오브젝트 또는 자식에 있는 AutoDrive 컴포넌트. 비워두면 자동 검색.")]
    public AutoDrive autoDrive;

    [Header("호버 하이라이트")]
    [Tooltip("Ray가 운전대에 닿았을 때 표시할 색상")]
    public Color hoverColor = new Color(0.3f, 0.85f, 1f, 1f); // 하늘색

    [Tooltip("원본 색상과 호버 색상 사이 혼합 비율 (0~1)")]
    [Range(0f, 1f)]
    public float hoverBlend = 0.4f;

    [Header("운전 시작 안내 UI (선택)")]
    [Tooltip("운전대 위에 표시할 '클릭하여 출발' 안내 UI. 없으면 무시.")]
    public GameObject driveGuideUI;

    [Header("입력")]
    [Tooltip("트리거/그립을 이 값 이상 당기면 클릭으로 인정 (0~1)")]
    [Range(0.1f, 1f)]
    public float pressThreshold = 0.6f;

    [Header("디버그")]
    public bool showDebugInfo = true;

    // ── 내부 상태 ──────────────────────────────────────
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable;
    private Renderer[]           cachedRenderers;
    private Color[][]            originalColors;
    private bool                 hasDriveStarted = false;
    private bool                 isHovered = false;
    private bool                 wasPressed = false;
    private readonly List<UnityEngine.XR.InputDevice> _controllers = new List<UnityEngine.XR.InputDevice>();

    // ═══════════════════════════════════════════════════
    //  Unity 생명주기
    // ═══════════════════════════════════════════════════

    void Awake()
    {
        // XRSimpleInteractable 자동 추가
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        if (interactable == null)
            interactable = gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();

        CacheRenderers();
    }

    void Start()
    {
        // AutoDrive 자동 검색 (Inspector에서 할당 안 했을 때)
        if (autoDrive == null)
        {
            autoDrive = GetComponentInParent<AutoDrive>();
            if (autoDrive == null)
                autoDrive = FindFirstObjectByType<AutoDrive>();

            if (autoDrive == null)
                Debug.LogError("[SteeringWheelInteraction] AutoDrive를 찾을 수 없습니다!");
        }

        interactable.hoverEntered.AddListener(OnHoverEnter);
        interactable.hoverExited.AddListener(OnHoverExit);
        interactable.selectEntered.AddListener(OnSteeringWheelClicked);

        // 안내 UI 초기 표시
        if (driveGuideUI != null)
            driveGuideUI.SetActive(true);
    }

    void OnDestroy()
    {
        if (interactable == null) return;
        interactable.hoverEntered.RemoveListener(OnHoverEnter);
        interactable.hoverExited.RemoveListener(OnHoverExit);
        interactable.selectEntered.RemoveListener(OnSteeringWheelClicked);
    }

    // ═══════════════════════════════════════════════════
    //  XR Interaction 이벤트
    // ═══════════════════════════════════════════════════

    /// <summary>Ray가 운전대에 진입 → 색상 하이라이트 + 버튼 폴링 시작</summary>
    private void OnHoverEnter(HoverEnterEventArgs args)
    {
        isHovered = true;
        wasPressed = AnyTriggerOrGripPressed(); // 이미 당겨진 상태로 진입해도 즉시 발동되지 않도록
        ApplyHoverColor();
    }

    /// <summary>Ray가 운전대에서 벗어남 → 색상 복원</summary>
    private void OnHoverExit(HoverExitEventArgs args)
    {
        isHovered = false;
        wasPressed = false;
        RestoreColors();
    }

    // 호버 중 트리거/그립 아무거나 새로 당기면 클릭 처리 (selectEntered 바인딩과 무관하게)
    void Update()
    {
        if (hasDriveStarted || !isHovered || interactable == null || !interactable.enabled)
        {
            wasPressed = false;
            return;
        }

        bool pressed = AnyTriggerOrGripPressed();
        if (pressed && !wasPressed)
            OnSteeringWheelClicked(default);
        wasPressed = pressed;
    }

    private bool AnyTriggerOrGripPressed()
    {
        _controllers.Clear();
        UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
            UnityEngine.XR.InputDeviceCharacteristics.Controller, _controllers);

        foreach (var d in _controllers)
        {
            if (d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float t) && t >= pressThreshold) return true;
            if (d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip,    out float g) && g >= pressThreshold) return true;
        }
        return false;
    }

    /// <summary>운전대 클릭 → 자율운전 시작</summary>
    private void OnSteeringWheelClicked(SelectEnterEventArgs args)
    {
        if (hasDriveStarted) return;
        hasDriveStarted = true;

        Log("운전대 클릭 감지 → 자율운전 시작");

        // 클릭 후 Interactable & 색상 초기화
        interactable.enabled = false;
        RestoreColors();

        // 안내 UI 숨김
        if (driveGuideUI != null)
            driveGuideUI.SetActive(false);

        // 자율운전 시작
        // AutoDrive.onDrivingComplete 이벤트는 도착 후 2초 뒤 자동 발동
        // → TrunkInteractionController.UnlockTrunk() 연결 권장 (Inspector에서 설정)
        if (autoDrive != null)
            autoDrive.StartDriving();
        else
            Debug.LogError("[SteeringWheelInteraction] AutoDrive가 null입니다!");
    }

    // ═══════════════════════════════════════════════════
    //  색상 처리
    // ═══════════════════════════════════════════════════

    private void CacheRenderers()
    {
        cachedRenderers = GetComponentsInChildren<Renderer>();
        originalColors  = new Color[cachedRenderers.Length][];

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Material[] mats = cachedRenderers[i].materials;
            originalColors[i] = new Color[mats.Length];
            for (int m = 0; m < mats.Length; m++)
                originalColors[i][m] = mats[m].color;
        }
    }

    private void ApplyHoverColor()
    {
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Material[] mats = cachedRenderers[i].materials;
            for (int m = 0; m < mats.Length; m++)
                mats[m].color = Color.Lerp(originalColors[i][m], hoverColor, hoverBlend);
        }
    }

    private void RestoreColors()
    {
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Material[] mats = cachedRenderers[i].materials;
            for (int m = 0; m < mats.Length; m++)
                mats[m].color = originalColors[i][m];
        }
    }

    // ═══════════════════════════════════════════════════
    //  디버그
    // ═══════════════════════════════════════════════════

    private void Log(string msg)
    {
        if (showDebugInfo) Debug.Log($"[SteeringWheelInteraction] {msg}");
    }
}
