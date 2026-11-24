using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 전체 스테이지 흐름을 관리하는 싱글톤 매니저.
///
/// 전환 순서:
///   인트로(스트릿 역할) → 튜토리얼 → StreetScene → Stage1
///   → StreetScene → Stage2 → StreetScene → Stage3 → 아웃트로
///
/// 사용 방법:
///   - 씬 어디서든 StageFlowManager.Instance 로 접근
///   - 인트로 도구 선택 완료 후 : StageFlowManager.Instance.GoToTutorial() 호출
///   - 튜토리얼 완료 후 : StageFlowManager.Instance.GoToStreet() 호출
///   - Stage 클리어 후 : StageFlowManager.Instance.GoToStreet() 호출
///   - Street씬 도구 선택 완료 후 문 통과 시 : StageFlowManager.Instance.GoToNextStage() 호출
/// </summary>
public class StageFlowManager : MonoBehaviour
{
    public static StageFlowManager Instance { get; private set; }

    // ═══════════════════════════════════════════════════
    //  Inspector 설정
    // ═══════════════════════════════════════════════════

    [Header("씬 이름 설정")]
    [Tooltip("튜토리얼 씬 이름")]
    public string tutorialSceneName = "Tutorial";

    [Tooltip("Street(차 내부) 씬 이름")]
    public string streetSceneName = "Street";

    [Tooltip("각 스테이지 씬 이름 (순서대로: Stage1, Stage3)")]
    public string[] stageSceneNames = { "Stage1", "Stage3" };

    [Tooltip("마지막 스테이지 클리어 후 이동할 아웃트로 씬 이름")]
    public string outroSceneName = "Outro";

    [Header("스테이지별 필요 청소도구")]
    [Tooltip(
        "StreetScene 방문 횟수별로 선택해야 할 도구 태그 목록\n" +
        "• index 0 : 튜토리얼 후 첫 번째 Street 방문 → Stage1 진입\n" +
        "• index 1 : Stage1 클리어 후 두 번째 Street 방문 → Stage2 진입\n" +
        "• index 2 : Stage2 클리어 후 세 번째 Street 방문 → Stage3 진입\n" +
        "※ 인트로 씬 도구 선택은 TrunkInteractionController.overrideRequiredTags 로 설정\n" +
        "툴 태그 예시: Sponge / Mop / Broom"
    )]
    public StageToolRequirement[] toolRequirements;

    [Header("페이드 전환")]
    [Tooltip("씬 전환 전 대기 시간 (초). 페이드 UI가 있을 경우 맞춰서 조정하세요.")]
    public float transitionDelay = 0.5f;

    [Header("지갑 (씬 간 유지)")]
    [Tooltip("게임 시작 시 초기 소지금")]
    public int startingMoney = 0;

    [Header("디버그")]
    public bool showDebugInfo = false;

    // ═══════════════════════════════════════════════════
    //  런타임 상태 (읽기 전용 프로퍼티)
    // ═══════════════════════════════════════════════════

    /// <summary>현재 진행 중인 스테이지 인덱스 (0-based). -1=아직 스테이지 시작 전(인트로), 0=Stage1, 1=Stage2, 2=Stage3</summary>
    public int CurrentStageIndex { get; private set; } = -1;

    /// <summary>
    /// 몇 번째 StreetScene 방문인지 (0-based).
    /// 0 = 튜토리얼 후 첫 방문 (Stage1 진입용 도구 선택)
    /// 1 = Stage1 클리어 후 두 번째 방문 (Stage2 진입용 도구 선택)
    /// 2 = Stage2 클리어 후 세 번째 방문 (Stage3 진입용 도구 선택)
    /// ※ 인트로 방문은 카운트하지 않음 (overrideRequiredTags로 별도 처리)
    /// </summary>
    public int StreetVisitIndex { get; private set; } = 0;

    /// <summary>Street씬에서 현재까지 선택 완료된 도구 태그 목록</summary>
    public List<string> SelectedToolTags { get; private set; } = new List<string>();

    /// <summary>현재 Street 방문에서 필요한 모든 도구가 선택됐는지 여부</summary>
    public bool AllToolsSelected { get; private set; } = false;

    /// <summary>현재 소지금 (씬 간 유지). 청소 보상 + 랭크 보상이 모두 반영된 값.</summary>
    public int Money { get; private set; }

