using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

/// <summary>씬 진입 시 대기 중인 랭크 보상(StageFlowManager.PendingReward) 처리 방식.</summary>
public enum PendingRewardHandling
{
    Absorb,         // 조용히 흡수 (연출 없음)
    CountUpOnStart, // HUD 가 뜨면서 카운트업 재생
    Manual,         // 외부에서 PlayPendingRewardCountUp() 호출할 때까지 대기
}

/// <summary>
/// 씬 안의 Cash HUD 표시 + 청소 보상 입금 담당.
/// 실제 잔액은 <see cref="StageFlowManager"/> 의 지갑(Money)에 저장되어 씬 간 유지된다.
/// StageFlowManager 가 없으면(스테이지 씬을 직접 Play) 로컬 값으로 폴백 동작한다.
/// </summary>
public class MoneyManager : MonoBehaviour
{
    [Header("VR 카메라")]
    public Transform vrCamera;
    public float distanceFromPlayer = 1.5f;

    [Header("Money UI")]
    public GameObject moneyUIPanel;
    public TextMeshProUGUI moneyText;

    [Header("청소 보상 설정")]
    [Tooltip("손 — 쓰레기 줍기")]
    public int trashReward = 1000;

    [Tooltip("스펀지 — 물 얼룩")]
    public int waterReward = 1500;

    [Tooltip("대걸레 — 바닥 얼룩")]
    public int floorReward = 2000;

    [Tooltip("빗자루 — 먼지 쓸기")]
    public int dustReward = 1500;

    [Tooltip("파리채 — 파리 잡기 (최고 보상)")]
    public int flyReward = 5000;

    [Tooltip("스크래퍼 — 껌 제거")]
    public int gumReward = 3000;

    [Tooltip("먼지털이 — 먼지 털기")]
    public int dusterReward = 2500;

    [Tooltip("변기솔 — 변기 얼룩")]
    public int toiletReward = 3000;

    [Tooltip("화장실타일솔 — 타일 청소")]
    public int tileReward = 2000;

    [Tooltip("손걸레 — 닦기")]
    public int ragReward = 1500;

    [Header("사운드")]
    public AudioSource sfxSource;
    public AudioClip rewardClip;

    [Header("지속 지갑 연동")]
    [Tooltip("체크 시 지속 지갑(StageFlowManager)을 무시하고 이 씬 로컬 값만 사용한다.\n" +
             "→ 청소 보상·랭크 보상이 씬을 넘어가지 않음. 튜토리얼처럼 소지금이 남으면 안 되는 씬용.\n" +
             "(씬에 TutorialProgressTracker 가 있으면 체크 안 해도 자동으로 로컬 전용이 된다)")]
    public bool ignorePersistentWallet = false;

    [Header("랭크 보상 카운트업 연출")]
    [Tooltip("이 씬에 진입했을 때 StageFlowManager 에 예약된 랭크 보상(PendingReward)을 어떻게 처리할지:\n" +
             "• Absorb          : 조용히 흡수 (연출 없음). 튜토리얼 / 상점 등 기본값\n" +
             "• CountUpOnStart : HUD 가 뜨면서 카운트업 애니메이션 재생. 다음 스테이지 씬 권장\n" +
             "• Manual          : 외부에서 PlayPendingRewardCountUp() 을 호출할 때까지 대기")]
    public PendingRewardHandling pendingRewardHandling = PendingRewardHandling.Absorb;
    [Tooltip("카운트업 시작 전 대기 (초). HUD 페이드인 / 씬 로드 직후 텀")]
    public float countUpStartDelay = 0.5f;
    [Tooltip("카운트업 애니메이션 시간 (초)")]
    public float countUpDuration = 3f;
    [Tooltip("카운트업 동안 반복 재생할 효과음 (비워두면 무음)")]
    public AudioClip countUpClip;

    [Header("디버그")]
    public bool showDebugInfo = true;

    // StageFlowManager 가 없을 때만 사용하는 폴백 저장소
    private int localMoney = 0;
    private int totalTrash, totalWater, totalFloor, totalDust, totalFly, totalDuster;
    private Coroutine countUpCoroutine;
    private bool subscribed = false;
    private bool walletDisabled = false;   // Start 에서 확정 (ignorePersistentWallet + 튜토리얼 자동감지)

    private static StageFlowManager Flow => StageFlowManager.Instance;
    private bool HasFlow => !walletDisabled && Flow != null;

