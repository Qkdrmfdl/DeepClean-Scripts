using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// 아웃트로 씬: 태블릿 알림 클릭 후 "결과" 패널을 켜고
/// 1단계 → 2단계 → 3단계 → 합산 순서로 값을 하나씩 나타낸다.
/// 값은 StageFlowManager.StageResults 에서 읽는다.
///
/// 연결 방법:
///   TabletNotification.onClicked → 이 컴포넌트의 Play()
///
/// 각 스테이지 행(stageRows)과 합산 행은 값 4줄이 들어갈 TMP 하나씩만 있으면 된다.
/// (라벨 "청소 진행도 / 오염 제거 / 소요시간 / 수익" 은 별도 고정 텍스트로 두면 됨)
/// </summary>
public class TotalResultSequencer : MonoBehaviour
{
    [System.Serializable]
    public class ResultRow
    {
        [Tooltip("순차적으로 나타낼 행 오브젝트. CanvasGroup 이 있으면 페이드인, 없으면 SetActive.")]
        public GameObject rowRoot;

        [Tooltip("값 4줄이 들어갈 TMP (청소율 / 오염 제거 / 소요시간 / 수익)")]
        public TMP_Text valueText;

        [Tooltip("이 행이 보여줄 스테이지 (0 = Stage1, 1 = Stage2, 2 = Stage3)")]
        public int stageIndex;
    }

    [Header("결과 패널")]
    [Tooltip("'결과' 패널 루트. 시작 시 자동으로 꺼둔다.")]
    public GameObject resultPanelRoot;
    [Tooltip("Play() 시 카메라 앞으로 스냅")]
    public bool snapToCameraOnPlay = false;
    [Tooltip("비우면 Camera.main 사용")]
    public Transform cameraTransform;
    public float distanceFromCamera = 1.5f;

    [Header("스테이지 행 (나타나는 순서대로)")]
    public ResultRow[] stageRows;

    [Header("합산 행")]
    public GameObject totalRowRoot;
    public TMP_Text totalValueText;
    [Tooltip("종합 랭크 표시용 TMP (선택)")]
    public TMP_Text overallRankText;

    [Header("연출 타이밍")]
    [Tooltip("패널이 켜진 뒤 첫 행이 나타나기까지 대기(초)")]
    public float startDelay = 0.4f;
    [Tooltip("행과 행 사이 간격(초)")]
    public float stepInterval = 0.9f;
    [Tooltip("각 행 페이드인 시간(초). 0 이면 즉시. (행에 CanvasGroup 이 있을 때만 페이드)")]
    public float fadeDuration = 0.35f;

    [Header("값 형식")]
    [Tooltip("오염 제거를 '55/100개' 처럼 전체 개수까지 표시")]
    public bool showItemDenominator = false;
    [Tooltip("소요시간을 mm:ss 대신 '15분' 형태로")]
    public bool elapsedAsMinutes = false;
    [Tooltip("금액 뒤에 붙일 단위")]
    public string moneySuffix = "원";

