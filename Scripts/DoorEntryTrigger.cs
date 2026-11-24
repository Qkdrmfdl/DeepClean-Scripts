using UnityEngine;

/// <summary>
/// StreetScene의 집 입구 문에 부착하는 트리거.
/// 플레이어가 문을 통과할 때 StageFlowManager.AllToolsSelected를 확인하고
/// 조건 충족 시 GoToNextStage()를 호출해 다음 스테이지로 전환.
///
/// 설정 방법:
///   1. 문 앞 빈 GameObject에 이 스크립트와 Trigger Collider 부착
///   2. Is Trigger : 체크
///   3. Blocked UI : 도구 미선택 시 표시할 안내 UI (선택)
///   4. StageFlowManager.OnAllToolsSelectedEvent가 발생하면 문 하이라이트 자동 활성화
///
/// 흐름:
///   도구 모두 선택 → OnAllToolsSelectedEvent → 문 활성화 표시
///   플레이어 진입 → AllToolsSelected 확인 → GoToNextStage()
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorEntryTrigger : MonoBehaviour
{
    // ═══════════════════════════════════════════════════
    //  Inspector 설정
    // ═══════════════════════════════════════════════════

    [Header("안내 UI")]
    [Tooltip("도구를 아직 선택하지 않았을 때 표시할 안내 UI (선택)")]
    public GameObject blockedUI;

    [Tooltip("모든 도구 선택 완료 후 통과 가능 상태 표시 UI (선택)")]
    public GameObject readyUI;

    [Header("통과 가능 표시")]
    [Tooltip("모든 도구 선택 완료 시 문 테두리에 표시할 색상")]
    public Color readyColor = new Color(0.2f, 1f, 0.3f, 1f);

    [Tooltip("통과 불가 상태 색상")]
    public Color blockedColor = new Color(1f, 0.2f, 0.2f, 1f);

    [Tooltip("문 주변 렌더러 (색상으로 상태 표시, 없으면 생략)")]
    public Renderer doorRenderer;

    [Tooltip("안내 UI 자동 숨김까지 대기 시간 (초). 0이면 숨기지 않음.")]
    public float blockedUIHideDelay = 2.5f;

    [Header("플레이어 태그")]
    [Tooltip("플레이어 콜라이더 태그")]
    public string playerTag = "Player";

    [Header("디버그")]
    public bool showDebugInfo = true;

    // ═══════════════════════════════════════════════════
    //  내부 상태
    // ═══════════════════════════════════════════════════

    private bool hasTriggered = false;
    private bool isReady = false;
    private Coroutine hideUICoroutine;
    private Color[] doorOriginalColors;

    // ═══════════════════════════════════════════════════
    //  Unity 생명주기
    // ═══════════════════════════════════════════════════

    void Awake()
    {
        // Trigger 강제 설정
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        // 문 렌더러 원본 색상 캐싱
        if (doorRenderer != null)
        {
            doorOriginalColors = new Color[doorRenderer.materials.Length];
            for (int m = 0; m < doorRenderer.materials.Length; m++)
                doorOriginalColors[m] = doorRenderer.materials[m].color;
        }
    }

    void Start()
    {
        // UI 초기 상태
        if (blockedUI != null) blockedUI.SetActive(false);
        if (readyUI != null)   readyUI.SetActive(false);

        // 문 색상 초기화 (잠금 상태)
        SetDoorColor(blockedColor);

        // StageFlowManager 이벤트 구독
        if (StageFlowManager.Instance != null)
        {
            StageFlowManager.Instance.OnAllToolsSelectedEvent += OnAllToolsSelected;
            Log("StageFlowManager 이벤트 구독 완료");

            // 이미 모든 도구가 선택돼 있는 경우 (씬 재진입 등)
            if (StageFlowManager.Instance.AllToolsSelected)
                OnAllToolsSelected();
        }
        else
        {
            Debug.LogError("[DoorEntryTrigger] StageFlowManager.Instance가 null입니다! " +
                           "StageFlowManager 오브젝트가 씬에 있는지 확인하세요.");
        }
    }

    void OnDestroy()
    {
        // 이벤트 구독 해제 (씬 전환 시 메모리 누수 방지)
        if (StageFlowManager.Instance != null)
            StageFlowManager.Instance.OnAllToolsSelectedEvent -= OnAllToolsSelected;
    }

    // ═══════════════════════════════════════════════════
    //  트리거 이벤트
    // ═══════════════════════════════════════════════════

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (hasTriggered) return;

        if (StageFlowManager.Instance == null)
        {
            Debug.LogError("[DoorEntryTrigger] StageFlowManager.Instance가 null입니다!");
            return;
        }

        if (StageFlowManager.Instance.AllToolsSelected)
        {
            // 조건 충족 → 다음 스테이지로
            hasTriggered = true;
            Log("플레이어 진입 + 모든 도구 선택 완료 → GoToNextStage()");
            StageFlowManager.Instance.GoToNextStage();
        }
        else
        {
            // 조건 미충족 → 안내 UI 표시
            Log("플레이어 진입 but 도구 미선택 → 안내 표시");
            ShowBlockedUI();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        // 영역 이탈 시 안내 UI 즉시 숨김
        if (blockedUI != null) blockedUI.SetActive(false);
        if (hideUICoroutine != null)
        {
            StopCoroutine(hideUICoroutine);
            hideUICoroutine = null;
        }
    }

    // ═══════════════════════════════════════════════════
    //  이벤트 콜백
    // ═══════════════════════════════════════════════════

    /// <summary>StageFlowManager.OnAllToolsSelectedEvent 콜백.</summary>
    private void OnAllToolsSelected()
    {
        isReady = true;
        Log("모든 도구 선택 완료 → 문 통과 가능 상태로 전환");

        // 문 색상 → 초록
        SetDoorColor(readyColor);

        // UI 전환
        if (blockedUI != null) blockedUI.SetActive(false);
        if (readyUI != null)   readyUI.SetActive(true);
    }

    // ═══════════════════════════════════════════════════
    //  UI / 색상 처리
    // ═══════════════════════════════════════════════════

    private void ShowBlockedUI()
    {
        if (blockedUI == null) return;

        blockedUI.SetActive(true);

        // 이전 자동 숨김 코루틴이 있으면 취소
        if (hideUICoroutine != null)
            StopCoroutine(hideUICoroutine);

        if (blockedUIHideDelay > 0f)
            hideUICoroutine = StartCoroutine(HideBlockedUIAfterDelay());
    }

    private System.Collections.IEnumerator HideBlockedUIAfterDelay()
    {
        yield return new WaitForSeconds(blockedUIHideDelay);
        if (blockedUI != null) blockedUI.SetActive(false);
        hideUICoroutine = null;
    }

    private void SetDoorColor(Color color)
    {
        if (doorRenderer == null) return;

        Material[] mats = doorRenderer.materials;
        foreach (var mat in mats)
            mat.color = color;
    }

    // ═══════════════════════════════════════════════════
    //  디버그
    // ═══════════════════════════════════════════════════

    private void Log(string msg)
    {
        if (showDebugInfo)
            Debug.Log($"[DoorEntryTrigger] {msg}");
    }

    void OnDrawGizmos()
    {
        Gizmos.color = isReady
            ? new Color(0.2f, 1f, 0.3f, 0.3f)
            : new Color(1f, 0.2f, 0.2f, 0.3f);
        Gizmos.DrawCube(transform.position, transform.localScale);
    }
}
