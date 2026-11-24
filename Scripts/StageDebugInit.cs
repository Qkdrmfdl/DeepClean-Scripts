#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 에디터 전용 디버그 헬퍼.
/// Street씬 또는 Stage씬에 빈 GameObject에 달아두면
/// StageFlowManager 없이 직접 Play해도 원하는 상태로 시작할 수 있습니다.
/// </summary>
public class StageDebugInit : MonoBehaviour
{
    [Header("테스트할 Street 방문 인덱스")]
    [Tooltip("0 = 튜토리얼 후 첫 방문 (→ Stage1)\n1 = Stage1 클리어 후 (→ Stage2)\n2 = Stage2 클리어 후 (→ Stage3)")]
    public int streetVisitIndex = 0;

    [Header("씬 이름 (StageFlowManager가 없을 때 사용)")]
    public string[] stageSceneNames = { "Stage1", "Stage2", "Stage3" };
    public string streetSceneName = "StreetScene";
    public string tutorialSceneName = "Tutorial";

    [Header("방문별 필요 도구 태그")]
    [Tooltip("StageFlowManager의 toolRequirements와 동일하게 설정하세요.")]
    public StageToolRequirement[] toolRequirements;

    void Awake()
    {
        if (StageFlowManager.Instance != null)
        {
            StageFlowManager.Instance.SetDebugState(streetVisitIndex - 1, streetVisitIndex);
            Debug.Log($"[StageDebugInit] 기존 StageFlowManager 상태 덮어씀 ─ StreetVisit:{streetVisitIndex}");
            return;
        }

        GameObject go = new GameObject("[DEBUG] StageFlowManager");
        StageFlowManager sfm = go.AddComponent<StageFlowManager>();

        sfm.stageSceneNames   = stageSceneNames;
        sfm.streetSceneName   = streetSceneName;
        sfm.tutorialSceneName = tutorialSceneName;
        sfm.toolRequirements  = toolRequirements;

        // CurrentStageIndex = streetVisitIndex - 1
        // (GoToNextStage에서 +1 하므로 한 칸 뒤로)
        sfm.SetDebugState(streetVisitIndex - 1, streetVisitIndex);

        Debug.Log($"[StageDebugInit] StageFlowManager 생성 ─ StreetVisit:{streetVisitIndex}");
    }
}
#endif
