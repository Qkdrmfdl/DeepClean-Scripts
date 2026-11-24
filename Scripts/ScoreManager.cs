using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 스테이지별 청소율 가중치 한 항목. 청소 대상 태그 하나와 그 기여도(%).
/// cleanableWeights 배열의 합이 100이 아니어도 내부에서 정규화됨.
/// </summary>
[System.Serializable]
public class CleanableWeight
{
    [Tooltip("청소 대상 태그. 예: Cleanable_Hand, Cleanable_Duster, Cleanable_Rag")]
    public string cleanableTag;

    [Tooltip("이 대상이 청소율에서 차지하는 비중(%). 배열 전체 합이 100이 되도록 맞추는 것을 권장")]
    [Range(0f, 100f)]
    public float weightPercent = 10f;
}

public class ScoreManager : MonoBehaviour
{
    [Header("VR 카메라")]
    public Transform vrCamera;
    public float distanceFromPlayer = 1.5f;

    [Header("실시간 UI")]
    public TextMeshProUGUI progressText;
    public Slider progressBar;

    [Header("Rank UI")]
    public GameObject rankPanel;
    public TextMeshProUGUI rankText;
    [Tooltip("랭크에 따라 달라지는 한 줄 코멘트 (예: S랭크 → \"청소의 달인이네요~~!\")")]
    public TextMeshProUGUI rankCommentText;
    public TextMeshProUGUI rankProgressText;
    public TextMeshProUGUI rankRewardText; // 보상 금액 표시용

    [Header("랭크 보상 설정")]
    [Tooltip("S 랭크 보상 (95% 이상)")]
    public int sRankReward = 500000;

    [Tooltip("A 랭크 보상 (85% 이상)")]
    public int aRankReward = 350000;

    [Tooltip("B 랭크 보상 (70% 이상)")]
    public int bRankReward = 200000;

    [Tooltip("C 랭크 보상 (50% 이상)")]
    public int cRankReward = 80000;

    [Tooltip("F 랭크 보상 (50% 미만)")]
    public int fRankReward = 10000;

    [Header("랭크별 코멘트")]
    [Tooltip("S 랭크 코멘트 (뒤 이미지 때문에 이 길이를 넘지 않는 것을 권장)")]
    public string sRankComment = "청소의 달인이네요~~!";

    [Tooltip("A 랭크 코멘트")]
    public string aRankComment = "꽤 잘하시네요!";

    [Tooltip("B 랭크 코멘트")]
    public string bRankComment = "조금 아쉬워요!";

    [Tooltip("C 랭크 코멘트")]
    public string cRankComment = "다시 도전해봐요!";

    [Tooltip("F 랭크 코멘트")]
    public string fRankComment = "청소가 필요해요!";

    [Header("청소율 가중치 (스테이지별)")]
    [Tooltip("이 스테이지에서 쓰는 청소 대상 태그와 비중(%)을 전부 등록.\n" +
             "각 스테이지 씬의 ScoreManager 마다 그 스테이지가 실제 사용하는 도구를 모두 넣어야 함.\n" +
             "(예: Stage1 = Hand·Sponge·Mop·Broom·Duster·Flapper)\n" +
             "배열 합이 100이 아니거나 없는 태그가 섞여도, 실제 존재하는 항목만으로 정규화됨.")]
    public CleanableWeight[] cleanableWeights;

    [Header("돈 애니메이션 설정")]
    [Tooltip("돈이 증가하는 애니메이션 시간 (초). StageFlowManager 없는 단독 테스트에서만 사용되는 폴백")]
    public float moneyAnimationDuration = 3f;
    // 카운트업 효과음은 MoneyManager.countUpClip 으로 이관됨 (다음 스테이지 씬 진입 시 재생)

    [Header("MoneyManager 참조")]
    public MoneyManager moneyManager;