    /// <summary>
    /// 아직 카운트업 연출로 보여주지 않은 랭크 보상 누적액.
    /// Money 에는 이미 포함돼 있으며, 연출(또는 생략) 후 0으로 비워짐.
    /// </summary>
    public int PendingReward { get; private set; }

    /// <summary>
    /// 스테이지별 청소 결과 (랭크/청소율/보상/소요시간). 씬을 넘어 유지되어
    /// 아웃트로의 전체 결과창(TotalResultSequencer)에서 읽어 표시한다.
    /// 인덱스가 아니라 stageIndex 필드로 구분하며, 같은 스테이지를 다시 기록하면 덮어쓴다.
    /// </summary>
    public List<StageResult> StageResults { get; private set; } = new List<StageResult>();

    private bool walletInitialized = false;

    // ═══════════════════════════════════════════════════
    //  이벤트
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 필요한 모든 도구가 선택됐을 때 발생.
    /// DoorEntryTrigger 등에서 구독하여 문 활성화 처리.
    /// </summary>
    public System.Action OnAllToolsSelectedEvent;

    /// <summary>소지금이 바뀔 때 새 잔액을 통지. (구독: MoneyManager / ShopManager 등)</summary>
    public System.Action<int> OnMoneyChanged;

    // ═══════════════════════════════════════════════════
    //  Unity 생명주기
    // ═══════════════════════════════════════════════════

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (!walletInitialized)
            {
                Money = Mathf.Max(0, startingMoney);
                PendingReward = 0;
                walletInitialized = true;
            }

