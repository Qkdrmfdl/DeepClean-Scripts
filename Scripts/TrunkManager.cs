using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class TrunkManager : MonoBehaviour
{
    [Header("트렁크 설정")]
    public Animator trunkAnimator;
    public string openAnimationName = "TrunkOpen";

    [Header("선택할 청소도구 오브젝트")]
    public GameObject[] toolObjects; // 스펀지, 빗자루, 대걸레 오브젝트

    [Header("하이라이트 설정")]
    public Color highlightColor = Color.yellow;
    public float blinkSpeed = 2f;

    [Header("UI")]
    public Button openTrunkButton;
    public GameObject allSelectedUI; // 다 골랐을 때 표시할 UI

    [Header("연동")]
    public ToolSwitcher toolSwitcher;
    public ShopManager shopManager;

    [Header("디버그")]
    public bool showDebugInfo = true;

    private bool isTrunkOpen = false;
    private List<GameObject> selectedTools = new List<GameObject>();
    private Dictionary<GameObject, Renderer[]> toolRenderers = new Dictionary<GameObject, Renderer[]>();
    private Dictionary<GameObject, Coroutine> blinkCoroutines = new Dictionary<GameObject, Coroutine>();

    void Start()
    {
        if (toolSwitcher == null)
            toolSwitcher = FindFirstObjectByType<ToolSwitcher>();
        if (shopManager == null)
            shopManager = FindFirstObjectByType<ShopManager>();

        // 트렁크 열기 버튼 연결
        if (openTrunkButton != null)
            openTrunkButton.onClick.AddListener(OpenTrunk);

        // 처음엔 도구 숨기기
        foreach (var tool in toolObjects)
        {
            if (tool != null)
            {
                tool.SetActive(false);
                toolRenderers[tool] = tool.GetComponentsInChildren<Renderer>();
            }
        }

        if (allSelectedUI != null)
            allSelectedUI.SetActive(false);
    }

    public void OpenTrunk()
    {
        if (isTrunkOpen) return;
        isTrunkOpen = true;

        // 트렁크 애니메이션
        if (trunkAnimator != null)
            trunkAnimator.Play(openAnimationName);

        // 버튼 비활성화
        if (openTrunkButton != null)
            openTrunkButton.interactable = false;

        // 잠시 후 도구 표시 + 하이라이트
        StartCoroutine(ShowToolsAfterDelay(1.0f));

        if (showDebugInfo)
            Debug.Log("트렁크 열림!");
    }

    IEnumerator ShowToolsAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        foreach (var tool in toolObjects)
        {
            if (tool != null)
            {
                tool.SetActive(true);

                // 콜라이더에 클릭 이벤트 추가
                var clickHandler = tool.GetComponent<ToolClickHandler>();
                if (clickHandler == null)
                    clickHandler = tool.AddComponent<ToolClickHandler>();

                clickHandler.Initialize(this, tool);

                // 하이라이트 시작
                blinkCoroutines[tool] = StartCoroutine(BlinkHighlight(tool));
            }
        }
    }

    IEnumerator BlinkHighlight(GameObject tool)
    {
        if (!toolRenderers.ContainsKey(tool)) yield break;

        Renderer[] renderers = toolRenderers[tool];
        Dictionary<Renderer, Color[]> originalColors = new Dictionary<Renderer, Color[]>();

        // 원본 색상 저장
        foreach (var r in renderers)
        {
            Color[] colors = new Color[r.materials.Length];
            for (int i = 0; i < r.materials.Length; i++)
                colors[i] = r.materials[i].color;
            originalColors[r] = colors;
        }

        while (true)
        {
            float t = (Mathf.Sin(Time.time * blinkSpeed) + 1f) / 2f;

            foreach (var r in renderers)
            {
                if (originalColors.ContainsKey(r))
                {
                    for (int i = 0; i < r.materials.Length; i++)
                    {
                        Color blended = Color.Lerp(originalColors[r][i], highlightColor, t * 0.5f);
                        r.materials[i].color = blended;
                    }
                }
            }

            yield return null;
        }
    }

    public void OnToolSelected(GameObject tool)
    {
        if (selectedTools.Contains(tool)) return;

        // 하이라이트 중지
        if (blinkCoroutines.ContainsKey(tool))
        {
            StopCoroutine(blinkCoroutines[tool]);
            blinkCoroutines.Remove(tool);
        }

        selectedTools.Add(tool);

        // ToolSwitcher에 이미지 등록
        RegisterToolToSwitcher(tool);

        // 도구 오브젝트 숨기기
        tool.SetActive(false);

        if (showDebugInfo)
            Debug.Log($"도구 선택: {tool.name} ({selectedTools.Count}/{toolObjects.Length})");

        // 모두 선택했는지 확인
        if (selectedTools.Count >= toolObjects.Length)
            OnAllToolsSelected();
    }

    void RegisterToolToSwitcher(GameObject tool)
    {
        if (toolSwitcher == null) return;

        // 도구 태그로 ToolSwitcher 버튼 찾아서 활성화
        string tag = tool.tag;

        for (int i = 0; i < toolSwitcher.toolTags.Length; i++)
        {
            if (toolSwitcher.toolTags[i] == tag)
            {
                if (i < toolSwitcher.toolButtons.Length && toolSwitcher.toolButtons[i] != null)
                {
                    toolSwitcher.toolButtons[i].interactable = true;

                    if (showDebugInfo)
                        Debug.Log($"ToolSwitcher 버튼 활성화: {tag}");
                }
                break;
            }
        }
    }

    public bool AllToolsSelected()
    {
        return selectedTools.Count >= toolObjects.Length;
    }

    void OnAllToolsSelected()
    {
        if (showDebugInfo)
            Debug.Log("모든 도구 선택 완료!");

        // 이동 안내 UI 표시
        if (allSelectedUI != null)
            allSelectedUI.SetActive(true);
    }
}