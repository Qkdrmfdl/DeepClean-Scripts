using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 아웃트로 씬: 차 자율주행 중 태블릿(메세지) 알림을 띄우고 반짝이게 한다.
/// 알림 안의 버튼을 클릭하면 반짝임을 멈추고 onClicked 이벤트를 발생시킨다.
/// (전체 결과창 표시는 TotalResultSequencer 가 onClicked 를 구독해서 처리)
///
/// 배치 주의:
///   • 이 컴포넌트는 "메세지 알림 Canvas"가 아니라, 항상 켜져 있는 별도 오브젝트
///     (예: 차, 또는 빈 "Outro Manager")에 붙이고 notificationRoot 에 알림 캔버스를 지정한다.
///     (알림 캔버스 자신에 붙이면 Awake 에서 자기 자신을 꺼버려 코루틴이 안 돈다.)
///
/// 표시 시점(둘 중 하나):
///   • autoShowOnStart = true : 씬 시작 후 showDelay 초 뒤 자동 표시 (기본)
///   • AutoDrive.onDrivingComplete 등에 ShowNotification() 을 연결
/// </summary>
public class TabletNotification : MonoBehaviour
{
    [Header("알림 오브젝트")]
    [Tooltip("표시/숨김할 알림 루트 (메세지 알림 Canvas). 시작 시 자동으로 꺼둔다.")]
    public GameObject notificationRoot;

    [Header("반짝임")]
    [Tooltip("깜빡일 대상 CanvasGroup. 비워두면 notificationRoot 에서 찾거나 자동 추가.")]
    public CanvasGroup blinkTarget;
    [Range(0f, 1f)] public float minAlpha = 0.3f;
    [Range(0f, 1f)] public float maxAlpha = 1f;
    [Tooltip("한 번 깜빡이는 주기(초). 작을수록 빠르게 깜빡임.")]
    public float blinkPeriod = 0.8f;
    [Tooltip("깜빡일 때 살짝 커졌다 작아지는 배율. 1 이면 크기 변화 없음.")]
    public float pulseScale = 1.06f;

    [Header("표시 시점")]
    [Tooltip("체크 시 씬 시작 후 showDelay 초 뒤 자동으로 알림 표시. 끄면 ShowNotification() 을 외부에서 호출.")]
    public bool autoShowOnStart = true;
    [Tooltip("autoShowOnStart 일 때 대기 시간(초). 차 출발(AutoDrive.autoStartDelay) 이후 주행 중에 뜨도록 맞춘다.")]
    public float showDelay = 8f;

    [Header("클릭 버튼")]
    [Tooltip("알림을 여는 버튼(메세지Button). 비워두면 notificationRoot 자식에서 자동 검색.")]
    public Button clickButton;
    [Tooltip("클릭 시 알림 루트도 같이 숨길지")]
    public bool hideRootOnClick = true;

