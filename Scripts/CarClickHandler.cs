using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// 차 오브젝트에 부착하는 클릭 핸들러.
/// XR Ray로 차를 조준한 상태에서 트리거 또는 그립 아무거나 당기면 씬 이동.
/// (기본 selectEntered + 호버 중 컨트롤러 버튼 폴링 → 프로젝트 입력 바인딩과 무관하게 둘 다 동작)
/// goToOutroInsteadOfStreet 체크 시에는 아웃트로 씬으로 이동(GoToOutro()) — Stage3 씬의 차 전용.
///
/// 설정 방법:
///   1. 차 메시 오브젝트(또는 전용 Collider 오브젝트)에 이 스크립트 부착
///   2. Collider가 없으면 자동으로 BoxCollider 추가됨
///   3. XRSimpleInteractable도 자동 추가됨 (씬에 XRInteractionManager 필요)
/// </summary>
[RequireComponent(typeof(Collider))]
public class CarClickHandler : MonoBehaviour
{
    [Header("이동 대상")]
    [Tooltip("체크하면 Street 대신 아웃트로 씬으로 이동. Stage3(마지막 스테이지) 씬의 차에만 체크")]
    public bool goToOutroInsteadOfStreet = false;

    [Header("호버 하이라이트")]
    [Tooltip("Ray가 차에 닿았을 때 표시할 외곽선 색상")]
    public Color hoverColor = new Color(1f, 0.85f, 0.2f, 1f); // 노란빛

    [Tooltip("원본 색상과 호버 색상 사이 혼합 비율 (0~1)")]
    [Range(0f, 1f)]
    public float hoverBlend = 0.35f;

    [Header("입력")]
    [Tooltip("트리거/그립을 이 값 이상 당기면 클릭으로 인정 (0~1)")]
    [Range(0.1f, 1f)]
    public float pressThreshold = 0.6f;

    [Header("디버그")]
    public bool showDebugInfo = true;

    // ── 내부 상태 ──────────────────────────────────────
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable;
    private Renderer[]           cachedRenderers;
    private Color[][]            originalColors;   // [렌더러 인덱스][머터리얼 인덱스]
    private bool                 hasTriggered = false;
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

        // 렌더러 & 원본 색상 캐싱 (Awake에서 한 번만, 수정 전에 저장)
        CacheRenderers();
    }

    void Start()
    {
        interactable.hoverEntered.AddListener(OnHoverEnter);
        interactable.hoverExited.AddListener(OnHoverExit);
        interactable.selectEntered.AddListener(OnCarClicked);

        // 청소완료 버튼을 누르거나 제한시간이 끝나 Rank UI가 뜨기 전까지는 차를 클릭할 수 없다.
        // ScoreManager.ShowRankUI()에서 Unlock()을 호출해 풀어준다.
        interactable.enabled = false;
    }

    void OnDestroy()
    {
        if (interactable == null) return;
        interactable.hoverEntered.RemoveListener(OnHoverEnter);
        interactable.hoverExited.RemoveListener(OnHoverExit);
        interactable.selectEntered.RemoveListener(OnCarClicked);
    }

    // ═══════════════════════════════════════════════════
    //  XR Interaction 이벤트
    // ═══════════════════════════════════════════════════

    /// <summary>Ray가 차에 진입했을 때 → 색상 하이라이트 + 버튼 폴링 시작</summary>
    private void OnHoverEnter(HoverEnterEventArgs args)
    {
        isHovered = true;
        wasPressed = AnyTriggerOrGripPressed(); // 이미 당겨진 상태로 진입해도 즉시 발동되지 않도록
        ApplyHoverColor();
    }

    /// <summary>Ray가 차에서 벗어났을 때 → 원본 색상 복원</summary>
    private void OnHoverExit(HoverExitEventArgs args)
    {
        isHovered = false;
        wasPressed = false;
        RestoreColors();
    }

    // 호버 중 트리거/그립 아무거나 새로 당기면 클릭 처리 (selectEntered 바인딩과 무관하게)
    void Update()
    {
        if (hasTriggered || !isHovered || interactable == null || !interactable.enabled)
        {
            wasPressed = false;
            return;
        }

        bool pressed = AnyTriggerOrGripPressed();
        if (pressed && !wasPressed)
            OnCarClicked(default);
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

    /// <summary>Rank UI가 뜬 뒤(청소완료 버튼 또는 제한시간 종료) 차 클릭을 허용한다.</summary>
    public void Unlock()
    {
        if (interactable != null)
            interactable.enabled = true;
    }

    /// <summary>차를 클릭했을 때 → Street씬(기본) 또는 아웃트로 씬으로 이동</summary>
    private void OnCarClicked(SelectEnterEventArgs args)
    {
        if (hasTriggered) return;
        hasTriggered = true;

        Log($"차 클릭 감지 → {(goToOutroInsteadOfStreet ? "GoToOutro()" : "GoToStreet()")} 호출");

        // 중복 클릭 방지
        interactable.enabled = false;
        RestoreColors();

        if (StageFlowManager.Instance != null)
        {
            if (goToOutroInsteadOfStreet)
                StageFlowManager.Instance.GoToOutro();
            else
                StageFlowManager.Instance.GoToStreet();
        }
        else
        {
            Debug.LogError("[CarClickHandler] StageFlowManager.Instance가 null입니다. " +
                           "StageFlowManager 오브젝트가 씬에 있는지 확인하세요!");
        }
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
        if (showDebugInfo) Debug.Log($"[CarClickHandler] {msg}");
    }
}
