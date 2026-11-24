using System.Collections;
using TMPro;
using UnityEngine;

[System.Serializable]
public class NarrationEntry
{
    [Tooltip("오디오 클립 (없으면 자막만 표시)")]
    public AudioClip clip;
    [TextArea(2, 5)]
    public string subtitle;
    [Tooltip("0이면 클립 길이 자동 적용, 클립도 없으면 3초")]
    public float overrideDuration = 0f;
}

public class TutorialNarrationManager : MonoBehaviour
{
    public static TutorialNarrationManager Instance { get; private set; }

    [Header("나레이션 목록 (0번 인덱스 = 1번 나레이션)")]
    [Tooltip("0~20번. Inspector에서 Size를 21로 늘리고 AudioClip/자막 연결 후 사용")]
    public NarrationEntry[] narrations = new NarrationEntry[21];

    [Header("오디오")]
    public AudioSource audioSource;
    [Tooltip("나레이션 전체 볼륨 배율 (1 = 기본, 2 = 두 배)")]
    [Range(0f, 3f)]
    public float narrationVolume = 1f;

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

    private CanvasGroup _canvasGroup;
    private Coroutine _current;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

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

    // 인덱스로 재생 (1번 나레이션 → index 0)
    public void Play(int index)
    {
        if (index < 0 || index >= narrations.Length) return;
        if (_current != null) StopCoroutine(_current);
        _current = StartCoroutine(PlayAndClear(narrations[index]));
    }

    IEnumerator PlayAndClear(NarrationEntry entry)
    {
        yield return PlayRoutine(entry);
        _current = null;
    }

    // 여러 나레이션 순서대로 재생 (앞 항목 종료 후 다음 재생)
    public void PlaySequence(params int[] indices)
    {
        if (_current != null) StopCoroutine(_current);
        _current = StartCoroutine(SequenceRoutine(indices));
    }

    public void Stop()
    {
        if (_current != null) { StopCoroutine(_current); _current = null; }
        if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
        HideSubtitleImmediate();
    }

    public bool IsPlaying => _current != null;

    // 나레이션 전체(페이드아웃 포함) 완료까지 대기
    public Coroutine WaitUntilDone() => StartCoroutine(WaitRoutine());

    IEnumerator WaitRoutine()
    {
        while (_current != null)
            yield return null;
    }

    // 해당 인덱스 나레이션의 오디오 재생 시간 반환 (fadeInDuration 포함)
    // Play() 직후 이 시간만큼 대기하면 오디오가 끝나는 시점과 일치
    public float GetAudioEndTime(int index)
    {
        if (index < 0 || index >= narrations.Length) return 0f;
        var e = narrations[index];
        if (e == null) return 0f;
        float dur = e.overrideDuration > 0f
            ? e.overrideDuration
            : (e.clip != null ? e.clip.length : 3f);
        return fadeInDuration + dur;
    }

    IEnumerator SequenceRoutine(int[] indices)
    {
        foreach (int i in indices)
        {
            if (i < 0 || i >= narrations.Length) continue;
            yield return PlayRoutine(narrations[i]);
        }
        _current = null;
    }

    IEnumerator PlayRoutine(NarrationEntry entry)
    {
        if (entry == null) yield break;

        float duration = entry.overrideDuration > 0f
            ? entry.overrideDuration
            : (entry.clip != null ? entry.clip.length : 3f);

        bool hasSubtitle = !string.IsNullOrEmpty(entry.subtitle);

        if (hasSubtitle && subtitlePanel != null)
        {
            subtitleText.text = entry.subtitle;
            subtitlePanel.SetActive(true);

            // 자막이 이미 표시 중이면 페이드인 생략 (연속 재생 시 끊김 방지)
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