            Log($"초기화 완료 ─ DontDestroyOnLoad 적용됨 (소지금 {Money:N0})");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 현재는 씬 로드 후 별도 처리 없음.
        // 각 Stage 씬의 ToolSwitcher는 씬에 미리 세팅된 도구를 그대로 사용.
    }

    // ═══════════════════════════════════════════════════
    //  씬 전환 공개 메서드
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 인트로에서 도구 선택 완료 후 튜토리얼 씬으로 이동.
    /// AllToolsSelected=true 상태를 유지하므로 이후 StreetScene 진입 시 문이 즉시 활성화됨.
    /// </summary>
    public void GoToTutorial()
    {
        Log($"튜토리얼로 이동 ─ 선택된 도구: [{string.Join(", ", SelectedToolTags)}]");
        StartCoroutine(LoadSceneWithDelay(tutorialSceneName));
    }

    /// <summary>
    /// 현재 스테이지 클리어 후 Street씬으로 이동.
    /// (예: Stage1의 DoorExitTrigger 또는 CarEntryTrigger에서 호출)
    /// </summary>
    public void GoToStreet()
    {
        // Street 진입 전 도구 선택 상태 초기화
        AllToolsSelected = false;
        SelectedToolTags.Clear();

        Log($"Street씬으로 이동 ─ 방문 횟수: {StreetVisitIndex + 1}번째, 필요 도구: [{string.Join(", ", GetRequiredToolTags())}]");
        StartCoroutine(LoadSceneWithDelay(streetSceneName));
    }

    /// <summary>
    /// 마지막 스테이지(Stage3) 클리어 후 아웃트로 씬으로 이동.
    /// (Stage3 씬의 차 오브젝트 CarClickHandler 에서 goToOutroInsteadOfStreet=true 로 호출)
    /// </summary>
    public void GoToOutro()
    {
        AllToolsSelected = false;
        SelectedToolTags.Clear();

        Log($"아웃트로 씬으로 이동 ─ {outroSceneName} (기록된 스테이지 결과 {StageResults.Count}개)");
        StartCoroutine(LoadSceneWithDelay(outroSceneName));
    }

    /// <summary>
    /// Street씬에서 문 통과 시 다음 스테이지 씬으로 이동.
    /// AllToolsSelected == false 이면 이동을 차단함.
    /// (DoorEntryTrigger에서 호출)
    /// </summary>
    public void GoToNextStage()
    {
        if (!AllToolsSelected)
        {
            Log("⚠ 아직 모든 도구를 선택하지 않았습니다. 이동이 차단됩니다.");
            return;
        }

        // CurrentStageIndex: -1(인트로) → 0(Stage1) → 1(Stage2) → 2(Stage3)
        int nextStageIndex = CurrentStageIndex + 1;

        if (nextStageIndex >= stageSceneNames.Length)
        {
            Log("⚠ 더 이상 진행할 스테이지가 없습니다. (마지막 스테이지 완료)");
            return;
        }

        // 상태 전진 (StreetVisitIndex는 GoToStreet() 이후 다음 방문을 위해 여기서 증가)
        CurrentStageIndex = nextStageIndex;
        StreetVisitIndex++;

        string nextScene = stageSceneNames[CurrentStageIndex];
        Log($"다음 스테이지로 이동 ─ {nextScene} (스테이지 {CurrentStageIndex + 1})");
        StartCoroutine(LoadSceneWithDelay(nextScene));
    }

    // ═══════════════════════════════════════════════════
    //  도구 선택 관련 공개 메서드
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 현재 Street 방문에서 선택해야 할 도구 태그 배열 반환.
    /// TrunkInteractionController가 이 목록을 참조해서 하이라이트/잠금 처리.
    /// </summary>
    public string[] GetRequiredToolTags()
    {
        if (toolRequirements == null || toolRequirements.Length == 0)
        {
            Debug.LogWarning("[StageFlowManager] toolRequirements가 비어있습니다. Inspector에서 설정하세요!");
            return new string[0];
        }

        if (StreetVisitIndex >= toolRequirements.Length)
        {
            Debug.LogWarning($"[StageFlowManager] StreetVisitIndex({StreetVisitIndex})가 toolRequirements 범위를 초과했습니다!");
            return new string[0];
        }

        return toolRequirements[StreetVisitIndex].requiredToolTags ?? new string[0];
    }

    /// <summary>
    /// 특정 도구 태그가 현재 방문에서 선택해야 하는 도구인지 확인.
    /// TrunkInteractionController에서 도구별 하이라이트 여부 결정 시 사용.
    /// </summary>
    public bool IsRequiredTool(string toolTag)
    {
        foreach (string tag in GetRequiredToolTags())
        {
            if (tag == toolTag) return true;
        }
        return false;
    }

    /// <summary>
    /// 해당 도구가 이미 선택됐는지 확인.
    /// TrunkInteractionController에서 중복 선택 방지 시 사용.
    /// </summary>
    public bool IsToolAlreadySelected(string toolTag)
    {
        return SelectedToolTags.Contains(toolTag);
    }

    /// <summary>
    /// 도구 하나가 선택됐을 때 TrunkInteractionController에서 호출.
    /// 필요한 도구를 모두 선택하면 OnAllToolsSelectedEvent를 발생시킴.
    /// </summary>
    public void RegisterSelectedTool(string toolTag)
    {
        if (!IsRequiredTool(toolTag))
        {
            Log($"⚠ '{toolTag}'는 현재 방문에서 필요한 도구가 아닙니다. 무시합니다.");
            return;
        }

        if (SelectedToolTags.Contains(toolTag))
        {
            Log($"'{toolTag}'는 이미 선택된 도구입니다.");
            return;
        }

        SelectedToolTags.Add(toolTag);
        Log($"도구 선택됨: {toolTag} ({SelectedToolTags.Count} / {GetRequiredToolTags().Length})");

        CheckAllToolsSelected();
    }

    // ═══════════════════════════════════════════════════
    //  지갑 (씬 간 유지)
    // ═══════════════════════════════════════════════════

    /// <summary>청소 보상 등 즉시 반영되는 수입/차감. (음수 허용, 0 미만으로는 안 내려감)</summary>
    public void AddMoney(int amount)
    {
        if (amount == 0) return;
        Money = Mathf.Max(0, Money + amount);
        OnMoneyChanged?.Invoke(Money);
        Log($"소지금 {(amount >= 0 ? "+" : "")}{amount:N0} → {Money:N0}");
    }

    /// <summary>상점 등에서 소비. 잔액이 부족하면 false 반환하고 아무것도 안 함.</summary>
    public bool SpendMoney(int amount)
    {
        if (amount <= 0 || Money < amount) return false;
        Money -= amount;
        OnMoneyChanged?.Invoke(Money);
        Log($"소지금 -{amount:N0} (소비) → {Money:N0}");
        return true;
    }

    /// <summary>소지금을 특정 값으로 설정. (치트 / 초기화 / 카운트업 도중 스냅 용)</summary>
    public void SetMoney(int amount)
    {
        Money = Mathf.Max(0, amount);
        OnMoneyChanged?.Invoke(Money);
    }

    /// <summary>
    /// 랭크 보상 지급. 잔액에는 즉시 더하되, 카운트업 연출용으로 PendingReward 에도 누적한다.
    /// (연출은 다음 스테이지 씬 진입 시 MoneyManager 가 CountUpOnStart 로 재생)
    /// </summary>
    public void GrantReward(int amount)
    {
        if (amount <= 0) return;
        Money += amount;
        PendingReward += amount;
        OnMoneyChanged?.Invoke(Money);
        Log($"랭크 보상 +{amount:N0} 예약 (소지금 {Money:N0}, 대기 연출 {PendingReward:N0})");
    }

    /// <summary>PendingReward 를 0으로 비우고 그 값을 반환한다. (연출 완료 또는 생략 시)</summary>
    public int ConsumePendingReward()
    {
        int r = PendingReward;
        PendingReward = 0;
        return r;
    }

    // ═══════════════════════════════════════════════════
    //  스테이지별 결과 (씬 간 유지)
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 한 스테이지의 청소 결과를 기록한다. (ScoreManager.GiveRankReward 에서 호출)
    /// 같은 stageIndex 가 이미 있으면 덮어쓴다.
    /// </summary>
    public void RecordStageResult(int stageIndex, string rank, float cleanPercent, int reward, float elapsedSeconds,
                                  int cleanedCount = 0, int totalCount = 0)
    {
        if (stageIndex < 0)
        {
            Log($"⚠ RecordStageResult 무시 — 잘못된 stageIndex({stageIndex})");
            return;
        }

        var result = new StageResult
        {
            stageIndex     = stageIndex,
            rank           = rank,
            cleanPercent   = cleanPercent,
            reward         = reward,
            elapsedSeconds = elapsedSeconds,
            cleanedCount   = cleanedCount,
            totalCount     = totalCount,
        };

        int existing = StageResults.FindIndex(r => r.stageIndex == stageIndex);
        if (existing >= 0)
            StageResults[existing] = result;
        else
            StageResults.Add(result);

        StageResults.Sort((a, b) => a.stageIndex.CompareTo(b.stageIndex));

        Log($"스테이지 {stageIndex + 1} 결과 기록 — 랭크 {rank} / 청소율 {cleanPercent:F0}% / " +
            $"보상 {reward:N0}원 / 소요 {elapsedSeconds:F0}초 (누적 {StageResults.Count}개)");
    }

    /// <summary>stageIndex 에 해당하는 결과를 반환. 없으면 null.</summary>
    public StageResult GetStageResult(int stageIndex)
    {
        return StageResults.Find(r => r.stageIndex == stageIndex);
    }

    /// <summary>기록된 스테이지들의 청소율 평균 (0~100). 기록이 없으면 0.</summary>
    public float GetAverageCleanPercent()
    {
        if (StageResults.Count == 0) return 0f;
        float sum = 0f;
        foreach (var r in StageResults) sum += r.cleanPercent;
        return sum / StageResults.Count;
    }

    /// <summary>기록된 스테이지 보상의 총합.</summary>
    public int GetTotalStageReward()
    {
        int sum = 0;
        foreach (var r in StageResults) sum += r.reward;
        return sum;
    }

    /// <summary>기록된 스테이지들의 소요 시간 합계 (초).</summary>
    public float GetTotalElapsedSeconds()
    {
        float sum = 0f;
        foreach (var r in StageResults) sum += r.elapsedSeconds;
        return sum;
    }

    /// <summary>기록된 스테이지들의 청소한 오염 개수 합계. totalItems 로 전체 개수 합계도 반환.</summary>
    public int GetTotalCleanedCount(out int totalItems)
    {
        int cleaned = 0; totalItems = 0;
        foreach (var r in StageResults) { cleaned += r.cleanedCount; totalItems += r.totalCount; }
        return cleaned;
    }

    /// <summary>청소율 평균 기준 종합 랭크 (S/A/B/C/F). ScoreManager 의 기준과 동일.</summary>
    public string GetOverallRank()
    {
        float avg = GetAverageCleanPercent();
        if (avg >= 95f) return "S";
        if (avg >= 85f) return "A";
        if (avg >= 70f) return "B";
        if (avg >= 50f) return "C";
        return "F";
    }

    /// <summary>결과 기록 초기화. (새 게임 시작 시)</summary>
    public void ResetResults()
    {
        StageResults.Clear();
        Log("스테이지 결과 기록 초기화");
    }

    // ═══════════════════════════════════════════════════
    //  Private 내부 메서드
    // ═══════════════════════════════════════════════════

    /// <summary>필요한 도구를 모두 선택했는지 검사. 완료 시 이벤트 발생.</summary>
    private void CheckAllToolsSelected()
    {
        string[] required = GetRequiredToolTags();
        if (required.Length == 0) return;

        foreach (string tag in required)
        {
            if (!SelectedToolTags.Contains(tag))
                return; // 아직 선택 안 된 도구가 있음
        }

        AllToolsSelected = true;
        Log("✅ 모든 도구 선택 완료! → OnAllToolsSelectedEvent 발생");
        OnAllToolsSelectedEvent?.Invoke();
    }

    /// <summary>일정 딜레이 후 씬 로드. 비동기 로드로 VR 컴포지터를 계속 먹여 전환 중 머리 추적이 멈추지 않게 한다.</summary>
    private IEnumerator LoadSceneWithDelay(string sceneName)
    {
        if (transitionDelay > 0f)
            yield return new WaitForSeconds(transitionDelay);

        yield return SceneManager.LoadSceneAsync(sceneName);
    }

    private void Log(string msg)
    {
        if (showDebugInfo)
            Debug.Log($"[StageFlowManager] {msg}");
    }

    // ═══════════════════════════════════════════════════
    //  디버그 전용
    // ═══════════════════════════════════════════════════

    /// <summary>현재 상태를 한 줄 요약. Inspector 혹은 로그에서 확인용.</summary>
    public string GetStatusSummary()
    {
        string stageLabel = CurrentStageIndex < 0 ? "인트로" : $"Stage{CurrentStageIndex + 1}";
        return $"CurrentStage: {stageLabel} | StreetVisit: {StreetVisitIndex} | " +
               $"선택됨: [{string.Join(", ", SelectedToolTags)}] | 완료: {AllToolsSelected} | " +
               $"소지금: {Money:N0} (대기 연출: {PendingReward:N0})";
    }

