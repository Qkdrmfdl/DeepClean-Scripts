using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionTrigger : MonoBehaviour
{
    [Header("씬 전환 설정")]
    public string targetSceneName = "TutorialRoom";

    [Tooltip("체크하면 targetSceneName 대신 StageFlowManager.GoToNextStage()를 호출합니다.\n" +
             "StreetScene의 문에 사용하세요.")]
    public bool useStageFlowManager = false;

    [Header("전환 조건")]
    public TrunkManager trunkManager; // 도구 선택 완료 후에만 전환

    [Header("UI")]
    public GameObject transitionUI; // "다음 장소로 이동 중..." 등

    [Header("디버그")]
    public bool showDebugInfo = true;

    private bool isTransitioning = false;

    void Start()
    {
        if (trunkManager == null)
            trunkManager = FindFirstObjectByType<TrunkManager>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (isTransitioning) return;
        if (!other.CompareTag("Player")) return;

        if (useStageFlowManager)
        {
            if (StageFlowManager.Instance == null)
            {
                Debug.LogError("[SceneTransitionTrigger] StageFlowManager.Instance가 null입니다!");
                return;
            }
            if (!StageFlowManager.Instance.AllToolsSelected)
            {
                if (showDebugInfo)
                    Debug.Log("[SceneTransitionTrigger] 아직 모든 도구를 선택하지 않았습니다!");
                return;
            }
            isTransitioning = true;
            StageFlowManager.Instance.GoToNextStage();
            return;
        }

        // 도구 선택 완료 전에 전환 안 함
        if (trunkManager != null && !trunkManager.AllToolsSelected())
        {
            if (showDebugInfo)
                Debug.Log("아직 트렁크에서 도구를 선택하지 않았습니다!");
            return;
        }

        StartCoroutine(TransitionToScene());
    }

    System.Collections.IEnumerator TransitionToScene()
    {
        isTransitioning = true;

        if (showDebugInfo)
            Debug.Log($"씬 전환: {targetSceneName}");

        if (transitionUI != null)
            transitionUI.SetActive(true);

        yield return new WaitForSeconds(1.5f);

        SceneManager.LoadSceneAsync(targetSceneName);
    }
}
