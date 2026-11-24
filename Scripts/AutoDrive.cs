using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class WaypointSet
{
    [Tooltip("이 방문에서 사용할 웨이포인트 순서")]
    public Transform[] waypoints;

    [Tooltip("씬 시작 시 차량이 텔레포트될 때 적용할 초기 회전값 (오일러각)")]
    public Vector3 startRotation;
}

public class AutoDrive : MonoBehaviour
{
    [Header("방문별 웨이포인트")]
    [Tooltip("StreetVisitIndex별 웨이포인트 세트\n• [0] : 인트로 방문\n• [1] : Stage1 클리어 후\n• [2] : Stage2 클리어 후")]
    public WaypointSet[] waypointSets;

    // 런타임에 선택된 웨이포인트 (waypointSets에서 자동 선택)
    private Transform[] waypoints;

    [Header("이동 설정")]
    public float moveSpeed = 5f;
    [Tooltip("회전 속도 (도/초). RotateTowards 방식이라 목표 방향에 부드럽게 수렴하고 상시 떨림이 없음.\n" +
             "예전 Slerp 계수(5 근처)가 아니라 실제 각속도이므로 60~120 정도 권장.")]
    public float rotateSpeed = 90f;
    public float waypointReachDistance = 0.5f;

    [Tooltip("자율주행 중 차 회전 여부. 끄면 Inspector에 설정한 회전값 그대로 유지.")]
    public bool rotateWhileDriving = false;

    [Tooltip("회전할 때 모델 축 보정값 (Y축). 차가 90도 틀어지면 -90 또는 90 입력.")]
    public float rotationYOffset = 0f;

    [Header("VR 흔들림 방지")]
    [Tooltip("체크 시 FixedUpdate + Kinematic Rigidbody(Interpolate)로 이동한다.\n" +
             "플레이어 리그(XR Origin)나 UI 캔버스가 이 차의 자식일 때, 주행 중에도 컨트롤러 레이가 떨리지 않게 해준다.\n" +
             "Rigidbody 가 없으면 자동으로 추가됨(Kinematic).")]
    public bool smoothForVR = true;

    [Header("Arrival")]
    [Tooltip("방문별 플레이어 하차 위치 (waypointSets와 같은 인덱스)\n• [0] : 인트로 방문\n• [1] : Stage1 클리어 후\n• [2] : Stage2 클리어 후")]
    public Transform[] playerExitPoints;
    public Transform playerTransform;   // XR Origin (플레이어)
    public float exitDelay = 2f;

    public UnityEvent onDrivingComplete; // 주행 완료 이벤트 (선택)

    [Tooltip("씬 시작 시 자동으로 주행 시작 (운전대/버튼 클릭 없이 바로 출발).")]
    public bool autoStartDriving = false;

    [Tooltip("Auto Start Driving 시 출발 전 대기 시간 (초)")]
    public float autoStartDelay = 0f;

    [Header("사운드")]
    [Tooltip("주행 중 재생할 엔진음 (비워두면 무음). Loop 되는 클립 권장.")]
    public AudioClip engineLoopClip;
    [Tooltip("엔진음 재생용 AudioSource (비워두면 자동 생성)")]
    public AudioSource engineAudio;
    [Tooltip("정지 시 페이드아웃 시간 (초). 0이면 즉시 정지.")]
    public float engineFadeOutTime = 0.4f;

    [Header("디버그")]
    public bool showDebugInfo = true;

    private int currentWaypointIndex = 0;
    private bool isDriving = false;
    private int selectedVisitIndex = 0;
    private float desiredYaw;

    private Rigidbody rb;
    private Coroutine engineFadeCoroutine;