#if UNITY_EDITOR
    [ContextMenu("현재 상태 출력 (Debug)")]
    private void PrintStatus() => Debug.Log(GetStatusSummary());

    [ContextMenu("지갑 리셋 (startingMoney 로)")]
    private void DebugResetWallet()
    {
        Money = Mathf.Max(0, startingMoney);
        PendingReward = 0;
        OnMoneyChanged?.Invoke(Money);
        ResetResults();
        Debug.Log($"[StageFlowManager] 지갑 리셋 → {Money:N0}");
    }

    [ContextMenu("소지금 +100,000 (테스트)")]
    private void DebugAddMoney() => AddMoney(100000);

    public void SetDebugState(int currentStageIndex, int streetVisitIndex)
    {
        CurrentStageIndex = currentStageIndex;
        StreetVisitIndex  = streetVisitIndex;
        AllToolsSelected  = false;
        SelectedToolTags.Clear();
        Log($"[DEBUG] 상태 강제 설정 ─ CurrentStage:{currentStageIndex}, StreetVisit:{streetVisitIndex}");
    }
#endif
}

// ═══════════════════════════════════════════════════════
//  보조 데이터 클래스 (Inspector 직렬화용)
// ═══════════════════════════════════════════════════════

/// <summary>
/// 각 Street 방문에서 선택해야 할 도구 태그 목록.
/// Inspector에서 배열 원소로 편집 가능.
/// </summary>
[System.Serializable]
public class StageToolRequirement
{
    [Tooltip("이 Street 방문에서 선택해야 할 도구 태그들.\n예) Sponge, Mop, Broom")]
    public string[] requiredToolTags;
}

/// <summary>
/// 한 스테이지의 청소 결과. StageFlowManager 가 씬을 넘어 보관하며,
/// 아웃트로의 전체 결과창에서 스테이지별 텍스트를 채우는 데 사용한다.
/// </summary>
[System.Serializable]
public class StageResult
{
    [Tooltip("0 = Stage1, 1 = Stage2, 2 = Stage3")]
    public int stageIndex;

    [Tooltip("랭크 문자 (S / A / B / C / F)")]
    public string rank;

    [Tooltip("청소율 (0~100)")]
    public float cleanPercent;

    [Tooltip("이 스테이지에서 받은 최종 보상액 (타임 보너스 포함)")]
    public int reward;

    [Tooltip("청소 완료까지 걸린 시간 (초)")]
    public float elapsedSeconds;

    [Tooltip("청소한 오염 아이템 개수")]
    public int cleanedCount;

    [Tooltip("이 스테이지의 오염 아이템 총 개수")]
    public int totalCount;
}
