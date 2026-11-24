using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [System.Serializable]
    public class ToolInfo
    {
        [Tooltip("도구 태그 (ToolSwitcher의 toolTags와 동일하게. 맨손은 \"Hand\", 도구 없음은 \"None\")")]
        public string toolTag;

        [Tooltip("화면에 표시할 한글 이름 (예: 스펀지, 대걸레)")]
        public string koreanName;

        [TextArea(2, 4)]
        [Tooltip("이름 아래에 표시할 도구 설명")]
        public string description;
    }

    [Header("청소율 UI")]
    public Slider cleanSlider;
    public TextMeshProUGUI cleanPercentText;

    [Header("현재 도구 표시 UI")]
    public TextMeshProUGUI currentToolText;
    [Tooltip("도구 이름 아래에 표시할 설명 텍스트 (선택)")]
    public TextMeshProUGUI currentToolDescriptionText;
    [Tooltip("도구 이름 앞에 붙일 라벨. 비워두면 이름만 표시됨")]
    public string toolNameLabel = "도구: ";

    [Header("도구 한글 이름 / 설명")]
    [Tooltip("도구 태그별 한글 이름과 설명. 목록에 없는 태그는 태그 원문이 그대로 표시됨")]
    public ToolInfo[] toolInfos = new ToolInfo[]
    {
        new ToolInfo { toolTag = "None",        koreanName = "없음",       description = "" },
        new ToolInfo { toolTag = "Hand",        koreanName = "맨손",       description = "손으로 직접 집어 치우는 쓰레기를 처리합니다." },
        new ToolInfo { toolTag = "Sponge",      koreanName = "스펀지",     description = "물때와 얼룩을 문질러 닦아냅니다." },
        new ToolInfo { toolTag = "Mop",         koreanName = "대걸레",     description = "바닥의 넓은 오염을 밀어서 닦습니다." },
        new ToolInfo { toolTag = "Broom",       koreanName = "빗자루",     description = "바닥에 흩어진 먼지와 부스러기를 쓸어 담습니다." },
        new ToolInfo { toolTag = "Duster",      koreanName = "먼지떨이",   description = "선반과 가구 위의 먼지를 털어냅니다." },
        new ToolInfo { toolTag = "Flapper",     koreanName = "유리 밀대",  description = "유리와 거울에 남은 물기를 밀어서 제거합니다." },
        new ToolInfo { toolTag = "Vacuum",      koreanName = "진공청소기", description = "카펫과 틈새의 먼지를 빨아들입니다." },
        new ToolInfo { toolTag = "Scraper",     koreanName = "스크래퍼",   description = "바닥에 눌어붙은 껌과 이물질을 긁어냅니다." },
        new ToolInfo { toolTag = "ToiletBrush", koreanName = "변기솔",     description = "변기 안쪽의 찌든 때를 문질러 청소합니다." },
    };

    [Header("연동")]
    public ToolSwitcher toolSwitcher;
    public ScoreManager scoreManager;

    // 매 프레임 텍스트 갱신 방지: 도구가 바뀔 때만 반영
    private string lastToolTag = null;

    void Start()
    {
        if (toolSwitcher == null)
            toolSwitcher = FindFirstObjectByType<ToolSwitcher>();
        if (scoreManager == null)
            scoreManager = FindFirstObjectByType<ScoreManager>();

        if (cleanSlider != null)
            cleanSlider.value = 0;

        if (cleanPercentText != null)
            cleanPercentText.text = "0%";

        ApplyToolDisplay("None");
    }

    void Update()
    {
        UpdateCleanUI();
        UpdateToolName();
    }

    // 실시간 청소율 업데이트
    void UpdateCleanUI()
    {
        if (scoreManager == null)
            return;

        float progress = scoreManager.GetCurrentProgress() / 100f;

        if (cleanSlider != null)
            cleanSlider.value = progress;

        if (cleanPercentText != null)
            cleanPercentText.text = Mathf.RoundToInt(progress * 100f) + "%";
    }

    // 현재 도구 이름/설명 업데이트
    void UpdateToolName()
    {
        if (toolSwitcher == null || currentToolText == null)
            return;

        string tool = toolSwitcher.GetCurrentToolTag();

        if (string.IsNullOrEmpty(tool) || tool == "none")
            tool = toolSwitcher.IsHandModeActive ? "Hand" : "None";
        else if (tool == "Mop1" || tool == "Mop2")
            tool = "Mop";

        if (tool == lastToolTag)
            return;
        lastToolTag = tool;

        ApplyToolDisplay(tool);
    }

    // 도구 태그에 해당하는 한글 이름/설명을 UI에 반영. 매핑이 없으면 태그 원문 사용.
    void ApplyToolDisplay(string tag)
    {
        ToolInfo info = null;
        if (toolInfos != null)
        {
            foreach (var t in toolInfos)
            {
                if (t != null && t.toolTag == tag) { info = t; break; }
            }
        }

        string displayName = (info != null && !string.IsNullOrEmpty(info.koreanName)) ? info.koreanName : tag;

        if (currentToolText != null)
            currentToolText.text = toolNameLabel + displayName;

        if (currentToolDescriptionText != null)
            currentToolDescriptionText.text = info != null ? info.description : "";
    }
}
