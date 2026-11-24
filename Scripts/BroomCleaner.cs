using UnityEngine;
using UnityEngine.XR;
using UnityEngine.Rendering.Universal;

public class BroomCleaner : MonoBehaviour
{
    [Header("청소 설정")]
    [Tooltip("한 번 닦을 때마다 투명해지는 정도 (0~1)")]
    public float cleanPercentage = 0.5f;

    [Tooltip("완전히 청소하는 데 필요한 횟수")]
    public int totalCleanCount = 2;

    [Header("레이캐스트 설정")]
    [Tooltip("청소 감지 기준점 (빗자루 헤드 오브젝트). 비워두면 이 컴포넌트의 위치 사용")]
    public Transform cleaningPoint;

    [Tooltip("빗자루가 닿을 수 있는 최대 거리")]
    public float reachDistance = 1.0f;

    [Header("컨트롤러 설정")]
    [Tooltip("오른손 컨트롤러 Transform")]
    public Transform rightHandController;
    public InputDeviceCharacteristics controllerCharacteristics =
        InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;

    [Header("잡기 설정")]
    [Tooltip("트리거를 얼마나 눌러야 '누른' 것으로 인정할지 (0~1)")]
    public float triggerThreshold = 0.7f;

    [Tooltip("그립을 얼마나 눌러야 '누른' 것으로 인정할지 (0~1)")]
    public float gripThreshold = 0.7f;

    [Header("쓸기 감지 설정")]
    [Tooltip("좌/우로 움직이는 속도 임계값 (m/s)")]
    public float sweepVelocityThreshold = 0.4f;

    [Tooltip("방향 변경 시 필요한 최소 시간 (초)")]
    public float directionChangeDelay = 0.25f;

    [Tooltip("왕복으로 인정하는 방향 반전 내적 임계값 (-1~0, -0.3이면 120도 이상 꺾였을 때 인식, 곡선 허용)")]
    [SerializeField] private float sweepReverseDot = -0.3f;

    [Header("ToolSwitcher 연동")]
    [Tooltip("ToolSwitcher 스크립트")]
    public ToolSwitcher toolSwitcher;

    [Header("디버그")]
    [Tooltip("콘솔에 청소 로그 표시")]
    public bool showDebugInfo = true;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sweepSound;
    private Coroutine stopAudioCoroutine;

    // 각 먼지의 청소 상태를 저장하는 딕셔너리
    private System.Collections.Generic.Dictionary<GameObject, DustData> stainProgress =
        new System.Collections.Generic.Dictionary<GameObject, DustData>();

    private InputDevice targetDevice;
    private bool isGripping = false;
    private MoneyManager moneyManager;

    // 튜토리얼
    private bool reportedTutorial = false;
    private TutorialProgressTracker tutorialTracker;
    private TutorialCleaningGuide cleaningGuide;
    private float lastWrongToolHintTime = -999f;
    public float wrongToolHintCooldown = 3f;

    // 쓸기 방향 감지 변수
    private enum SweepDirection { None, Left, Right }
    private SweepDirection currentDirection = SweepDirection.None;
    private SweepDirection lastDirection = SweepDirection.None;
    private float directionChangeTimer = 0f;
    private GameObject currentDustBeingCleaned = null;
    private Vector3 lastSweepVelocity = Vector3.zero; // 이전 유효 수평 속도 벡터