    [Header("Rank UI 표시 시 비활성화")]
    [Tooltip("Rank UI를 표시할 때 비활성화할 ToolSwitcher")]
    public ToolSwitcher toolSwitcher;
    [Tooltip("Rank UI 표시 시 직접 비활성화할 Pad UI 오브젝트")]
    public GameObject padUIObject;
    [Tooltip("Rank UI 표시 시 숨길 실시간 HUD (CleaningPercent Canvas)")]
    public GameObject realtimeUICanvas;

    [Header("랭크 완료 나레이션")]
    [Tooltip("랭크 UI 표시 후 재생할 나레이션 클립 (스테이지별로 배열, 인덱스 0=Stage1)")]
    public AudioClip[] rankNarrationClips;
    [Tooltip("랭크 UI 표시 후 나레이션 재생 대기 시간 (초)")]
    public float rankNarrationDelay = 2f;
    [Tooltip("나레이션 전용 AudioSource (비워두면 자동 생성). 카운트업 효과음 소스와 분리됨")]
    public AudioSource narrationSfxSource;

    [Header("Rank UI 자동 숨기기 설정")]
    [Tooltip("Rank UI를 표시하고 난 후 자동으로 숨길 때까지의 시간 (0 = 자동 숨김 없음)")]
    public float autoHideDelay = 5f;

    [Header("타임어택 연동")]
    [Tooltip("같은 씬의 StageTimer. 비워두면 시간 보너스 없음")]
    public StageTimer stageTimer;
    [Tooltip("시간을 가득 남겼을 때 랭크 보상에 추가되는 최대 배율 (1.0 = 최대 2배)")]
    public float maxTimeBonusMultiplier = 1.0f;
    [Tooltip("타임 보너스 금액 표시용 텍스트 (선택)")]
    public TMPro.TextMeshProUGUI timeBonusText;
    [Tooltip("소요 시간 표시용 텍스트 (선택). stageTimer가 연결돼 있어야 값이 채워짐. MM:SS 숫자만 출력")]
    public TMPro.TextMeshProUGUI rankTimeText;

    [Header("Stage 3 전체 랭크 UI")]
    [Tooltip("Stage 3 완료 후 5초 뒤에 표시할 전체 랭크 패널")]
    public GameObject totalRankPanel;
    [Tooltip("전체 랭크 UI가 표시될 때까지의 대기 시간 (초)")]
    public float totalRankDelay = 5f;

    [Header("튜토리얼 연동")]
    [Tooltip("튜토리얼 씬에서만 연결. 비워두면 일반 스테이지로 동작")]
    public TutorialProgressTracker tutorialProgressTracker;
    [Tooltip("튜토리얼 씬(tutorialProgressTracker 연결됨)에서는 청소완료 결과를 최대치로 고정 — 랭크 S · 청소율 100% · S랭크 보상")]
    public bool forceMaxResultInTutorial = true;

    [Header("디버그")]
    public bool showDebugInfo = false;

    private bool isRankUIVisible = false;
    private Coroutine autoHideCoroutine;
    private bool hasReceivedReward = false;
    private bool isAnimatingMoney = false;

    // 청소율 계산용: cleanableWeights 각 태그의 시작 시 총개수
    private readonly Dictionary<string, int> _cleanableTotals = new();
    private bool _warnedNoWeights = false;

    // 성능 최적화: 진행률 캐시 + 갱신 주기 제한
    private float cachedProgress = 0f;
    private float progressUpdateTimer = 0f;
    private const float PROGRESS_UPDATE_INTERVAL = 0.3f; // 0.3초마다만 재계산
    private Image fillImage; // GetComponent 매 프레임 방지
    private string lastProgressText = ""; // 텍스트 변경 시에만 ForceMeshUpdate 호출