    void Start()
    {
        if (vrCamera == null && Camera.main != null)
            vrCamera = Camera.main.transform;

        if (sfxSource == null)
            sfxSource = GetComponent<AudioSource>();

        // 튜토리얼 씬에서는 소지금이 남으면 안 되므로 지속 지갑을 끊고 로컬 전용으로 동작
        walletDisabled = ignorePersistentWallet
                         || FindFirstObjectByType<TutorialProgressTracker>() != null;

        // Money UI 활성화
        if (moneyUIPanel != null)
            moneyUIPanel.SetActive(true);

        // 각 오브젝트 수 세기
        totalTrash = GameObject.FindGameObjectsWithTag("Cleanable_Hand").Length;
        totalWater = GameObject.FindGameObjectsWithTag("Cleanable_Sponge").Length;
        totalFloor = GameObject.FindGameObjectsWithTag("Cleanable_Mop").Length;
        totalDust = GameObject.FindGameObjectsWithTag("Cleanable_Broom").Length;
        totalFly = GameObject.FindGameObjectsWithTag("Cleanable_Flapper").Length;
        totalDuster = GameObject.FindGameObjectsWithTag("Cleanable_Duster").Length;

        bool startCountUp = false;

        if (HasFlow)
        {
            if (!subscribed)
            {
                Flow.OnMoneyChanged += OnFlowMoneyChanged;
                subscribed = true;
            }

            // 대기 중인 랭크 보상 처리 (잔액엔 이미 반영돼 있음 — PendingReward 는 연출 표식일 뿐)
            if (Flow.PendingReward > 0)
            {
                switch (pendingRewardHandling)
                {
                    case PendingRewardHandling.Absorb:
                        Flow.ConsumePendingReward();
                        break;
                    case PendingRewardHandling.CountUpOnStart:
                        startCountUp = true;
                        break;
                    case PendingRewardHandling.Manual:
                        break; // 외부 호출 대기
                }
            }
        }

        RefreshDisplay();

        if (startCountUp)
            PlayPendingRewardCountUp();

        if (showDebugInfo)
            Debug.Log($"[MoneyManager] 초기화 완료 - 소지금:{CurrentMoney:N0} " +
                      $"(지갑:{(HasFlow ? "StageFlowManager" : "로컬 폴백")}) / " +
                      $"쓰레기:{totalTrash}, 얼룩:{totalWater}, 바닥:{totalFloor}, 먼지:{totalDust}, 파리:{totalFly}, 먼지털이:{totalDuster}");
    }

    void OnDestroy()
    {
        if (subscribed && Flow != null)
        {
            Flow.OnMoneyChanged -= OnFlowMoneyChanged;
            subscribed = false;
        }
    }

    void Update()
    {
        // Money UI 플레이어 따라다니기
        FollowPlayer(moneyUIPanel);
    }

    void FollowPlayer(GameObject panel)
    {
        if (vrCamera == null || panel == null) return;

        // ToolSwitcher UI처럼 즉각 추적 (Lerp/Slerp 없이 직접 대입)
        panel.transform.position = vrCamera.position + vrCamera.forward * distanceFromPlayer;
        panel.transform.rotation = Quaternion.LookRotation(vrCamera.forward);
    }

    // ─────────────────────────────────────────────
    //  잔액 접근
    // ─────────────────────────────────────────────

    /// <summary>실제 잔액 (연출 대기분 포함).</summary>
    public int CurrentMoney => HasFlow ? Flow.Money : localMoney;

    /// <summary>HUD 에 표시할 값. 연출(카운트업) 대기 중인 보상은 뺀 값으로 보여준다.</summary>
    private int DisplayMoney
    {
        get
        {
            if (!HasFlow) return localMoney;
            int m = Flow.Money;
            // 아직 연출로 안 보여준 보상은 표시에서 뺀다 (곧 카운트업으로 채워짐)
            if (pendingRewardHandling != PendingRewardHandling.Absorb) m -= Flow.PendingReward;
            return Mathf.Max(0, m);
        }
    }

    private void OnFlowMoneyChanged(int _)
    {
        // 카운트업 중에는 코루틴이 직접 텍스트를 갱신하므로 건드리지 않는다.
        if (countUpCoroutine == null)
            RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (moneyText == null) return;
        moneyText.text = $"Cash: {DisplayMoney:N0}";
        moneyText.ForceMeshUpdate();
    }

    // ─────────────────────────────────────────────
    //  청소 완료 후 호출되는 함수들
    // ─────────────────────────────────────────────

    public void OnTrashCleaned()       => AddMoney(trashReward,  "쓰레기");
    public void OnWaterStainCleaned()  => AddMoney(waterReward,  "물 얼룩");
    public void OnFloorStainCleaned()  => AddMoney(floorReward,  "바닥 얼룩");
    public void OnDustCleaned()        => AddMoney(dustReward,   "먼지");
    public void OnFlyCleaned()         => AddMoney(flyReward,    "파리");
    public void OnGumCleaned()         => AddMoney(gumReward,    "껌");
    public void OnDusterCleaned()      => AddMoney(dusterReward, "먼지털이");
    public void OnToiletStainCleaned() => AddMoney(toiletReward, "변기 얼룩");
    public void OnRagCleaned()         => AddMoney(ragReward,    "손걸레 닦기");

