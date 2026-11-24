using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UINavigationManager : MonoBehaviour
{
    [Header("패널 설정")]
    public GameObject menuPanel;
    public GameObject shopPanel;

    [Header("관리할 모든 패널 등록")]
    public List<GameObject> allPanels = new List<GameObject>();

    [Header("디버그")]
    public bool showDebugInfo = true;

    private Stack<GameObject> panelHistory = new Stack<GameObject>();
    private GameObject currentPanel;

    void Start()
    {
        // 시작할 때 allPanels 전부 끄기 (menuPanel 제외)
        HideAllPanels();

        if (menuPanel != null)
        {
            currentPanel = menuPanel;
            currentPanel.SetActive(true);

            if (showDebugInfo)
                Debug.Log($"시작 패널: {menuPanel.name}");
        }
        else
        {
            Debug.LogError("Menu Panel이 설정되지 않았습니다!");
        }
    }

    private void HideAllPanels()
    {
        foreach (GameObject panel in allPanels)
        {
            if (panel != null)
                panel.SetActive(false);
        }
    }

    // ─── OnClick 연결용 함수들 ───────────────────
    public void ShowShop()
    {
        NavigateToPanel(shopPanel);
    }

    public void ShowMenu()
    {
        GoToHome();
    }
    // ─────────────────────────────────────────────

    public void NavigateToPanel(GameObject newPanel)
    {
        if (newPanel == null || currentPanel == newPanel) return;

        if (currentPanel != null)
        {
            panelHistory.Push(currentPanel);
            currentPanel.SetActive(false);
        }

        currentPanel = newPanel;
        currentPanel.SetActive(true);

        if (showDebugInfo)
            Debug.Log($"패널 이동: {currentPanel.name} (히스토리: {panelHistory.Count})");
    }

    public void GoBack()
    {
        if (currentPanel == menuPanel || panelHistory.Count == 0) return;

        if (currentPanel != null)
            currentPanel.SetActive(false);

        currentPanel = panelHistory.Pop();
        currentPanel.SetActive(true);

        if (showDebugInfo)
            Debug.Log($"뒤로가기: {currentPanel.name}");
    }

    public void GoToHome()
    {
        if (menuPanel == null || currentPanel == menuPanel) return;

        if (currentPanel != null)
            currentPanel.SetActive(false);

        panelHistory.Clear();

        // HideAllPanels 제거 - menuPanel만 켜기
        currentPanel = menuPanel;
        currentPanel.SetActive(true);

        if (showDebugInfo)
            Debug.Log($"홈으로 이동: {menuPanel.name}");
    }

    public bool CanGoBack() => currentPanel != menuPanel && panelHistory.Count > 0;
    public string GetCurrentPanelName() => currentPanel != null ? currentPanel.name : "없음";
}