    void Start()
    {
        if (vrCamera == null)
            vrCamera = Camera.main.transform;

        // MoneyManager 자동 찾기
        if (moneyManager == null)
            moneyManager = FindFirstObjectByType<MoneyManager>();

        if (moneyManager == null)
            Debug.LogWarning("[ScoreManager] MoneyManager를 찾을 수 없습니다!");

        if (toolSwitcher == null)
            toolSwitcher = FindFirstObjectByType<ToolSwitcher>();

        // prefab에서 씬 오브젝트 참조 불가 문제: 청소완료 버튼 onClick 런타임 연결
        if (toolSwitcher != null && toolSwitcher.cleaningCompleteButton != null)
        {
            toolSwitcher.cleaningCompleteButton.onClick.RemoveListener(ShowRankUI);
            toolSwitcher.cleaningCompleteButton.onClick.AddListener(ShowRankUI);
            if (showDebugInfo)
                Debug.Log("[ScoreManager] 청소완료 버튼 onClick 자동 연결됨");
        }

        // 나레이션 전용 AudioSource 준비 (카운트업 효과음 소스와 분리)
        if (narrationSfxSource == null)
        {
            narrationSfxSource = gameObject.AddComponent<AudioSource>();
            narrationSfxSource.playOnAwake = false;
            narrationSfxSource.spatialBlend = 0f;
        }

        // fillRect Image 캐시 (매 프레임 GetComponent 방지)
        if (progressBar != null && progressBar.fillRect != null)
            fillImage = progressBar.fillRect.GetComponent<Image>();

        // Rank UI를 처음 비활성화
        if (rankPanel != null)
            rankPanel.SetActive(false);

        // 스테이지별 청소 대상 총개수 캐싱 (청소율 정규화용)
        CacheCleanableTotals();

        if (showDebugInfo)
        {
            string detail = "";
            foreach (var kv in _cleanableTotals) detail += $"{kv.Key}={kv.Value} ";
            if (_cleanableTotals.Count == 0) detail = "cleanableWeights 미설정!";
            Debug.Log($"[ScoreManager] 초기화 완료 - 청소 대상: {detail}");
        }
    }

    void Update()
    {
        // 진행률은 0.3초마다만 재계산 (FindGameObjectsWithTag가 비쌈)
        progressUpdateTimer -= Time.deltaTime;
        if (progressUpdateTimer <= 0f)
        {
            progressUpdateTimer = PROGRESS_UPDATE_INTERVAL;
            cachedProgress = CalculateTotalProgress();
        }

        if (progressText != null)
        {
            string newText = $"Cleaning Percent: {cachedProgress:F0}%";
            if (newText != lastProgressText)
            {
                lastProgressText = newText;
                progressText.text = newText;
                progressText.ForceMeshUpdate();
            }
        }

        if (progressBar != null)
        {
            progressBar.value = cachedProgress;
            if (fillImage != null)
                fillImage.color = GetProgressColor(cachedProgress);
        }

        if (isRankUIVisible)
            FollowPlayer(rankPanel);
    }

    void FollowPlayer(GameObject panel)
    {
        if (vrCamera == null || panel == null) return;

        // ToolSwitcher UI처럼 즉각 추적 (Lerp/Slerp 없이 직접 대입)
        panel.transform.position = vrCamera.position + vrCamera.forward * distanceFromPlayer;
        panel.transform.rotation = Quaternion.LookRotation(vrCamera.forward);
    }