    void Start()
    {
        if (rightHandController == null)
        {
            rightHandController = FindRightHandController();
        }

        if (rightHandController == null)
        {
            Debug.LogError("오른손 컨트롤러를 찾을 수 없습니다!");
        }

        if (toolSwitcher == null)
        {
            toolSwitcher = FindFirstObjectByType<ToolSwitcher>();
        }

        if (toolSwitcher == null)
        {
            Debug.LogError("ToolSwitcher를 찾을 수 없습니다!");
        }

        InitializeController();
        tutorialTracker = FindFirstObjectByType<TutorialProgressTracker>();
        moneyManager = FindFirstObjectByType<MoneyManager>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    Transform FindRightHandController()
    {
        GameObject xrOrigin = GameObject.Find("XR Origin");
        if (xrOrigin != null)
        {
            Transform cameraOffset = xrOrigin.transform.Find("Camera Offset");
            if (cameraOffset != null)
            {
                Transform rightHand = cameraOffset.Find("RightHand Controller");
                if (rightHand == null) rightHand = cameraOffset.Find("Right Hand Controller");
                if (rightHand == null) rightHand = cameraOffset.Find("Right Controller");

                if (rightHand != null)
                {
                    if (showDebugInfo)
                    {
                        Debug.Log("오른손 컨트롤러 자동 발견: " + rightHand.name);
                    }
                    return rightHand;
                }
            }
        }
        return null;
    }

    void InitializeController()
    {
        var devices = new System.Collections.Generic.List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(controllerCharacteristics, devices);

        if (devices.Count > 0)
        {
            targetDevice = devices[0];
            if (showDebugInfo)
            {
                Debug.Log("컨트롤러 연결됨: " + targetDevice.name);
            }
        }
    }

    void Update()
    {
        if (!targetDevice.isValid)
        {
            InitializeController();
        }

        bool isHoldingBroom = IsHoldingBroom();

        if (isHoldingBroom)
        {
            CheckGripInput();

            if (isGripping)
            {
                CheckForDustContact();
            }
            else
            {
                // 그립을 놓으면 청소 진행 상태 초기화
                currentDustBeingCleaned = null;
                currentDirection = SweepDirection.None;
                lastDirection = SweepDirection.None;
                lastSweepVelocity = Vector3.zero;
            }
        }

        // 타이머 감소
        if (directionChangeTimer > 0)
        {
            directionChangeTimer -= Time.deltaTime;
        }
    }

    bool IsHoldingBroom()
    {
        if (toolSwitcher == null)
            return false;

        string currentTool = toolSwitcher.GetCurrentToolTag();
        return currentTool == "Broom";
    }

    void CheckGripInput()
    {
        if (!targetDevice.isValid)
            return;

        float triggerValue = 0f;
        targetDevice.TryGetFeatureValue(CommonUsages.trigger, out triggerValue);

        float gripValue = 0f;
        targetDevice.TryGetFeatureValue(CommonUsages.grip, out gripValue);

        bool wasPreviouslyGripping = isGripping;
        isGripping = (triggerValue >= triggerThreshold) && (gripValue >= gripThreshold);

        if (isGripping && !wasPreviouslyGripping)
        {
            if (targetDevice.isValid)
            {
                targetDevice.SendHapticImpulse(0, 0.3f, 0.1f);
            }

            if (showDebugInfo)
            {
                Debug.Log("빗자루 잡음!");
            }
        }
        else if (!isGripping && wasPreviouslyGripping)
        {
            if (showDebugInfo)
                Debug.Log("빗자루 놓음!");

            if (audioSource != null && audioSource.isPlaying)
            {
                if (stopAudioCoroutine != null) StopCoroutine(stopAudioCoroutine);
                stopAudioCoroutine = StartCoroutine(StopAfterDelay(audioSource, 0.2f));
            }
        }
    }

    void CheckForDustContact()
    {
        Collider[] hits = Physics.OverlapSphere(CleaningOrigin, reachDistance);

        GameObject nearestDust = null;
        float nearestDistance = float.MaxValue;

        // 가장 가까운 먼지 찾기
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Cleanable_Broom"))
            {
                float distance = Vector3.Distance(CleaningOrigin, hit.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestDust = hit.gameObject;
                }
            }
        }

        if (nearestDust != null)
        {
            // 다른 먼지로 바뀌었을 때 리셋
            if (currentDustBeingCleaned != nearestDust)
            {
                currentDustBeingCleaned = nearestDust;
                currentDirection = SweepDirection.None;
                lastDirection = SweepDirection.None;
                lastSweepVelocity = Vector3.zero;

                if (showDebugInfo)
                {
                    Debug.Log($"새 먼지 감지: {nearestDust.name}");
                }
            }

            // 쓸기 동작 감지
            DetectSweepMotion(nearestDust);
        }
        else
        {
            currentDustBeingCleaned = null;
            currentDirection = SweepDirection.None;
            lastDirection = SweepDirection.None;
            lastSweepVelocity = Vector3.zero;

            // 잘못된 도구 감지 (빗자루로 스펀지/대걸레 대상 청소 시도)
            if (isGripping && tutorialTracker != null
                && Time.time - lastWrongToolHintTime >= wrongToolHintCooldown)
            {
                foreach (Collider hit in hits)
                {
                    if (hit.CompareTag("Cleanable_Sponge"))
                    {
                        lastWrongToolHintTime = Time.time;
                        tutorialTracker.PlayWrongToolHint(ToolPickup.ToolType.Sponge);
                        break;
                    }
                    if (hit.CompareTag("Cleanable_Mop"))
                    {
                        lastWrongToolHintTime = Time.time;
                        tutorialTracker.PlayWrongToolHint(ToolPickup.ToolType.Mop);
                        break;
                    }
                }
            }
        }
    }