    [Header("사운드")]
    public AudioClip rowRevealSfx;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Tooltip("비우면 자동 생성")]
    public AudioSource sfxSource;

    [Header("디버그")]
    [Tooltip("StageFlowManager 결과가 없을 때(아웃트로 씬 단독 테스트) 가짜 데이터로 채움")]
    public bool useFakeDataIfEmpty = true;

    [Header("완료 이벤트")]
    public UnityEvent onSequenceComplete;

    public bool IsPlaying { get; private set; }

    private bool played;
    private Coroutine seqCo;

    void Awake()
    {
        if (sfxSource == null && rowRevealSfx != null)
        {
            sfxSource = GetComponent<AudioSource>();
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.spatialBlend = 0f;
            }
        }

        if (resultPanelRoot != null && resultPanelRoot != gameObject)
            resultPanelRoot.SetActive(false);

        HideAllRows();
    }

    /// <summary>결과 시퀀스를 시작한다. (TabletNotification.onClicked 에 연결)</summary>
    public void Play()
    {
        if (played || IsPlaying) return;
        played = true;
        IsPlaying = true;
        seqCo = StartCoroutine(PlaySequence());
    }

    /// <summary>다시 재생할 수 있도록 상태 초기화 (테스트용).</summary>
    public void ResetSequence()
    {
        if (seqCo != null) { StopCoroutine(seqCo); seqCo = null; }
        played = false;
        IsPlaying = false;
        HideAllRows();
        if (resultPanelRoot != null && resultPanelRoot != gameObject)
            resultPanelRoot.SetActive(false);
    }

    private IEnumerator PlaySequence()
    {
        if (resultPanelRoot != null)
            resultPanelRoot.SetActive(true);

        SnapToCamera();
        HideAllRows();

        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        // 스테이지 행: 배열 순서대로
        if (stageRows != null)
        {
            foreach (var row in stageRows)
            {
                if (row == null) continue;

                if (row.valueText != null)
                    row.valueText.text = BuildStageValue(row.stageIndex);

                yield return RevealRow(row.rowRoot);
                PlayRevealSfx();

                if (stepInterval > 0f)
                    yield return new WaitForSeconds(stepInterval);
            }
        }

        // 합산 행
        FillTotal();
        yield return RevealRow(totalRowRoot);
        PlayRevealSfx();

        IsPlaying = false;
        seqCo = null;
        onSequenceComplete?.Invoke();
    }

    // ── 값 구성 ────────────────────────────────────────

    private string BuildStageValue(int stageIndex)
    {
        var sfm = StageFlowManager.Instance;
        StageResult r = sfm != null ? sfm.GetStageResult(stageIndex) : null;

        if (r == null)
        {
            if (useFakeDataIfEmpty)
                return FormatValue(100f, 55, 100, 15 * 60f, 155000);
            Debug.LogWarning($"[TotalResultSequencer] Stage{stageIndex + 1} 결과가 없습니다.");
            return "-\n-\n-\n-";
        }

        return FormatValue(r.cleanPercent, r.cleanedCount, r.totalCount, r.elapsedSeconds, r.reward);
    }

    private void FillTotal()
    {
        var sfm = StageFlowManager.Instance;

        if (sfm != null && sfm.StageResults.Count > 0)
        {
            float avg    = sfm.GetAverageCleanPercent();
            int   cleaned = sfm.GetTotalCleanedCount(out int totalItems);
            float secs   = sfm.GetTotalElapsedSeconds();
            int   reward = sfm.GetTotalStageReward();

            if (totalValueText != null)
                totalValueText.text = FormatValue(avg, cleaned, totalItems, secs, reward);
            if (overallRankText != null)
                overallRankText.text = sfm.GetOverallRank();
        }
        else if (useFakeDataIfEmpty)
        {
            if (totalValueText != null)
                totalValueText.text = FormatValue(100f, 165, 300, 45 * 60f, 465000);
            if (overallRankText != null)
                overallRankText.text = "S";
        }
    }

    private string FormatValue(float pct, int cleaned, int total, float secs, int reward)
    {
        string line1 = $"{pct:F0}%";
        string line2 = (showItemDenominator && total > 0) ? $"{cleaned}/{total}개" : $"{cleaned}개";
        string line3 = elapsedAsMinutes ? $"{Mathf.RoundToInt(secs / 60f)}분" : FormatMMSS(secs);
        string line4 = $"{reward:N0}{moneySuffix}";
        return $"{line1}\n{line2}\n{line3}\n{line4}";
    }

    private static string FormatMMSS(float seconds)
    {
        int t = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return $"{t / 60:00}:{t % 60:00}";
    }

    // ── 행 표시 ────────────────────────────────────────

    private void HideAllRows()
    {
        if (stageRows != null)
            foreach (var row in stageRows)
                if (row != null && row.rowRoot != null)
                    SetRowHidden(row.rowRoot);

        if (totalRowRoot != null)
            SetRowHidden(totalRowRoot);
    }

    private void SetRowHidden(GameObject row)
    {
        var cg = row.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 0f;
        row.SetActive(false);
    }

    private IEnumerator RevealRow(GameObject row)
    {
        if (row == null) yield break;

        row.SetActive(true);
        var cg = row.GetComponent<CanvasGroup>();

        if (cg == null || fadeDuration <= 0f)
        {
            if (cg != null) cg.alpha = 1f;
            yield break;
        }

        float t = 0f;
        cg.alpha = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    private void SnapToCamera()
    {
        if (!snapToCameraOnPlay || resultPanelRoot == null) return;

        Transform cam = cameraTransform != null
            ? cameraTransform
            : (Camera.main != null ? Camera.main.transform : null);
        if (cam == null) return;

        resultPanelRoot.transform.position = cam.position + cam.forward * distanceFromCamera;
        resultPanelRoot.transform.rotation =
            Quaternion.LookRotation(resultPanelRoot.transform.position - cam.position);
    }

    private void PlayRevealSfx()
    {
        if (rowRevealSfx != null && sfxSource != null)
            sfxSource.PlayOneShot(rowRevealSfx, sfxVolume);
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 시퀀스 재생")]
    private void _TestPlay() { ResetSequence(); Play(); }
#endif
}