    // Rank UI 표시 (청소 완료 버튼 또는 StageTimer 만료 시 호출)
    public void ShowRankUI()
    {
        // 중복 호출 방지 (타이머 만료 + 버튼 동시 발생 케이스)
        if (isRankUIVisible) return;

        // 타이머 정지 (버튼을 직접 눌렀을 때)
        if (stageTimer != null && stageTimer.IsRunning)
            stageTimer.StopTimer();

        tutorialProgressTracker?.OnCleaningButtonPressed();

        if (rankPanel == null) return;

        float progress = ResolveProgress();
        string rank = ResolveRank(progress);
        int reward = GetRankReward(rank);

        if (rankText != null)
            rankText.text = rank;

        if (rankCommentText != null)
        {
            rankCommentText.text = GetRankComment(rank);
            rankCommentText.ForceMeshUpdate();
        }

        if (rankProgressText != null)
        {
            rankProgressText.text = $"{progress:F0}%";
            rankProgressText.ForceMeshUpdate();
        }

        // 보상 금액 표시
        if (rankRewardText != null)
        {
            rankRewardText.text = $"{reward:N0}원";
            rankRewardText.ForceMeshUpdate();
        }

        // 소요 시간 표시
        if (rankTimeText != null && stageTimer != null)
        {
            rankTimeText.text = StageTimer.FormatTime(stageTimer.ElapsedSeconds);
            rankTimeText.ForceMeshUpdate();
        }

        // 날아오는 효과 방지: SetActive 전 눈 앞에 바로 스냅
        if (vrCamera != null && rankPanel != null)
        {
            Vector3 snapPos = vrCamera.position + vrCamera.forward * distanceFromPlayer;
            rankPanel.transform.position = snapPos;
            rankPanel.transform.rotation = Quaternion.LookRotation(vrCamera.forward);
        }

        // ToolSwitcher + Pad UI 비활성화
        if (padUIObject != null)
            padUIObject.SetActive(false);

        // 실시간 HUD(CleaningPercent Canvas) 숨기기
        if (realtimeUICanvas != null)
            realtimeUICanvas.SetActive(false);

        // 씬마다 padUIObject 연결이 제각각이라(Stage2/3 누락·불일치) UIPanelToggle로도 확실히 닫는다.
        // 이미 padUIObject.SetActive(false)로 꺼진 오브젝트는 검색에서 제외되므로 중복 처리 안 됨.
        foreach (var padToggle in FindObjectsByType<UIPanelToggle>(FindObjectsSortMode.None))
            padToggle.ClosePanel();

        if (toolSwitcher != null)
            toolSwitcher.gameObject.SetActive(false);

        isRankUIVisible = true;
        rankPanel.SetActive(true);

        // Rank UI가 뜨기 전까지 잠겨있던 차 클릭을 여기서 풀어준다.
        foreach (var car in FindObjectsByType<CarClickHandler>(FindObjectsSortMode.None))
            car.Unlock();

        // 자동숨기기 코루틴
        if (autoHideCoroutine != null)
            StopCoroutine(autoHideCoroutine);
        if (autoHideDelay > 0f)
            autoHideCoroutine = StartCoroutine(AutoHideAfterDelay(autoHideDelay));

        // 자동으로 보상 지급 (애니메이션 포함)
        if (!hasReceivedReward)
            GiveRankReward();

        if (rankNarrationClips != null && rankNarrationClips.Length > 0)
            StartCoroutine(PlayNarrationAfterDelay(rankNarrationDelay));

        // Stage 3이면 전체 랭크 UI 표시 예약
        int currentStage = StageFlowManager.Instance != null ? StageFlowManager.Instance.CurrentStageIndex : -1;
        if (currentStage == 2 && totalRankPanel != null)
            StartCoroutine(ShowTotalRankAfterDelay(totalRankDelay));

        if (showDebugInfo)
            Debug.Log($"[Rank UI 표시] 청소율 {progress:F0}% / 랭크 {rank} / 보상 {reward:N0}원");
    }

    public void HideRankUI()
    {
        if (rankPanel == null) return;

        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
            autoHideCoroutine = null;
        }

        isRankUIVisible = false;
        rankPanel.SetActive(false);

        // 실시간 HUD(CleaningPercent Canvas) 다시 표시
        if (realtimeUICanvas != null)
            realtimeUICanvas.SetActive(true);

