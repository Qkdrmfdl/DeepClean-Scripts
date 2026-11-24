using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 패드 UI의 '청소도구 설명 코너'.
/// 도구 버튼을 누르면 TutorialDemoController 로 해당 도구 시뮬레이션을 눈앞에서 재생한다.
/// (튜토리얼의 청소도구 데모와 같은 엔진을 재사용)
///
/// 필요 조건:
///   • 같은 씬에 TutorialDemoController 가 도구 오브젝트/앵커까지 세팅된 채로 있어야 함
///     (현재 Stage1 / Stage3 / Tutorial 에 존재)
/// </summary>
public class PadToolSimulator : MonoBehaviour
{
    [System.Serializable]
    public class ToolButton
    {
        [Tooltip("설명 코너의 도구 버튼")]
        public Button button;

        [Tooltip("이 버튼이 재생할 시뮬레이션")]
        public ToolDemoType demoType;
    }

    [Header("참조")]
    [Tooltip("비우면 씬에서 자동 검색")]
    public TutorialDemoController demoController;

    [Tooltip("시뮬레이션 시작 시 닫고, 끝나면 다시 열 패드 패널. 비우면 씬에서 자동 검색")]
    public UIPanelToggle padPanel;

    [Header("도구 버튼")]
    [Tooltip("각 버튼 ↔ 시뮬레이션 종류 매핑. Start 에서 onClick 을 자동 연결한다.")]
    public ToolButton[] toolButtons;

    [Header("옵션")]
    [Tooltip("버튼 클릭 후 시뮬레이션 시작 전 대기(초). 0 이면 즉시, -1 이면 TutorialDemoController 의 demoStartDelay 사용")]
    public float startDelay = 0f;

    [Tooltip("시뮬레이션이 끝나면 패드 패널을 다시 연다")]
    public bool reopenPadAfterDemo = true;

    [Tooltip("패드를 다시 열 때 활성화할 설명 코너 패널 (선택). 서브패널 네비게이션이 있으면 여기 지정")]
    public GameObject descriptionCornerPanel;

    [Header("디버그")]
    public bool showDebugInfo = false;

    /// <summary>시뮬레이션 재생 중인지</summary>
    public bool IsPlaying { get; private set; }

    void Start()
    {
        if (demoController == null)
            demoController = FindFirstObjectByType<TutorialDemoController>();
        if (padPanel == null)
            padPanel = FindFirstObjectByType<UIPanelToggle>();

        if (demoController == null)
            Debug.LogWarning("[PadToolSimulator] TutorialDemoController 를 찾을 수 없습니다. " +
                             "이 씬에 도구 데모 세팅이 된 TutorialDemoController 가 있어야 시뮬레이션이 재생됩니다.");

        if (toolButtons != null)
        {
            foreach (var tb in toolButtons)
            {
                if (tb == null || tb.button == null) continue;
                ToolDemoType captured = tb.demoType;
                tb.button.onClick.AddListener(() => PlaySimulation(captured));
            }
        }
    }

    void OnDestroy()
    {
        if (toolButtons != null)
            foreach (var tb in toolButtons)
                if (tb != null && tb.button != null)
                    tb.button.onClick.RemoveAllListeners();
    }

    // ─────────────────────────────────────────
    // 외부 호출 진입점
    // ─────────────────────────────────────────

    /// <summary>도구 시뮬레이션 재생.</summary>
    public void PlaySimulation(ToolDemoType type)
    {
        if (IsPlaying)
        {
            if (showDebugInfo) Debug.Log("[PadToolSimulator] 이미 재생 중 — 무시");
            return;
        }
        if (demoController == null)
        {
            Debug.LogWarning("[PadToolSimulator] demoController 가 없어 시뮬레이션을 재생할 수 없습니다.");
            return;
        }

        StartCoroutine(RunSimulation(type));
    }

    /// <summary>버튼 OnClick(string) 용. "Sponge", "Duster", "Flapper" 처럼 ToolDemoType 이름을 넘긴다.</summary>
    public void PlaySimulation(string demoTypeName)
    {
        if (System.Enum.TryParse(demoTypeName, true, out ToolDemoType type))
            PlaySimulation(type);
        else
            Debug.LogWarning($"[PadToolSimulator] '{demoTypeName}' 은 올바른 ToolDemoType 이름이 아닙니다.");
    }

    /// <summary>버튼 OnClick(int) 용. ToolDemoType enum 의 정수값.</summary>
    public void PlaySimulation(int demoTypeIndex)
    {
        PlaySimulation((ToolDemoType)demoTypeIndex);
    }

    // ─────────────────────────────────────────

    private IEnumerator RunSimulation(ToolDemoType type)
    {
        IsPlaying = true;
        SetButtonsInteractable(false);

        if (showDebugInfo) Debug.Log($"[PadToolSimulator] 시뮬레이션 시작: {type}");

        // 패드 닫기 (TutorialDemoController 도 내부에서 닫지만 toolSwitcher 미연결 대비 명시적으로)
        if (padPanel != null) padPanel.ClosePanel();

        // PlayDemo 는 코루틴을 반환 → 끝날 때까지 대기
        Coroutine demo = demoController.PlayDemo(type, startDelay);
        if (demo != null) yield return demo;

        // 패드 복구
        if (reopenPadAfterDemo && padPanel != null)
        {
            padPanel.OpenPanel();
            if (descriptionCornerPanel != null)
                descriptionCornerPanel.SetActive(true);
        }

        SetButtonsInteractable(true);
        IsPlaying = false;

        if (showDebugInfo) Debug.Log($"[PadToolSimulator] 시뮬레이션 종료: {type}");
    }

    // TutorialDemoController.SetPlayerInputEnabled 가 패드 버튼을 잠글 수 있으므로
    // 이 코너의 버튼은 직접 상태를 관리한다.
    private void SetButtonsInteractable(bool value)
    {
        if (toolButtons == null) return;
        foreach (var tb in toolButtons)
            if (tb != null && tb.button != null)
                tb.button.interactable = value;
    }
}
