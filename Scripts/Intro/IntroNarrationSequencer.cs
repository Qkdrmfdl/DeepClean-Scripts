using System.Collections;
using TMPro;
using UnityEngine;

public class IntroNarrationSequencer : MonoBehaviour
{
    [Header("전화 벨소리")]
    public AudioClip ringtoneClip;
    [Tooltip("벨소리 재생 후 나레이션 시작까지 대기 시간 (초)")]
    public float delayAfterRingtone = 0.3f;

    [Header("나레이션 목록 (순서대로 재생)")]
    public NarrationEntry[] narrations = new NarrationEntry[4];

    [Header("오디오")]
    public AudioSource audioSource;
    [Range(0f, 3f), Tooltip("나레이션 볼륨 (1 = 기본, 3까지 증폭 가능)")]
    public float narrationVolume = 1f;
    [Range(0f, 3f), Tooltip("벨소리 볼륨")]
    public float ringtoneVolume = 1f;

    [Header("자막 UI")]
    [Tooltip("World Space Canvas로 설정된 패널 오브젝트")]
    public GameObject subtitlePanel;
    public TextMeshProUGUI subtitleText;

    [Header("자막 위치")]
    [Tooltip("없으면 Camera.main 자동 사용")]
    public Camera xrCamera;
    [Tooltip("카메라 정면 거리 (m)")]
    public float subtitleDistance = 2f;
    [Tooltip("카메라 위치 기준 수직 오프셋 (음수 = 아래)")]
    public float subtitleHeightOffset = -0.5f;

    [Header("페이드")]
    public float fadeInDuration = 0.3f;
    public float fadeOutDuration = 0.5f;

    [Header("시작 옵션")]
    [Tooltip("체크하면 씬 로드 직후 자동 시작")]
    public bool playOnStart = true;
    [Tooltip("자동 시작 전 대기 시간 (초)")]
    public float startDelay = 0f;

    [Header("나레이션 종료 후 패드 전환")]
    [Tooltip("빈 화면 패드 오브젝트 (나레이션 종료 시 비활성화)")]
    public GameObject blankPadObject;
    [Tooltip("카톡화면 패드 오브젝트 (나레이션 종료 시 활성화)")]
    public GameObject kakaoPadObject;
    [Tooltip("카톡 메시지 알림 효과음")]
    public AudioClip notificationSound;
    [Range(0f, 3f)]
    public float notificationVolume = 1f;
    [Tooltip("마지막 나레이션 종료 후 패드 전환까지 대기 시간 (초)")]
    public float delayBeforePadSwitch = 0f;

    private CanvasGroup _canvasGroup;
    private Coroutine _current;

    void Awake()
    {
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;

        if (subtitlePanel != null)
        {
            _canvasGroup = subtitlePanel.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = subtitlePanel.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            subtitlePanel.SetActive(false);
        }

        if (xrCamera == null) xrCamera = Camera.main;
    }

    void Start()
    {
        if (playOnStart)
            _current = StartCoroutine(RunSequence(startDelay));
    }

    // 외부에서 수동으로 시작할 때 호출
    public void Play()
    {
        if (_current != null) StopCoroutine(_current);
        _current = StartCoroutine(RunSequence(0f));
    }

    public void Stop()
    {
        if (_current != null) { StopCoroutine(_current); _current = null; }
        if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
        HideSubtitleImmediate();
    }

    public bool IsPlaying => _current != null;

    IEnumerator RunSequence(float initialDelay)
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        // 1) 전화 벨소리
        if (ringtoneClip != null)
        {
            audioSource.PlayOneShot(ringtoneClip, ringtoneVolume);
            yield return new WaitForSeconds(ringtoneClip.length + delayAfterRingtone);
        }

        // 2) 나레이션 + 자막 순서 재생
        foreach (var entry in narrations)
        {
            if (entry == null) continue;
            yield return StartCoroutine(PlayNarration(entry));
        }

        // 3) 마지막 나레이션 종료 후 패드 전환
        if (kakaoPadObject != null)
        {
            if (delayBeforePadSwitch > 0f)
                yield return new WaitForSeconds(delayBeforePadSwitch);

            if (notificationSound != null)
                audioSource.PlayOneShot(notificationSound, notificationVolume);

            if (blankPadObject != null)
                blankPadObject.SetActive(false);

            kakaoPadObject.SetActive(true);
        }

        _current = null;
    }

    IEnumerator PlayNarration(NarrationEntry entry)
    {
        float duration = entry.overrideDuration > 0f
            ? entry.overrideDuration
            : (entry.clip != null ? entry.clip.length : 3f);

        bool hasSubtitle = !string.IsNullOrEmpty(entry.subtitle);

        if (hasSubtitle && subtitlePanel != null)
        {
            subtitleText.text = entry.subtitle;
            subtitlePanel.SetActive(true);

            float currentAlpha = _canvasGroup != null ? _canvasGroup.alpha : 0f;
            if (currentAlpha < 0.99f)
                yield return StartCoroutine(Fade(currentAlpha, 1f, fadeInDuration));
            else if (_canvasGroup != null)
                _canvasGroup.alpha = 1f;
        }

        if (entry.clip != null)
            audioSource.PlayOneShot(entry.clip, narrationVolume);

        yield return new WaitForSeconds(duration);

        if (hasSubtitle && subtitlePanel != null)
        {
            yield return StartCoroutine(Fade(1f, 0f, fadeOutDuration));
            subtitlePanel.SetActive(false);
        }
    }

    void HideSubtitleImmediate()
    {
        if (subtitlePanel == null) return;
        if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        subtitlePanel.SetActive(false);
    }

    IEnumerator Fade(float from, float to, float dur)
    {
        if (_canvasGroup == null) yield break;
        float t = 0f;
        while (t < dur)
        {
            _canvasGroup.alpha = Mathf.Lerp(from, to, t / dur);
            t += Time.deltaTime;
            yield return null;
        }
        _canvasGroup.alpha = to;
    }
}