        if (showDebugInfo)
            Debug.Log("[Rank UI 닫기]");
    }

    private IEnumerator ShowTotalRankAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (totalRankPanel == null) yield break;

        // 눈 앞에 바로 스냅
        if (vrCamera != null)
        {
            totalRankPanel.transform.position = vrCamera.position + vrCamera.forward * distanceFromPlayer;
            totalRankPanel.transform.rotation = Quaternion.LookRotation(vrCamera.forward);
        }

        totalRankPanel.SetActive(true);

        if (showDebugInfo)
            Debug.Log("[ScoreManager] 전체 랭크 UI 표시");
    }

    private IEnumerator AutoHideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideRankUI();

        if (showDebugInfo)
            Debug.Log($"[ScoreManager] Rank UI {delay}초 후 자동숨김");
    }

    public void ToggleRankUI()
    {
        if (isRankUIVisible) HideRankUI();
        else ShowRankUI();
    }

    /// <summary>
    /// 랭크 보상 지급 (타임 보너스 배율 포함, 애니메이션 포함)
    /// </summary>
    public void GiveRankReward()
    {
        if (hasReceivedReward || isAnimatingMoney)
        {
            if (showDebugInfo)
                Debug.Log("[ScoreManager] 이미 보상을 받았거나 애니메이션 중입니다!");
            return;
        }

        if (moneyManager == null)
        {
            Debug.LogError("[ScoreManager] MoneyManager가 없어 보상을 지급할 수 없습니다!");
            return;
        }

        float progress = ResolveProgress();
        string rank    = ResolveRank(progress);
        int baseReward = GetRankReward(rank);

        // 타임 보너스 배율 계산 (1.0 ~ 1.0 + maxTimeBonusMultiplier)
        // 튜토리얼은 최대치 고정 → 타임 보너스도 최대
        float timeRatio      = ForceMax ? 1f : (stageTimer != null ? stageTimer.RemainingRatio : 0f);
        float bonusMultiplier = 1f + timeRatio * maxTimeBonusMultiplier;
        int finalReward      = Mathf.RoundToInt(baseReward * bonusMultiplier);
        int timeBonus        = finalReward - baseReward;

        // 타임 보너스 텍스트 표시
        if (timeBonusText != null)
        {
            timeBonusText.text = timeBonus > 0
                ? $"Time Bonus: +{timeBonus:N0}원 (x{bonusMultiplier:F2})"
                : "Time Bonus: 없음 (시간 초과)";
            timeBonusText.ForceMeshUpdate();
        }

        // 랭크 보상 텍스트 갱신 (최종 금액으로)
        if (rankRewardText != null)
        {
            rankRewardText.text = $"{finalReward:N0}원";
            rankRewardText.ForceMeshUpdate();
        }

        // 보상은 지속 지갑에 즉시 입금하고, 카운트업 연출은 다음 스테이지 씬 진입 시로 미룬다.
        // 튜토리얼은 결과가 남으면 안 되므로 지갑에 반영하지 않고 그 자리 연출만 한다.
        // (StageFlowManager 가 없는 단독 씬 테스트도 폴백으로 그 자리 카운트업)
        if (!IsTutorial && StageFlowManager.Instance != null)
            StageFlowManager.Instance.GrantReward(finalReward);
        else
            StartCoroutine(AnimateMoneyIncrease(finalReward));

        hasReceivedReward = true;

        // 스테이지별 결과를 씬 간 유지되는 StageFlowManager 에 기록 (아웃트로 전체 결과창용).
        // 튜토리얼은 실제 스테이지가 아니므로 제외.
        if (!IsTutorial && StageFlowManager.Instance != null)
        {
            int stageIndex = StageFlowManager.Instance.CurrentStageIndex;
            float elapsed  = stageTimer != null ? stageTimer.ElapsedSeconds : 0f;
            int cleaned    = GetCleanedItemCount(out int totalItems);
            StageFlowManager.Instance.RecordStageResult(stageIndex, rank, progress, finalReward, elapsed, cleaned, totalItems);
        }

        if (showDebugInfo)
            Debug.Log($"[ScoreManager] 보상 지급 — 랭크: {rank} | 기본: {baseReward:N0}원 | " +
                      $"시간 잔여: {timeRatio:P0} | 배율: x{bonusMultiplier:F2} | 최종: {finalReward:N0}원");
    }

    /// <summary>
    /// 돈 증가를 애니메이션하는 코루틴
    /// </summary>
    IEnumerator AnimateMoneyIncrease(int rewardAmount)
    {
        isAnimatingMoney = true;

        int startMoney = moneyManager.GetCurrentMoney();
        int targetMoney = startMoney + rewardAmount;
        float elapsedTime = 0f;

        if (showDebugInfo)
            Debug.Log($"[Money Animation] 시작: {startMoney:N0}원 → 목표: {targetMoney:N0}원");

        while (elapsedTime < moneyAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / moneyAnimationDuration;

            // EaseOutCubic (처음엔 빠르게 나중엔 느리게)
            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            // 현재 값 계산
            int currentDisplayMoney = Mathf.RoundToInt(Mathf.Lerp(startMoney, targetMoney, smoothT));

            // MoneyManager에 현재 금액 반영 (UpdateMoneyUI 호출)
            moneyManager.SetMoneyWithoutAnimation(currentDisplayMoney);

            yield return null;
        }

        // 최종 값 정확히 설정
        moneyManager.SetMoneyWithoutAnimation(targetMoney);

        isAnimatingMoney = false;

        if (showDebugInfo)
            Debug.Log($"[Money Animation] 완료! 최종 금액: {targetMoney:N0}원");
    }

    /// <summary>
    /// 랭크에 따른 보상 금액 반환
    /// </summary>
    int GetRankReward(string rank)
    {
        switch (rank)
        {
            case "S": return sRankReward;
            case "A": return aRankReward;
            case "B": return bRankReward;
            case "C": return cRankReward;
            case "F": return fRankReward;
            default:  return 0;
        }
    }

    /// <summary>
    /// 랭크에 따른 코멘트 문구 반환
    /// </summary>
    string GetRankComment(string rank)
    {
        switch (rank)
        {
            case "S": return sRankComment;
            case "A": return aRankComment;
            case "B": return bRankComment;
            case "C": return cRankComment;
            case "F": return fRankComment;
            default:  return "";
        }
    }

    /// <summary>
    /// cleanableWeights 에 등록된 각 청소 대상의 완료율을 가중 평균해 0~100 청소율을 낸다.
    /// 실제로 존재하는(시작 시 총개수 > 0) 항목의 가중치만으로 정규화하므로,
    /// 배열 합이 100이 아니거나 일부 태그가 이 스테이지에 없어도 최댓값은 항상 100이다.
    /// </summary>
    float CalculateTotalProgress()
    {
        if (cleanableWeights == null || cleanableWeights.Length == 0)
        {
            if (!_warnedNoWeights)
            {
                Debug.LogWarning("[ScoreManager] cleanableWeights 가 비어 있습니다. " +
                                 "이 스테이지 씬의 ScoreManager 인스펙터에서 청소 대상 태그/비중을 설정하세요.");
                _warnedNoWeights = true;
            }
            return 0f;
        }

        float weightedSum = 0f;  // Σ(완료율 × weight)
        float totalWeight = 0f;  // Σ(weight) — 실제 존재하는 항목만

        foreach (var w in cleanableWeights)
        {
            if (w == null || string.IsNullOrEmpty(w.cleanableTag) || w.weightPercent <= 0f)
                continue;

            if (!_cleanableTotals.TryGetValue(w.cleanableTag, out int total) || total <= 0)
                continue;   // 이 스테이지에 없는 대상 → 정규화에서 제외

            int remaining = SafeCountTag(w.cleanableTag);
            float doneRate = Mathf.Clamp01(1f - (float)remaining / total);

            weightedSum += doneRate * w.weightPercent;
            totalWeight += w.weightPercent;
        }

        return totalWeight > 0f ? (weightedSum / totalWeight) * 100f : 0f;
    }

    /// <summary>cleanableWeights 각 태그의 시작 시 총개수를 캐싱.</summary>
    void CacheCleanableTotals()
    {
        _cleanableTotals.Clear();
        if (cleanableWeights == null) return;

        foreach (var w in cleanableWeights)
        {
            if (w == null || string.IsNullOrEmpty(w.cleanableTag) || _cleanableTotals.ContainsKey(w.cleanableTag))
                continue;
            _cleanableTotals[w.cleanableTag] = SafeCountTag(w.cleanableTag);
        }
    }

    /// <summary>
    /// 시작 시점 대비 지금까지 청소한 오염 아이템 개수. totalItems 로 전체 개수도 반환.
    /// cleanableWeights 에 등록되고 시작 시 실제로 존재했던 태그들만 집계한다.
    /// </summary>
    public int GetCleanedItemCount(out int totalItems)
    {
        int total = 0, remaining = 0;
        foreach (var kv in _cleanableTotals)
        {
            total     += kv.Value;
            remaining += SafeCountTag(kv.Key);
        }
        totalItems = total;
        return Mathf.Clamp(total - remaining, 0, total);
    }

    /// <summary>존재하지 않는 태그로 호출해도 예외 대신 0 반환 + 경고.</summary>
    static int SafeCountTag(string tag)
    {
        try
        {
            return GameObject.FindGameObjectsWithTag(tag).Length;
        }
        catch (UnityException)
        {
            Debug.LogWarning($"[ScoreManager] 태그 '{tag}' 가 프로젝트에 없습니다. 청소율 계산에서 제외됩니다.");
            return 0;
        }
    }

    string GetRank(float progress)
    {
        if (progress >= 95f) return "S";
        else if (progress >= 85f) return "A";
        else if (progress >= 70f) return "B";
        else if (progress >= 50f) return "C";
        else return "F";
    }

    // 튜토리얼 씬에서는 결과를 최대치로 고정, 그 외에는 실제 청소율 기준
    bool IsTutorial => tutorialProgressTracker != null;
    bool ForceMax   => IsTutorial && forceMaxResultInTutorial;

    float ResolveProgress()          => ForceMax ? 100f : CalculateTotalProgress();
    string ResolveRank(float progress) => ForceMax ? "S"  : GetRank(progress);

    Color GetProgressColor(float progress)
    {
        if (progress >= 85f) return Color.green;
        else if (progress >= 50f) return Color.cyan;
        else return Color.gray;
    }

    public float GetCurrentProgress() => CalculateTotalProgress();
    public string GetCurrentRank()    => ResolveRank(CalculateTotalProgress());
    public int    GetCurrentReward()  => GetRankReward(GetCurrentRank());

    private IEnumerator PlayNarrationAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        int stageIndex = StageFlowManager.Instance != null ? StageFlowManager.Instance.CurrentStageIndex : 0;
        if (stageIndex < 0) stageIndex = 0;

        if (rankNarrationClips == null || stageIndex >= rankNarrationClips.Length) yield break;
        AudioClip clip = rankNarrationClips[stageIndex];
        if (clip == null) yield break;

        if (narrationSfxSource == null)
        {
            narrationSfxSource = gameObject.AddComponent<AudioSource>();
            narrationSfxSource.playOnAwake = false;
            narrationSfxSource.spatialBlend = 0f;
        }
        narrationSfxSource.PlayOneShot(clip);

        if (showDebugInfo)
            Debug.Log($"[ScoreManager] 랭크 나레이션 재생 (스테이지번호 {stageIndex + 1})");
    }

    /// <summary>
    /// 보상 받기 초기화 (새 게임 시작 시)
    /// </summary>
    public void ResetReward()
    {
        hasReceivedReward = false;

        if (showDebugInfo)
            Debug.Log("[ScoreManager] 보상 받기 초기화");
    }
}
