using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 도구를 처음 선택할 때 TutorialDemoController 데모를 한 번 실행합니다.
/// 튜토리얼에서 이미 데모한 도구(Sponge, Broom, Mop)는 자동으로 건너뜁니다.
/// </summary>
public class FirstUseDemoTrigger : MonoBehaviour
{
    [Header("참조")]
    public ToolSwitcher toolSwitcher;
    public TutorialDemoController demoController;
    public ShopManager shopManager;

    [Header("저장 방식")]
    [Tooltip("ON: 게임 재시작해도 데모 기록 유지 (PlayerPrefs)\nOFF: 이번 세션만 기록 (테스트 시 편리)")]
    public bool persistAcrossSessions = true;
    [Tooltip("PlayerPrefs 키 접두사 (persistAcrossSessions=ON 일 때만 사용)")]
    public string prefsPrefix = "DemoShown_";

    [Header("튜토리얼에서 이미 데모한 도구 태그 (건너뜀)")]
    public string[] tutorialCoveredTags = { "Sponge", "Broom", "Mop" };

    [Header("디버그")]
    public bool showDebugInfo = true;

    // toolTag → ToolDemoType 매핑
    private static readonly Dictionary<string, ToolDemoType> TagToDemoType = new Dictionary<string, ToolDemoType>
    {
        { "Duster",      ToolDemoType.Duster      },
        { "Flapper",     ToolDemoType.Flapper     },
        { "Spray",       ToolDemoType.Spray       },
        { "Vacuum",      ToolDemoType.Vacuum      },
        { "Mop2",        ToolDemoType.Mop2        },
        { "TileBrush",   ToolDemoType.TileBrush   },
        { "Scraper",     ToolDemoType.Scraper     },
        { "ToiletBrush", ToolDemoType.ToiletBrush },
    };

    // 세션 내 메모리 기록 (persistAcrossSessions=OFF 일 때 사용)
    private readonly HashSet<string> shownThisSession = new HashSet<string>();

    void Start()
    {
        if (toolSwitcher == null)
            toolSwitcher = FindFirstObjectByType<ToolSwitcher>();

        if (demoController == null)
            demoController = FindFirstObjectByType<TutorialDemoController>();

        if (shopManager == null)
            shopManager = FindFirstObjectByType<ShopManager>();

        if (toolSwitcher == null)
        {
            Debug.LogError("[FirstUseDemoTrigger] ToolSwitcher를 찾을 수 없습니다! 씬에 있는지 확인하세요.");
            return;
        }

        if (demoController == null)
            Debug.LogWarning("[FirstUseDemoTrigger] TutorialDemoController를 찾을 수 없습니다. 데모가 실행되지 않습니다.");

        // 튜토리얼 커버 도구를 완료 처리
        foreach (string tag in tutorialCoveredTags)
            MarkShown(tag);

        toolSwitcher.OnToolSelected += OnToolSelected;

        if (showDebugInfo)
            Debug.Log($"[FirstUseDemoTrigger] 초기화 완료. 데모 대상 도구: {string.Join(", ", TagToDemoType.Keys)}");
    }

    void OnDestroy()
    {
        if (toolSwitcher != null)
            toolSwitcher.OnToolSelected -= OnToolSelected;
    }

    void OnToolSelected(int toolIndex, string toolTag)
    {
        if (showDebugInfo)
            Debug.Log($"[FirstUseDemoTrigger] 도구 선택 감지: '{toolTag}' (index {toolIndex})");

        if (demoController == null)
        {
            Debug.LogWarning("[FirstUseDemoTrigger] TutorialDemoController가 없어 데모를 실행할 수 없습니다.");
            return;
        }

        // Sponge가 Spray로 업그레이드된 경우, 효과 태그를 Spray로 변환
        string effectiveTag = toolTag;
        if (toolTag == "Sponge" && shopManager != null && shopManager.IsSprayPurchased())
            effectiveTag = "Spray";

        if (!TagToDemoType.TryGetValue(effectiveTag, out ToolDemoType demoType))
        {
            if (showDebugInfo)
                Debug.Log($"[FirstUseDemoTrigger] '{effectiveTag}'는 데모 대상이 아닙니다.");
            return;
        }

        if (IsAlreadyShown(effectiveTag))
        {
            if (showDebugInfo)
                Debug.Log($"[FirstUseDemoTrigger] '{effectiveTag}' 데모는 이미 표시됨 — 건너뜀.");
            return;
        }

        MarkShown(effectiveTag);
        demoController.PlayDemo(demoType, 0f);

        Debug.Log($"[FirstUseDemoTrigger] 첫 사용 데모 실행: {effectiveTag} ({demoType})");
    }

    bool IsAlreadyShown(string toolTag)
    {
        if (shownThisSession.Contains(toolTag)) return true;
        if (persistAcrossSessions && PlayerPrefs.GetInt(prefsPrefix + toolTag, 0) == 1) return true;
        return false;
    }

    void MarkShown(string toolTag)
    {
        shownThisSession.Add(toolTag);
        if (persistAcrossSessions)
            PlayerPrefs.SetInt(prefsPrefix + toolTag, 1);
    }

    /// <summary>
    /// 모든 첫 사용 기록을 초기화합니다 (테스트·디버그용).
    /// </summary>
    [ContextMenu("모든 데모 기록 초기화")]
    public void ResetAllDemoRecords()
    {
        shownThisSession.Clear();

        foreach (string tag in TagToDemoType.Keys)
            PlayerPrefs.DeleteKey(prefsPrefix + tag);

        // 튜토리얼 커버 도구는 다시 완료 처리
        foreach (string tag in tutorialCoveredTags)
            MarkShown(tag);

        Debug.Log("[FirstUseDemoTrigger] 데모 기록 초기화 완료");
    }
}