    [Header("사운드")]
    public AudioClip notificationSfx;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Tooltip("비워두면 자동 생성")]
    public AudioSource sfxSource;

    [Header("이벤트")]
    public UnityEvent onShown;
    [Tooltip("알림 버튼을 클릭했을 때 1회 발생. 전체 결과창 표시(TotalResultSequencer)를 여기에 연결.")]
    public UnityEvent onClicked;

    /// <summary>알림이 현재 표시 중인지</summary>
    public bool IsShowing { get; private set; }
    /// <summary>알림 버튼이 이미 클릭됐는지 (중복 방지)</summary>
    public bool WasClicked { get; private set; }

    private Coroutine blinkCo;
    private Vector3 baseScale = Vector3.one;
    private bool listenerBound;

    void Awake()
    {
        if (notificationRoot == null)
        {
            Debug.LogWarning("[TabletNotification] notificationRoot 가 비어 있습니다. " +
                             "알림 캔버스를 지정하세요. (이 컴포넌트는 알림 캔버스와 다른, 항상 켜진 오브젝트에 두세요.)");
            return;
        }

        baseScale = notificationRoot.transform.localScale;

        if (blinkTarget == null)
            blinkTarget = notificationRoot.GetComponent<CanvasGroup>()
                       ?? notificationRoot.AddComponent<CanvasGroup>();

        if (clickButton == null)
            clickButton = notificationRoot.GetComponentInChildren<Button>(true);

        if (sfxSource == null && notificationSfx != null)
        {
            sfxSource = GetComponent<AudioSource>();
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.spatialBlend = 0f;
            }
        }

        notificationRoot.SetActive(false);
        IsShowing = false;
    }

    void OnEnable() { BindClick(); }
    void OnDisable() { UnbindClick(); }

    void Start()
    {
        if (autoShowOnStart)
            StartCoroutine(AutoShowRoutine());
    }

    private IEnumerator AutoShowRoutine()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, showDelay));
        ShowNotification();
    }

    /// <summary>알림을 표시하고 반짝임을 시작한다. (AutoDrive.onDrivingComplete 등에 연결 가능)</summary>
    public void ShowNotification()
    {
        if (IsShowing || WasClicked || notificationRoot == null) return;
        IsShowing = true;

        notificationRoot.SetActive(true);
        notificationRoot.transform.localScale = baseScale;

        if (sfxSource != null && notificationSfx != null)
            sfxSource.PlayOneShot(notificationSfx, sfxVolume);

        if (blinkCo != null) StopCoroutine(blinkCo);
        blinkCo = StartCoroutine(BlinkRoutine());

        onShown?.Invoke();
    }

    /// <summary>반짝임만 멈춘다 (알림은 그대로 유지, 알파/스케일 원상복구).</summary>
    public void StopBlinking()
    {
        if (blinkCo != null) { StopCoroutine(blinkCo); blinkCo = null; }
        if (blinkTarget != null) blinkTarget.alpha = 1f;
        if (notificationRoot != null) notificationRoot.transform.localScale = baseScale;
    }

    /// <summary>알림을 숨기고 반짝임을 멈춘다.</summary>
    public void Dismiss()
    {
        StopBlinking();
        IsShowing = false;
        if (notificationRoot != null) notificationRoot.SetActive(false);
    }

    private IEnumerator BlinkRoutine()
    {
        float t = 0f;
        float period = Mathf.Max(0.05f, blinkPeriod);
        while (true)
        {
            t += Time.deltaTime;
            float s = (Mathf.Sin(t * Mathf.PI * 2f / period) + 1f) * 0.5f; // 0..1

            if (blinkTarget != null)
                blinkTarget.alpha = Mathf.Lerp(minAlpha, maxAlpha, s);

            if (pulseScale > 1f && notificationRoot != null)
                notificationRoot.transform.localScale = baseScale * Mathf.Lerp(1f, pulseScale, s);

            yield return null;
        }
    }

    // ── 클릭 처리 ──────────────────────────────────────
    private void BindClick()
    {
        if (listenerBound) return;
        if (clickButton == null && notificationRoot != null)
            clickButton = notificationRoot.GetComponentInChildren<Button>(true);
        if (clickButton == null) return;

        clickButton.onClick.AddListener(HandleClick);
        listenerBound = true;
    }

    private void UnbindClick()
    {
        if (listenerBound && clickButton != null)
            clickButton.onClick.RemoveListener(HandleClick);
        listenerBound = false;
    }

    /// <summary>알림 버튼 클릭 처리. 인스펙터 버튼 OnClick 에 직접 연결해도 됨.</summary>
    public void HandleClick()
    {
        if (WasClicked || !IsShowing) return;   // 알림 뜨기 전/중복 클릭 무시
        WasClicked = true;

        StopBlinking();
        IsShowing = false;
        if (hideRootOnClick && notificationRoot != null)
            notificationRoot.SetActive(false);

        onClicked?.Invoke();
    }

#if UNITY_EDITOR
    [ContextMenu("테스트: 알림 표시")]
    private void _TestShow() => ShowNotification();

    [ContextMenu("테스트: 클릭 처리")]
    private void _TestClick() { IsShowing = true; WasClicked = false; HandleClick(); }
#endif
}