    void DetectSweepMotion(GameObject dust)
    {
        if (!targetDevice.isValid)
            return;

        Vector3 velocity = Vector3.zero;
        targetDevice.TryGetFeatureValue(CommonUsages.deviceVelocity, out velocity);

        // 월드 공간 수평(XZ) 속도만 사용 — 컨트롤러 쥔 방향과 무관, Y 제거로 곡선 동작 허용
        Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);
        float speed = horizontalVel.magnitude;

        if (showDebugInfo && Time.frameCount % 30 == 0)
            Debug.Log($"수평 속도: {speed:F3} m/s");

        if (speed >= sweepVelocityThreshold)
        {
            if (lastSweepVelocity.magnitude >= sweepVelocityThreshold)
            {
                float dot = Vector3.Dot(lastSweepVelocity.normalized, horizontalVel.normalized);

                // 이전 방향과 현재 방향이 충분히 반대이면 왕복 1세트 완료
                if (directionChangeTimer <= 0 && dot < sweepReverseDot)
                {
                    lastSweepVelocity = Vector3.zero;
                    directionChangeTimer = directionChangeDelay;
                    OnSweepCycleComplete(dust);
                    return;
                }
            }
            lastSweepVelocity = horizontalVel;
        }
    }

    void OnSweepCycleComplete(GameObject dust)
    {
        if (showDebugInfo)
            Debug.Log("쓸기 한 세트 완료! (청소)");

        cleaningGuide ??= FindFirstObjectByType<TutorialCleaningGuide>();
        if (cleaningGuide != null && cleaningGuide.IsWrongTool(ToolPickup.ToolType.Broom))
        {
            cleaningGuide.PlayCurrentStepHint();
            return;
        }

        if (audioSource != null && sweepSound != null && !audioSource.isPlaying)
        {
            if (stopAudioCoroutine != null) { StopCoroutine(stopAudioCoroutine); stopAudioCoroutine = null; }
            audioSource.PlayOneShot(sweepSound);
        }

        // 먼지 청소 진행
        CleanDust(dust);
    }

    void CleanDust(GameObject dust)
    {
        // 각 먼지의 청소 데이터가 없으면 새로 만들기
        if (!stainProgress.ContainsKey(dust))
        {
            stainProgress[dust] = new DustData(dust);
        }

        DustData data = stainProgress[dust];

        // 이미 완전히 청소됐으면 스킵
        if (data.isFullyCleaned)
        {
            return;
        }

        // 청소 횟수 증가
        data.cleanCount++;

        if (data.renderer != null)
        {
            // 일반 Renderer — 단계적으로 투명하게
            float newAlpha = Mathf.Max(0f, data.initialAlpha * (1f - cleanPercentage * data.cleanCount));
            Color color = data.renderer.material.color;
            color.a = newAlpha;
            data.renderer.material.color = color;
            SetMaterialTransparent(data.renderer.material);
        }
        else if (data.decalProjector != null)
        {
            // URP Decal Projector — fadeFactor 감소
            float newAlpha = Mathf.Max(0f, data.initialAlpha * (1f - cleanPercentage * data.cleanCount));
            data.decalProjector.fadeFactor = newAlpha;
        }
        // 비주얼 없는 일반 오브젝트 — 횟수만 카운트 후 제거

        // 컨트롤러 햅틱 피드백
        if (targetDevice.isValid)
        {
            targetDevice.SendHapticImpulse(0, 0.3f, 0.1f);
        }

        if (showDebugInfo)
            Debug.Log($"청소 중... ({data.cleanCount}/{totalCleanCount}) [{dust.name}]");

        // 완전히 청소되면 제거
        if (data.cleanCount >= totalCleanCount)
        {
            data.isFullyCleaned = true;
            if (!reportedTutorial)
            {
                reportedTutorial = true;
                FindFirstObjectByType<TutorialProgressTracker>()?.OnToolUsed(ToolPickup.ToolType.Broom);
            }
            if (audioSource != null && audioSource.isPlaying)
            {
                if (stopAudioCoroutine != null) StopCoroutine(stopAudioCoroutine);
                stopAudioCoroutine = StartCoroutine(StopAfterDelay(audioSource, 0.2f));
            }

            if (moneyManager != null)
                moneyManager.OnDustCleaned();

            if (showDebugInfo)
            {
                Debug.Log("먼지 완전 제거!");
            }

            // 강한 햅틱 피드백
            if (targetDevice.isValid)
            {
                targetDevice.SendHapticImpulse(0, 0.8f, 0.3f);
            }

            // 먼지 오브젝트 제거
            Destroy(dust);

            // 딕셔너리에서 제거
            stainProgress.Remove(dust);

            // 현재 청소 중인 먼지 초기화
            currentDustBeingCleaned = null;
        }
    }

    // Material의 Transparent 설정 적용
    void SetMaterialTransparent(Material material)
    {
        // Standard Shader의 경우
        if (material.shader.name.Contains("Standard"))
        {
            material.SetFloat("_Mode", 3); // Transparent
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
        }
        // URP Lit Shader의 경우
        else if (material.shader.name.Contains("Universal Render Pipeline/Lit"))
        {
            material.SetFloat("_Surface", 1); // Transparent
            material.SetFloat("_Blend", 0); // Alpha
            material.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.renderQueue = 3000;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        }
    }

    Vector3 CleaningOrigin => cleaningPoint != null ? cleaningPoint.position : transform.position;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(CleaningOrigin, reachDistance);
    }

    System.Collections.IEnumerator StopAfterDelay(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        source.Stop();
    }
}

// 각 먼지의 청소 상태를 저장하는 클래스
public class DustData
{
    public GameObject stainObject;
    public Renderer renderer;
    public DecalProjector decalProjector;
    public float initialAlpha;
    public int cleanCount = 0;
    public bool isFullyCleaned = false;

    public DustData(GameObject dust)
    {
        stainObject = dust;
        renderer = dust.GetComponent<Renderer>();
        if (renderer == null)
            decalProjector = dust.GetComponent<DecalProjector>();

        if (renderer != null)
            initialAlpha = renderer.material.color.a;
        else if (decalProjector != null)
            initialAlpha = decalProjector.fadeFactor;
        else
            initialAlpha = 1f;
    }

    public bool HasVisual => renderer != null || decalProjector != null;
}