    void Awake()
    {
        if (smoothForVR)
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();

            rb.isKinematic    = true;
            rb.useGravity     = false;
            rb.interpolation  = RigidbodyInterpolation.Interpolate;

            if (showDebugInfo)
                Debug.Log("[AutoDrive] smoothForVR 활성 — Kinematic Rigidbody(Interpolate)로 이동합니다.");
        }
    }

    void Start()
    {
        // 엔진음용 AudioSource 준비
        if (engineAudio == null)
            engineAudio = GetComponent<AudioSource>();
        if (engineAudio != null)
        {
            engineAudio.playOnAwake = false;
            engineAudio.loop = true;
        }

        SelectWaypointSet();

        if (autoStartDriving)
            StartCoroutine(AutoStartWithDelay());
    }

    private void SelectWaypointSet()
    {
        if (waypointSets == null || waypointSets.Length == 0)
        {
            Debug.LogWarning("[AutoDrive] waypointSets가 비어있습니다. Inspector에서 설정하세요.");
            waypoints = new Transform[0];
            return;
        }

        int visitIndex = StageFlowManager.Instance != null
            ? StageFlowManager.Instance.StreetVisitIndex
            : 0;

        if (visitIndex >= waypointSets.Length)
        {
            Debug.LogWarning($"[AutoDrive] StreetVisitIndex({visitIndex})가 waypointSets 범위를 초과합니다. 마지막 세트를 사용합니다.");
            visitIndex = waypointSets.Length - 1;
        }

        waypoints = waypointSets[visitIndex].waypoints ?? new Transform[0];
        selectedVisitIndex = visitIndex;

        // 이전 위치에서 첫 웨이포인트로 이동하는 장면이 보이지 않도록 즉시 텔레포트
        if (waypoints.Length > 0 && waypoints[0] != null)
        {
            transform.position = waypoints[0].position;
            transform.rotation = Quaternion.Euler(waypointSets[visitIndex].startRotation);
            if (rb != null) { rb.position = transform.position; rb.rotation = transform.rotation; }
        }

        desiredYaw = transform.eulerAngles.y;

        if (showDebugInfo)
            Debug.Log($"[AutoDrive] StreetVisitIndex={visitIndex} → 웨이포인트 {waypoints.Length}개 선택됨");
    }

    private IEnumerator AutoStartWithDelay()
    {
        if (autoStartDelay > 0f)
            yield return new WaitForSeconds(autoStartDelay);
        StartDriving();
    }

    public void StartDriving()
    {
        if (isDriving || waypoints == null || waypoints.Length == 0) return;

        isDriving = true;

        StartEngineSound();

        if (showDebugInfo)
            Debug.Log("자율주행 시작!");
    }

    private void StartEngineSound()
    {
        if (engineAudio == null || engineLoopClip == null) return;

        if (engineFadeCoroutine != null)
        {
            StopCoroutine(engineFadeCoroutine);
            engineFadeCoroutine = null;
        }

        engineAudio.clip = engineLoopClip;
        engineAudio.volume = 1f;
        if (!engineAudio.isPlaying)
            engineAudio.Play();
    }

    private void StopEngineSound()
    {
        if (engineAudio == null || !engineAudio.isPlaying) return;

        if (engineFadeOutTime <= 0f)
        {
            engineAudio.Stop();
            return;
        }

        if (engineFadeCoroutine != null)
            StopCoroutine(engineFadeCoroutine);
        engineFadeCoroutine = StartCoroutine(FadeOutEngine());
    }

    private IEnumerator FadeOutEngine()
    {
        float startVolume = engineAudio.volume;
        float t = 0f;
        while (t < engineFadeOutTime)
        {
            t += Time.deltaTime;
            engineAudio.volume = Mathf.Lerp(startVolume, 0f, t / engineFadeOutTime);
            yield return null;
        }
        engineAudio.Stop();
        engineAudio.volume = startVolume;
        engineFadeCoroutine = null;
    }

    // VR 리그가 자식일 때: 물리 스텝에서 이동해야 Interpolate 로 부드럽게 렌더된다.
    void FixedUpdate()
    {
        if (smoothForVR)
            DriveTick(Time.fixedDeltaTime);
    }

    // smoothForVR 를 끈 경우에만 기존 방식(Update 이동) 사용.
    void Update()
    {
        if (!smoothForVR)
            DriveTick(Time.deltaTime);
    }

    private void DriveTick(float dt)
    {
        if (!isDriving) return;

        if (waypoints == null || currentWaypointIndex >= waypoints.Length)
        {
            FinishDriving();
            return;
        }

        Transform target = waypoints[currentWaypointIndex];
        if (target == null)
        {
            currentWaypointIndex++;
            return;
        }

        bool useRb = smoothForVR && rb != null;
        Vector3 curPos = useRb ? rb.position : transform.position;
        Quaternion curRot = useRb ? rb.rotation : transform.rotation;

        // ── 이동 ──
        Vector3 newPos = Vector3.MoveTowards(curPos, target.position, moveSpeed * dt);

        // ── 회전 (Y축) : RotateTowards 로 수렴, 웨이포인트 근처 방향 노이즈는 무시 ──
        Quaternion newRot = curRot;
        if (rotateWhileDriving)
        {
            Vector3 flatDir = target.position - curPos;
            flatDir.y = 0f;

            // 목표에 충분히 멀 때만 원하는 방향을 갱신 (가까우면 방향 벡터가 요동쳐서 빙글 도는 것 방지)
            if (flatDir.sqrMagnitude > 0.0004f && flatDir.magnitude > waypointReachDistance * 1.5f)
                desiredYaw = Quaternion.LookRotation(flatDir.normalized).eulerAngles.y + rotationYOffset;

            float nextYaw = Mathf.MoveTowardsAngle(curRot.eulerAngles.y, desiredYaw, rotateSpeed * dt);
            newRot = Quaternion.Euler(0f, nextYaw, 0f);
        }

        ApplyMove(newPos, newRot, useRb);

        // ── 웨이포인트 도달 ──
        if (Vector3.Distance(newPos, target.position) <= waypointReachDistance)
        {
            currentWaypointIndex++;
            if (showDebugInfo)
                Debug.Log($"웨이포인트 {currentWaypointIndex} 도달");

            if (currentWaypointIndex >= waypoints.Length)
                FinishDriving();
        }
    }

    private void ApplyMove(Vector3 pos, Quaternion rot, bool useRb)
    {
        if (useRb)
        {
            rb.MovePosition(pos);
            rb.MoveRotation(rot);
        }
        else
        {
            transform.position = pos;
            transform.rotation = rot;
        }
    }

    private void FinishDriving()
    {
        if (!isDriving) return;

        isDriving = false;
        StopEngineSound();
        StartCoroutine(ExitCarAfterDelay());

        if (showDebugInfo)
            Debug.Log("목적지 도착!");
    }

    IEnumerator ExitCarAfterDelay()
    {
        yield return new WaitForSeconds(exitDelay);

        // 플레이어를 트렁크 앞 위치에 텔레포트
        if (playerTransform != null && playerExitPoints != null && selectedVisitIndex < playerExitPoints.Length)
        {
            Transform exitPoint = playerExitPoints[selectedVisitIndex];
            if (exitPoint != null)
                playerTransform.position = exitPoint.position;
        }

        onDrivingComplete?.Invoke();
    }

    void OnDrawGizmosSelected()
    {
        if (waypointSets == null) return;

        Color[] colors = { Color.yellow, Color.cyan, Color.magenta };

        for (int s = 0; s < waypointSets.Length; s++)
        {
            Transform[] wps = waypointSets[s]?.waypoints;
            if (wps == null) continue;

            Gizmos.color = colors[s % colors.Length];
            for (int i = 0; i < wps.Length - 1; i++)
            {
                if (wps[i] != null && wps[i + 1] != null)
                    Gizmos.DrawLine(wps[i].position, wps[i + 1].position);
            }
            foreach (var wp in wps)
            {
                if (wp != null)
                    Gizmos.DrawWireSphere(wp.position, 0.3f);
            }
        }
    }
}
