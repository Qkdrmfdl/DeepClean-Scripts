using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// 각 Stage 씬에 배치하는 카운트다운 타이머.
/// UI는 기존 CleaningPercent Canvas 안의 Text + Slider를 사용 (별도 캔버스 없음).
/// - 시간 초과 시 ScoreManager.ShowRankUI() 자동 호출 (강제 청소완료)
/// - 남은 시간 비율(0~1)을 ScoreManager가 읽어 보너스 배율에 사용
/// </summary>
public class StageTimer : MonoBehaviour
{
    [Header("제한 시간")]
    [Tooltip("스테이지 제한 시간 (초). 기본 20분 = 1200")]
    public float timeLimitSeconds = 1200f;

    [Header("UI (CleaningPercent Canvas 안에 배치)")]
    [Tooltip("남은 시간을 표시할 TMP 텍스트")]
    public TextMeshProUGUI timerText;
    [Tooltip("남은 시간 앞에 붙일 라벨 (예: \"제한 시간: \")")]
    public string timerLabel = "제한 시간: ";
    [Tooltip("시간 경과를 표현할 슬라이더 (1→0으로 감소)")]
    public Slider timerSlider;

    [Header("색상 경고")]
    [Tooltip("남은 시간이 이 값(초) 이하일 때 주의 색상")]
    public float warningThreshold = 300f;   // 5분
    [Tooltip("남은 시간이 이 값(초) 이하일 때 위험 색상")]
    public float dangerThreshold  = 120f;   // 2분
    public Color normalColor  = Color.white;
    public Color warningColor = Color.yellow;
    public Color dangerColor  = Color.red;

    [Header("슬라이더 색상 (Fill Image)")]
    [Tooltip("슬라이더 Fill Image. 연결하면 색상도 자동 변경됨")]
    public Image sliderFillImage;

    [Header("깜빡임")]
    [Tooltip("위험 구간에서 타이머 텍스트 깜빡이는 주기 (초)")]
    public float blinkInterval = 0.5f;

    [Header("ScoreManager 연동")]
    public ScoreManager scoreManager;

    [Header("디버그")]
    public bool showDebugInfo = false;

    private float remainingTime;
    private bool isRunning  = false;
    private bool hasExpired = false;
    private bool isBlinking = false;
    private Coroutine blinkCoroutine;

    /// <summary>남은 시간 비율 (0 = 없음, 1 = 가득)</summary>
    public float RemainingRatio => timeLimitSeconds > 0f ? Mathf.Clamp01(remainingTime / timeLimitSeconds) : 0f;

    /// <summary>남은 시간 (초)</summary>
    public float RemainingSeconds => remainingTime;

    /// <summary>경과(소요) 시간 (초) = 제한 시간 - 남은 시간</summary>
    public float ElapsedSeconds => Mathf.Clamp(timeLimitSeconds - remainingTime, 0f, timeLimitSeconds);

    /// <summary>타이머가 동작 중인지</summary>
    public bool IsRunning => isRunning;

    void Start()
    {
        if (scoreManager == null)
            scoreManager = FindFirstObjectByType<ScoreManager>();

        if (scoreManager == null)
            Debug.LogWarning("[StageTimer] ScoreManager를 찾을 수 없습니다!");

        // 슬라이더 초기값
        if (timerSlider != null)
        {
            timerSlider.minValue = 0f;
            timerSlider.maxValue = 1f;
            timerSlider.value    = 1f;
        }

        remainingTime = timeLimitSeconds;
        RefreshUI();
        isRunning = true;

        if (showDebugInfo)
            Debug.Log($"[StageTimer] 시작 — 제한 시간: {FormatTime(timeLimitSeconds)}");
    }

    void Update()
    {
        if (!isRunning || hasExpired) return;

        remainingTime -= Time.deltaTime;

        if (remainingTime <= 0f)
        {
            remainingTime = 0f;
            OnTimeExpired();
            return;
        }

        RefreshUI();
        UpdateColorAndBlink();
    }

    void RefreshUI()
    {
        // 텍스트
        if (timerText != null)
        {
            string newText = timerLabel + FormatTime(remainingTime);
            if (timerText.text != newText)
            {
                timerText.text = newText;
                timerText.ForceMeshUpdate();
            }
        }

        // 슬라이더 (1 → 0)
        if (timerSlider != null)
            timerSlider.value = RemainingRatio;
    }

    void UpdateColorAndBlink()
    {
        Color targetColor;

        if (remainingTime <= dangerThreshold)
        {
            targetColor = dangerColor;
            if (!isBlinking)
                blinkCoroutine = StartCoroutine(BlinkText());
        }
        else if (remainingTime <= warningThreshold)
        {
            targetColor = warningColor;
            StopBlink();
        }
        else
        {
            targetColor = normalColor;
            StopBlink();
        }

        if (timerText != null)       timerText.color       = targetColor;
        if (sliderFillImage != null) sliderFillImage.color = targetColor;
    }

    IEnumerator BlinkText()
    {
        isBlinking = true;
        while (isBlinking)
        {
            if (timerText != null) timerText.enabled = !timerText.enabled;
            yield return new WaitForSeconds(blinkInterval);
        }
        if (timerText != null) timerText.enabled = true;
    }

    void StopBlink()
    {
        if (!isBlinking) return;
        isBlinking = false;
        if (blinkCoroutine != null) { StopCoroutine(blinkCoroutine); blinkCoroutine = null; }
        if (timerText != null) timerText.enabled = true;
    }

    void OnTimeExpired()
    {
        hasExpired = true;
        isRunning  = false;
        StopBlink();

        if (timerText != null)
        {
            timerText.text    = timerLabel + "00:00";
            timerText.color   = dangerColor;
            timerText.enabled = true;
            timerText.ForceMeshUpdate();
        }
        if (timerSlider != null)       timerSlider.value    = 0f;
        if (sliderFillImage != null)   sliderFillImage.color = dangerColor;

        if (showDebugInfo)
            Debug.Log("[StageTimer] 시간 초과 — 강제 청소완료");

        if (scoreManager != null)
            scoreManager.ShowRankUI();
        else
            Debug.LogError("[StageTimer] ScoreManager가 없어 강제 완료를 처리할 수 없습니다!");
    }

    /// <summary>청소완료 버튼을 직접 눌렀을 때 ScoreManager.ShowRankUI()에서 호출</summary>
    public void StopTimer()
    {
        if (!isRunning) return;
        isRunning = false;
        StopBlink();

        if (showDebugInfo)
            Debug.Log($"[StageTimer] 수동 정지 — 남은 시간: {FormatTime(remainingTime)} ({RemainingRatio:P0})");
    }

    public static string FormatTime(float seconds)
    {
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return $"{total / 60:D2}:{total % 60:D2}";
    }

#if UNITY_EDITOR
    [ContextMenu("타이머 강제 만료 (테스트)")]
    void DebugExpire() => remainingTime = 0f;

    [ContextMenu("남은 시간 30초로 설정 (테스트)")]
    void DebugSet30() => remainingTime = 30f;
#endif
}