    void AddMoney(int amount, string cleanType)
    {
        if (HasFlow)
            Flow.AddMoney(amount);          // → OnMoneyChanged → RefreshDisplay
        else
        {
            localMoney += amount;
            RefreshDisplay();
        }

        if (sfxSource != null && rewardClip != null)
            sfxSource.PlayOneShot(rewardClip);

        if (showDebugInfo)
            Debug.Log($"[MoneyManager] {cleanType} 청소 완료! +{amount}원 (총 {CurrentMoney}원)");
    }

    // ─────────────────────────────────────────────
    //  외부 API (상점 / 치트 / 애니메이션)
    // ─────────────────────────────────────────────

    /// <summary>현재 잔액 조회.</summary>
    public int GetCurrentMoney() => CurrentMoney;

    /// <summary>돈 소비 (상점 등). 잔액 부족 시 false.</summary>
    public bool SpendMoney(int amount)
    {
        bool ok;
        if (HasFlow)
            ok = Flow.SpendMoney(amount);
        else
        {
            ok = localMoney >= amount;
            if (ok) { localMoney -= amount; RefreshDisplay(); }
        }

        if (showDebugInfo)
            Debug.Log(ok
                ? $"[MoneyManager] {amount}원 소비 (남은 돈: {CurrentMoney}원)"
                : $"[MoneyManager] 돈이 부족합니다! (필요: {amount}원, 현재: {CurrentMoney}원)");

        return ok;
    }

    /// <summary>돈 추가 (치트나 보너스).</summary>
    public void AddMoneyDirect(int amount)
    {
        if (HasFlow) Flow.AddMoney(amount);
        else { localMoney += amount; RefreshDisplay(); }
    }

    /// <summary>돈 초기화.</summary>
    public void ResetMoney()
    {
        if (HasFlow) Flow.SetMoney(0);
        else { localMoney = 0; RefreshDisplay(); }

        if (showDebugInfo)
            Debug.Log("[MoneyManager] 돈 초기화");
    }

    /// <summary>애니메이션에서 - UI 를 조용히 특정 값으로 맞춤 (폴백 경로의 ScoreManager 카운트업용).</summary>
    public void SetMoneyWithoutAnimation(int amount)
    {
        if (HasFlow) Flow.SetMoney(amount);
        else { localMoney = amount; RefreshDisplay(); }
    }

    // ─────────────────────────────────────────────
    //  랭크 보상 카운트업 연출 (Street/트렁크 씬에서 호출)
    // ─────────────────────────────────────────────

    /// <summary>
    /// StageFlowManager 에 예약된 랭크 보상(PendingReward)을 카운트업 애니메이션으로 보여준다.
    /// 잔액엔 이미 반영돼 있으므로 (잔액 - 보상) → 잔액 으로 숫자만 굴린다.
    /// AutoDrive.onDrivingComplete 등 트렁크 도착 시점에 연결.
    /// </summary>
    public void PlayPendingRewardCountUp()
    {
        if (!HasFlow || Flow.PendingReward <= 0)
        {
            RefreshDisplay();
            return;
        }

        int reward = Flow.ConsumePendingReward();
        int to = Flow.Money;
        int from = Mathf.Max(0, to - reward);

        if (countUpCoroutine != null) StopCoroutine(countUpCoroutine);
        countUpCoroutine = StartCoroutine(CountUpRoutine(from, to));

        if (showDebugInfo)
            Debug.Log($"[MoneyManager] 랭크 보상 카운트업 {from:N0} → {to:N0} (+{reward:N0})");
    }

    IEnumerator CountUpRoutine(int from, int to)
    {
        if (countUpStartDelay > 0f)
        {
            // 대기 중에는 시작값으로 고정 표시
            if (moneyText != null)
            {
                moneyText.text = $"Cash: {from:N0}";
                moneyText.ForceMeshUpdate();
            }
            yield return new WaitForSeconds(countUpStartDelay);
        }

        if (sfxSource != null && countUpClip != null)
        {
            sfxSource.clip = countUpClip;
            sfxSource.loop = true;
            sfxSource.Play();
        }

        float elapsed = 0f;
        float dur = Mathf.Max(0.01f, countUpDuration);
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            float smooth = 1f - Mathf.Pow(1f - t, 3f);   // EaseOutCubic
            int v = Mathf.RoundToInt(Mathf.Lerp(from, to, smooth));
            if (moneyText != null)
            {
                moneyText.text = $"Cash: {v:N0}";
                moneyText.ForceMeshUpdate();
            }
            yield return null;
        }

        if (sfxSource != null && sfxSource.clip == countUpClip)
        {
            sfxSource.Stop();
            sfxSource.loop = false;
        }

        countUpCoroutine = null;
        RefreshDisplay();
    }
}
