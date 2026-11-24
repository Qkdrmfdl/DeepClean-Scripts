using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

public class MopCleaner : MonoBehaviour
{
    [Header("청소 설정")]
    [Tooltip("이 도구로 청소 가능한 오브젝트의 태그")]
    [SerializeField] private string cleanableTag = "Cleanable_Mop";

    [Tooltip("ToolSwitcher에서 이 도구를 식별하는 태그 prefix (StartsWith 비교)")]
    [SerializeField] private string toolTagPrefix = "Mop";

    [Tooltip("한 번 닦을 때마다 투명해지는 정도 (0~1)")]
    public float cleanPercentage = 0.3f;

    [Tooltip("완전히 청소하는 데 필요한 횟수")]
    public int totalCleanCount = 3;

    [Tooltip("URP Decal이 아닌, 일반 Renderer 머티리얼(알파값)로 표현된 얼룩의 색상 속성 이름")]
    public string stainAlphaPropertyName = "_BaseColor";

    [Header("대걸레 헤드 더러워지기 설정")]
    [Tooltip("대걸레 헤드 머티리얼 (curly_teddy_natural_diff_1k)")]
    public Material mopHeadMaterial;

    [Tooltip("대걸레 헤드가 있는 오브젝트의 Renderer")]
    public Renderer mopHeadRenderer;

    [Tooltip("깨끗한 상태의 색상")]
    public Color cleanColor = Color.white;

    [Tooltip("최대로 더러운 상태의 색상 (#5A5957)")]
    public Color dirtyColor = new Color(0.35f, 0.35f, 0.34f, 1f);

    [Tooltip("완전히 더러워지는 데 필요한 청소 횟수")]
    public int maxDirtyCount = 18;

    [Tooltip("머티리얼 색상 속성 이름")]
    public string colorPropertyName = "_BaseColor";

    [Tooltip("Renderer의 머티리얼 인덱스 (보통 0)")]
    public int materialIndex = 0;

    [Header("더러운 대걸레 얼룩 생성 설정")]
    [Tooltip("생성할 데칼 머티리얼들 (3개)")]
    public Material[] stainDecalMaterials = new Material[3];

    [Tooltip("생성될 데칼 최소 크기")]
    public Vector3 minDecalSize = new Vector3(0.3f, 0.3f, 0.5f);

    [Tooltip("생성될 데칼 최대 크기")]
    public Vector3 maxDecalSize = new Vector3(0.6f, 0.6f, 0.5f);

    [Tooltip("데칼이 생성될 레이어")]
    public LayerMask floorLayer = -1;

    [Tooltip("데칼 생성 시 바닥까지의 최대 거리")]
    public float raycastDistance = 2.0f;

    [Header("레이캐스트 설정")]
    [Tooltip("청소 감지 기준점 (대걸레 헤드 오브젝트). 비워두면 이 컴포넌트의 위치 사용")]
    public Transform cleaningPoint;

    [Tooltip("대걸레가 닿을 수 있는 최대 거리")]
    public float reachDistance = 0.8f;

    [Header("컨트롤러 설정")]
    [Tooltip("오른손 컨트롤러 Transform")]
    public Transform rightHandController;

    public InputDeviceCharacteristics controllerCharacteristics =
        InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;

    [Header("잡기 설정")]
    [Tooltip("트리거 임계값 (0~1)")]
    public float triggerThreshold = 0.7f;

    [Tooltip("그립 임계값 (0~1)")]
    public float gripThreshold = 0.7f;

    [Header("밀기 감지 설정")]
    [Tooltip("앞/뒤 속도 임계값 (m/s)")]
    public float pushVelocityThreshold = 0.35f;

    [Tooltip("방향 변경 최소 시간 (초)")]
    public float directionChangeDelay = 0.2f;

    [Header("대걸레 씻기 설정")]
    [Tooltip("위/아래 속도 임계값 (m/s)")]
    public float washVelocityThreshold = 0.35f;

    [Tooltip("대걸레를 완전히 씻는 데 필요한 제스처 횟수")]
    public int totalWashCount = 3;

    [Tooltip("씻기 방향 변경 최소 시간 (초)")]
    public float washDirectionChangeDelay = 0.2f;

    [Header("연동")]
    [Tooltip("ToolSwitcher 스크립트")]
    public ToolSwitcher toolSwitcher;

    [Header("양동이 연동")]
    [Tooltip("BucketWaterSystem 스크립트 (자동 검색)")]
    public WaterSystem bucketWaterSystem;

    [Header("디버그")]
    [Tooltip("디버그 로그 표시")]
    public bool showDebugInfo = false;

    [Header("Sound")]
    [SerializeField] private AudioSource mopAudioSource;
    [SerializeField] private AudioClip mopSound;
    [SerializeField] private AudioSource washAudioSource;
    [SerializeField] private AudioClip washSound;
    private bool isMopping = false;
    private float mopStopTimer = 0f;
    [Tooltip("닦기를 멈춘 뒤 이 시간(초) 안에 다시 닦기 시작하면 효과음이 끊기지 않고 이어서 재생됨.\n" +
             "청소기처럼 몇 초 안에 계속 청소하면 매번 다시 재생되지 않고 자연스럽게 이어지도록 값을 늘림")]
    [SerializeField] private float mopStopDelay = 2f;

    // ===== Private Variables =====
    // 얼룩 청소
    private System.Collections.Generic.Dictionary<GameObject, StainData> stainProgress =
        new System.Collections.Generic.Dictionary<GameObject, StainData>();
    private GameObject currentStainBeingCleaned = null;

    // 컨트롤러
    private InputDevice targetDevice;
    private bool isGripping = false;

    // 밀기 동작
    private enum PushDirection { None, Forward, Backward }
    private PushDirection currentDirection = PushDirection.None;
    private PushDirection lastDirection = PushDirection.None;
    private float directionChangeTimer = 0f;
    private Vector3 lastMopVelocity = Vector3.zero; // 이전 유효 수평 속도 벡터

    // 대걸레 헤드 더러워지기
    private int totalCleanActions = 0;
    private Material mopHeadMaterialInstance; // 인스턴스 (원본 보호)
    private Color originalMopColor;

    private MoneyManager moneyManager;

    // 튜토리얼
    private bool reportedTutorial = false;
    private TutorialProgressTracker tutorialTracker;
    private TutorialCleaningGuide cleaningGuide;
    private float lastWrongToolHintTime = -999f;
    public float wrongToolHintCooldown = 3f;

    // 대걸레 씻기
    private enum WashDirection { None, Up, Down }
    private WashDirection currentWashDirection = WashDirection.None;
    private WashDirection lastWashDirection = WashDirection.None;
    private float washDirectionChangeTimer = 0f;
    private int currentWashCount = 0;
    private bool isInWater = false;
    private GameObject currentWaterObject = null;

    // ===== Unity Lifecycle =====
    void Start()
    {
        InitializeController();
        InitializeRightHandController();
        InitializeToolSwitcher();
        InitializeMopHead();
        InitializeBucketWaterSystem();
        tutorialTracker = FindFirstObjectByType<TutorialProgressTracker>();
        moneyManager = FindFirstObjectByType<MoneyManager>();

        AudioSource[] sources = GetComponents<AudioSource>();
        if (mopAudioSource == null)
            mopAudioSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
        if (washAudioSource == null)
            washAudioSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {
        // 컨트롤러 재연결
        if (!targetDevice.isValid)
            InitializeController();

        // 타이머 감소
        if (directionChangeTimer > 0)
            directionChangeTimer -= Time.deltaTime;

        if (washDirectionChangeTimer > 0)
            washDirectionChangeTimer -= Time.deltaTime;

        if (mopStopTimer > 0f)
        {
            mopStopTimer -= Time.deltaTime;
            if (mopStopTimer <= 0f) { isMopping = false; mopAudioSource?.Stop(); }
        }

        // 대걸레를 들고 있지 않으면 리턴
        if (!IsHoldingMop())
        {
            ResetCleaningState();
            ResetWashingState();
            return;
        }

        // 잡기 확인
        CheckGripInput();

        // 잡고 있을 때만 작업
        if (isGripping)
        {
            // 물과의 충돌 확인
            CheckWaterContact();

            // 물 안에 있으면 씻기 제스처 감지
            if (isInWater)
            {
                DetectWashMotion();
            }
            else
            {
                // 물 밖에서는 청소 작업
                CheckForStainContact();
            }
        }
        else
        {
            ResetCleaningState();
            ResetWashingState();
        }
    }

    void OnDestroy()
    {
        // 원본 머티리얼 색상 복원
        RestoreMopHeadMaterial();
    }

    Vector3 CleaningOrigin => cleaningPoint != null ? cleaningPoint.position : transform.position;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(CleaningOrigin, reachDistance);
    }

    // ===== Initialization =====
    void InitializeController()
    {
        var devices = new System.Collections.Generic.List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(controllerCharacteristics, devices);

        if (devices.Count > 0)
        {
            targetDevice = devices[0];
            if (showDebugInfo)
                Debug.Log("컨트롤러 연결: " + targetDevice.name);
        }
    }

    void InitializeRightHandController()
    {
        if (rightHandController == null)
            rightHandController = FindRightHandController();

        if (rightHandController == null)
            Debug.LogError("오른손 컨트롤러를 찾을 수 없습니다!");
    }

    void InitializeToolSwitcher()
    {
        if (toolSwitcher == null)
            toolSwitcher = FindFirstObjectByType<ToolSwitcher>();

        if (toolSwitcher == null)
            Debug.LogError("ToolSwitcher를 찾을 수 없습니다!");
    }

    void InitializeBucketWaterSystem()
    {
        if (bucketWaterSystem == null)
            bucketWaterSystem = FindFirstObjectByType<WaterSystem>();

        if (bucketWaterSystem == null)
        {
            Debug.LogWarning("BucketWaterSystem을 찾을 수 없습니다! 양동이 헹굼 기능이 제한될 수 있습니다.");
        }
        else if (showDebugInfo)
        {
            Debug.Log("BucketWaterSystem 연동 완료!");
        }
    }

    void InitializeMopHead()
    {
        if (mopHeadMaterial == null)
        {
            Debug.LogWarning("대걸레 머티리얼 미할당! 'curly_teddy_natural_diff_1k'를 드래그하세요.");
            return;
        }

        // Renderer 자동 찾기
        if (mopHeadRenderer == null)
        {
            mopHeadRenderer = GetComponentInChildren<Renderer>();

            if (mopHeadRenderer == null)
            {
                Debug.LogWarning("대걸레 Renderer를 찾을 수 없습니다! Inspector에서 할당하세요.");
                return;
            }

            if (showDebugInfo)
                Debug.Log($"Renderer 자동 발견: {mopHeadRenderer.gameObject.name}");
        }

        // 머티리얼 인스턴스 생성 (원본 보호)
        mopHeadMaterialInstance = new Material(mopHeadMaterial);

        // Renderer에 인스턴스 머티리얼 할당
        Material[] materials = mopHeadRenderer.materials;

        if (materialIndex >= 0 && materialIndex < materials.Length)
        {
            materials[materialIndex] = mopHeadMaterialInstance;
            mopHeadRenderer.materials = materials;

            if (showDebugInfo)
                Debug.Log($"인스턴스 머티리얼 할당 완료! (인덱스: {materialIndex})");
        }
        else
        {
            Debug.LogWarning($"잘못된 머티리얼 인덱스: {materialIndex} (총 {materials.Length}개)");
            return;
        }

        // 색상 속성 찾기 및 저장
        if (mopHeadMaterialInstance.HasProperty(colorPropertyName))
        {
            originalMopColor = mopHeadMaterialInstance.GetColor(colorPropertyName);
            cleanColor = originalMopColor;

            if (showDebugInfo)
                Debug.Log($"대걸레 머티리얼 초기화! 원본 색상: {originalMopColor}");
        }
        else if (TryFindColorProperty())
        {
            originalMopColor = mopHeadMaterialInstance.GetColor(colorPropertyName);
            cleanColor = originalMopColor;

            if (showDebugInfo)
                Debug.Log($"색상 속성 발견: {colorPropertyName}");
        }
        else
        {
            Debug.LogWarning($"색상 속성을 찾을 수 없습니다!");
        }
    }

    bool TryFindColorProperty()
    {
        // 일반적인 색상 속성 이름들 시도
        string[] propertyNames = { "_Color", "_BaseColor", "_MainColor" };

        foreach (string propName in propertyNames)
        {
            if (mopHeadMaterialInstance.HasProperty(propName))
            {
                colorPropertyName = propName;
                return true;
            }
        }

        return false;
    }

    Transform FindRightHandController()
    {
        GameObject xrOrigin = GameObject.Find("XR Origin (XR Rig)");
        if (xrOrigin == null)
            xrOrigin = GameObject.Find("XR Origin");
        if (xrOrigin == null)
            return null;

        Transform cameraOffset = xrOrigin.transform.Find("Camera Offset");
        if (cameraOffset == null)
            return null;

        // 가능한 이름들 시도
        string[] possibleNames = { "RightHand Controller", "Right Hand Controller", "Right Controller" };

        foreach (string name in possibleNames)
        {
            Transform hand = cameraOffset.Find(name);
            if (hand != null)
            {
                if (showDebugInfo)
                    Debug.Log("오른손 컨트롤러 발견: " + hand.name);
                return hand;
            }
        }

        return null;
    }

    void RestoreMopHeadMaterial()
    {
        // Renderer에 원본 머티리얼 다시 할당
        if (mopHeadRenderer != null && mopHeadMaterial != null)
        {
            Material[] materials = mopHeadRenderer.materials;

            if (materialIndex >= 0 && materialIndex < materials.Length)
            {
                materials[materialIndex] = mopHeadMaterial;
                mopHeadRenderer.materials = materials;

                if (showDebugInfo)
                    Debug.Log("원본 머티리얼 복원!");
            }
        }

        // 인스턴스 머티리얼 메모리 해제
        if (mopHeadMaterialInstance != null)
        {
            Destroy(mopHeadMaterialInstance);
            mopHeadMaterialInstance = null;
        }
    }

    // ===== Input Handling =====
    bool IsHoldingMop()
    {
        if (toolSwitcher == null)
            return false;

        string currentTool = toolSwitcher.GetCurrentToolTag();
        return currentTool.StartsWith(toolTagPrefix);
    }

    void CheckGripInput()
    {
        if (!targetDevice.isValid)
            return;

        // 트리거 & 그립 값 가져오기
        targetDevice.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue);
        targetDevice.TryGetFeatureValue(CommonUsages.grip, out float gripValue);

        bool wasPreviouslyGripping = isGripping;
        isGripping = (triggerValue >= triggerThreshold) && (gripValue >= gripThreshold);

        // 잡는 순간 햅틱
        if (isGripping && !wasPreviouslyGripping)
        {
            if (targetDevice.isValid)
                targetDevice.SendHapticImpulse(0, 0.3f, 0.1f);

            if (showDebugInfo)
                Debug.Log("대걸레 잡음!");
        }
        else if (!isGripping && wasPreviouslyGripping)
        {
            if (showDebugInfo)
                Debug.Log("대걸레 놓음!");
            SetMopAudio(false);
        }
    }

    void SetMopAudio(bool playing)
    {
        if (mopAudioSource == null || mopSound == null) return;
        if (playing)
        {
            mopStopTimer = 0f;
            if (!isMopping)
            {
                isMopping = true;
                mopAudioSource.clip = mopSound;
                mopAudioSource.loop = true;
                mopAudioSource.Play();
            }
        }
        else if (isMopping && mopStopTimer <= 0f)
        {
            mopStopTimer = mopStopDelay;
        }
    }

    void ResetCleaningState()
    {
        currentStainBeingCleaned = null;
        currentDirection = PushDirection.None;
        lastDirection = PushDirection.None;
        lastMopVelocity = Vector3.zero;
        SetMopAudio(false);
    }

    void ResetWashingState()
    {
        currentWashDirection = WashDirection.None;
        lastWashDirection = WashDirection.None;
        currentWashCount = 0;
        isInWater = false;
        currentWaterObject = null;
        washAudioSource?.Stop();
    }

    // ===== Cleaning Logic =====
    void CheckForStainContact()
    {
        Collider[] hits = Physics.OverlapSphere(CleaningOrigin, reachDistance);

        // 가장 가까운 얼룩 찾기
        GameObject nearestStain = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag(cleanableTag))
            {
                float distance = Vector3.Distance(CleaningOrigin, hit.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestStain = hit.gameObject;
                }
            }
        }

        // 얼룩 발견
        if (nearestStain != null)
        {
            // 다른 얼룩으로 바뀌면 리셋
            if (currentStainBeingCleaned != nearestStain)
            {
                currentStainBeingCleaned = nearestStain;
                currentDirection = PushDirection.None;
                lastDirection = PushDirection.None;

                if (showDebugInfo)
                    Debug.Log($"얼룩 감지: {nearestStain.name}");
            }

            DetectPushMotion(nearestStain);
        }
        else
        {
            ResetCleaningState();

            // 잘못된 도구 감지 (대걸레로 스펀지/빗자루 대상 청소 시도)
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
                    if (hit.CompareTag("Cleanable_Broom"))
                    {
                        lastWrongToolHintTime = Time.time;
                        tutorialTracker.PlayWrongToolHint(ToolPickup.ToolType.Broom);
                        break;
                    }
                }
            }
        }
    }

    void DetectPushMotion(GameObject stain)
    {
        if (!targetDevice.isValid)
            return;

        targetDevice.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vector3 velocity);

        // 월드 공간 수평(XZ) 속도만 사용 — 컨트롤러 쥔 방향과 무관, 곡선 동작 허용
        Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);
        float speed = horizontalVel.magnitude;

        if (showDebugInfo && Time.frameCount % 30 == 0)
            Debug.Log($"속도: {speed:F3} m/s");

        SetMopAudio(speed > pushVelocityThreshold);

        if (speed >= pushVelocityThreshold)
        {
            if (lastMopVelocity.magnitude >= pushVelocityThreshold)
            {
                float dot = Vector3.Dot(lastMopVelocity.normalized, horizontalVel.normalized);

                // 이전 방향과 현재 방향이 충분히 반대이면 왕복 1세트 완료
                if (directionChangeTimer <= 0 && dot < -0.3f)
                {
                    lastMopVelocity = Vector3.zero;
                    directionChangeTimer = directionChangeDelay;
                    OnPushCycleComplete(stain);
                    return;
                }
            }
            lastMopVelocity = horizontalVel;
        }
    }

    void OnPushCycleComplete(GameObject stain)
    {
        if (showDebugInfo)
            Debug.Log("밀기 한 세트 완료!");

        cleaningGuide ??= FindFirstObjectByType<TutorialCleaningGuide>();
        if (cleaningGuide != null && cleaningGuide.IsWrongTool(ToolPickup.ToolType.Mop))
        {
            cleaningGuide.PlayCurrentStepHint();
            return;
        }

        // 18번 이상 청소했으면 청소 차단 & 얼룩 생성
        if (totalCleanActions >= maxDirtyCount)
        {
            if (showDebugInfo)
                Debug.Log("대걸레가 너무 더러워서 청소할 수 없습니다! 얼룩만 생성됩니다.");

            CreateDirtyStain();

            // 청소는 진행하지 않음
            return;
        }

        // 18번 이하일 때만 청소 진행
        CleanStain(stain);
        DirtyMopHead();
    }

    void CleanStain(GameObject stain)
    {
        // 청소 데이터 생성
        if (!stainProgress.ContainsKey(stain))
            stainProgress[stain] = new StainData(stain, stainAlphaPropertyName);

        StainData data = stainProgress[stain];

        // 이미 청소 완료
        if (data.isFullyCleaned)
            return;

        // Decal도 없고, 알파 방식 Renderer도 없으면 청소 불가능한 오브젝트
        if (data.isDecalType && data.decalProjector == null)
        {
            Debug.LogWarning($"DecalProjector 없음: {stain.name}");
            return;
        }
        if (!data.isDecalType && data.stainMaterialInstance == null)
        {
            Debug.LogWarning($"DecalProjector도 Renderer도 없음: {stain.name}");
            return;
        }

        // 청소 진행
        data.cleanCount++;

        // 투명도 계산 (0~1, 닦을수록 감소)
        float remainingRatio = Mathf.Max(0f, 1f - cleanPercentage * data.cleanCount);

        if (data.isDecalType)
        {
            // URP Decal 얼룩: fadeFactor를 낮춰서 지움
            data.decalProjector.fadeFactor = data.initialFadeFactor * remainingRatio;
        }
        else
        {
            // 일반 Renderer 얼룩: 머티리얼 알파값을 낮춰서 지움
            Color c = data.stainMaterialInstance.GetColor(stainAlphaPropertyName);
            c.a = data.initialAlpha * remainingRatio;
            data.stainMaterialInstance.SetColor(stainAlphaPropertyName, c);
        }

        // 햅틱
        if (targetDevice.isValid)
            targetDevice.SendHapticImpulse(0, 0.3f, 0.1f);

        // 완전 청소
        if (data.cleanCount >= totalCleanCount)
        {
            data.isFullyCleaned = true;
            if (!reportedTutorial)
            {
                reportedTutorial = true;
                FindFirstObjectByType<TutorialProgressTracker>()?.OnToolUsed(ToolPickup.ToolType.Mop);
            }
            if (moneyManager != null)
                moneyManager.OnFloorStainCleaned();

            if (showDebugInfo)
                Debug.Log("얼룩 완전 제거!");

            // 강한 햅틱
            if (targetDevice.isValid)
                targetDevice.SendHapticImpulse(0, 0.8f, 0.3f);

            // 오브젝트 파괴
            Destroy(stain);
            stainProgress.Remove(stain);
            currentStainBeingCleaned = null;
        }
    }

    void DirtyMopHead()
    {
        if (mopHeadMaterialInstance == null)
            return;

        // 이미 최대로 더러움 - 더 이상 색상 변경 안 함
        if (totalCleanActions >= maxDirtyCount)
        {
            if (showDebugInfo && totalCleanActions == maxDirtyCount)
                Debug.Log("대걸레 최대 더러움! 이제 청소하면 얼룩이 생깁니다!");
            return;
        }

        // 청소 횟수 증가
        totalCleanActions++;

        // 더러워지는 비율 (0~1)
        float dirtyRatio = Mathf.Clamp01((float)totalCleanActions / maxDirtyCount);

        // 색상 보간
        Color newColor = Color.Lerp(cleanColor, dirtyColor, dirtyRatio);

        // 색상 적용
        if (mopHeadMaterialInstance.HasProperty(colorPropertyName))
        {
            mopHeadMaterialInstance.SetColor(colorPropertyName, newColor);

            if (showDebugInfo)
                Debug.Log($"대걸레 더러워짐: {totalCleanActions}/{maxDirtyCount} ({dirtyRatio * 100:F0}%)");
        }
    }

    void CreateDirtyStain()
    {
        if (showDebugInfo)
            Debug.Log("CreateDirtyStain 함수 호출됨!");

        // 데칼 머티리얼이 설정되지 않았으면 경고
        if (stainDecalMaterials == null || stainDecalMaterials.Length == 0)
        {
            Debug.LogWarning("얼룩 데칼 머티리얼이 설정되지 않았습니다!");
            return;
        }

        // 유효한 머티리얼만 필터링
        System.Collections.Generic.List<Material> validMaterials =
            new System.Collections.Generic.List<Material>();

        foreach (Material mat in stainDecalMaterials)
        {
            if (mat != null)
                validMaterials.Add(mat);
        }

        if (validMaterials.Count == 0)
        {
            Debug.LogWarning("유효한 데칼 머티리얼이 없습니다! Inspector에서 Stain Decal Materials를 확인하세요.");
            return;
        }

        if (showDebugInfo)
            Debug.Log($"유효한 머티리얼 개수: {validMaterials.Count}");

        // 대걸레 헤드 위치에서 아래로 레이캐스트
        Ray ray = new Ray(CleaningOrigin, Vector3.down);
        RaycastHit hit;

        if (showDebugInfo)
        {
            Debug.Log($"레이캐스트 시작 위치: {transform.position}");
            Debug.Log($"레이캐스트 거리: {raycastDistance}");
            Debug.Log($"바닥 레이어: {floorLayer.value}");
        }

        if (Physics.Raycast(ray, out hit, raycastDistance, floorLayer))
        {
            if (showDebugInfo)
                Debug.Log($"바닥 감지 성공! 충돌 지점: {hit.point}, 오브젝트: {hit.collider.gameObject.name}");

            // 랜덤 머티리얼 선택 (3개 중 1개)
            int randomIndex = Random.Range(0, validMaterials.Count);
            Material selectedMaterial = validMaterials[randomIndex];

            // 랜덤 크기 생성
            Vector3 randomSize = new Vector3(
                Random.Range(minDecalSize.x, maxDecalSize.x),
                Random.Range(minDecalSize.y, maxDecalSize.y),
                Random.Range(minDecalSize.z, maxDecalSize.z)
            );

            // 랜덤 회전 (Y축만)
            float randomRotation = Random.Range(0f, 360f);

            // 데칼 오브젝트 생성
            GameObject decalObj = new GameObject("NewStainDecal");
            decalObj.transform.position = hit.point;
            decalObj.transform.rotation = Quaternion.Euler(90f, randomRotation, 0f);
            decalObj.tag = cleanableTag;

            // DecalProjector 추가
            DecalProjector decalProjector = decalObj.AddComponent<DecalProjector>();
            decalProjector.material = selectedMaterial;
            decalProjector.size = randomSize;
            decalProjector.fadeFactor = 1.0f;

            // 햅틱 피드백 (얼룩 생성)
            if (targetDevice.isValid)
                targetDevice.SendHapticImpulse(0, 0.5f, 0.15f);

            if (showDebugInfo)
            {
                Debug.Log($"✅ 새 얼룩 생성 완료!");
                Debug.Log($"  - 이름: {decalObj.name}");
                Debug.Log($"  - 위치: {decalObj.transform.position}");
                Debug.Log($"  - 회전: {decalObj.transform.rotation.eulerAngles}");
                Debug.Log($"  - 머티리얼: {selectedMaterial.name}");
                Debug.Log($"  - 크기: {randomSize}");
                Debug.Log($"  - 태그: {decalObj.tag}");
            }
        }
        else
        {
            Debug.LogWarning("⚠️ 바닥을 찾을 수 없습니다!");
            Debug.LogWarning($"레이캐스트 실패 - 시작 위치: {transform.position}, 방향: {Vector3.down}, 거리: {raycastDistance}");
            Debug.LogWarning($"Floor Layer 설정을 확인하세요. 현재 값: {floorLayer.value}");

            // 레이어 마스크가 -1(Everything)인지 확인
            if (floorLayer.value == -1)
            {
                Debug.LogWarning("Floor Layer가 'Everything'으로 설정되어 있습니다. 특정 레이어를 지정하는 것이 좋습니다.");
            }
        }
    }

    // ===== Washing Logic =====
    void CheckWaterContact()
    {
        // 물 오브젝트 찾기 (Trigger Collider 감지)
        Collider[] hits = Physics.OverlapSphere(CleaningOrigin, reachDistance);

        bool foundWater = false;

        foreach (Collider hit in hits)
        {
            // "Water" 태그를 가진 Trigger 오브젝트 찾기
            if (hit.CompareTag("Water") && hit.isTrigger)
            {
                foundWater = true;

                // 다른 물 오브젝트로 바뀌면 상태 리셋
                if (currentWaterObject != hit.gameObject)
                {
                    currentWaterObject = hit.gameObject;
                    currentWashCount = 0;
                    currentWashDirection = WashDirection.None;
                    lastWashDirection = WashDirection.None;

                    if (showDebugInfo)
                        Debug.Log($"물 감지: {hit.gameObject.name}");
                }

                isInWater = true;
                break;
            }
        }

        // 물에서 벗어남
        if (foundWater && !isInWater)
        {
            // 물에 막 들어간 순간 청소 효과음 즉시 정지
            SetMopAudio(false);
            mopStopTimer = 0f;
            isMopping = false;
            mopAudioSource?.Stop();
        }

        if (!foundWater && isInWater)
        {
            if (showDebugInfo)
                Debug.Log("물에서 벗어남");
            ResetWashingState();
        }
    }

    void DetectWashMotion()
    {
        if (!targetDevice.isValid)
            return;

        // 컨트롤러 속도 가져오기
        targetDevice.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vector3 velocity);

        // 월드 Y축 기준 (위/아래) — 양동이는 항상 바닥에 고정, 컨트롤러 방향 무관
        float verticalVelocity = velocity.y;

        // 디버그 (0.5초마다)
        if (showDebugInfo && Time.frameCount % 30 == 0)
            Debug.Log($"씻기 속도 (Y축): {verticalVelocity:F3} m/s");

        // 방향 감지
        WashDirection detectedDirection = WashDirection.None;

        if (verticalVelocity > washVelocityThreshold)
            detectedDirection = WashDirection.Up;
        else if (verticalVelocity < -washVelocityThreshold)
            detectedDirection = WashDirection.Down;

        // 방향 변경 확인
        if (detectedDirection != WashDirection.None && detectedDirection != currentWashDirection)
        {
            if (washDirectionChangeTimer <= 0)
            {
                lastWashDirection = currentWashDirection;
                currentWashDirection = detectedDirection;
                washDirectionChangeTimer = washDirectionChangeDelay;

                if (showDebugInfo)
                    Debug.Log($"씻기 방향: {currentWashDirection}");

                // 위↔아래 한 세트 완료
                if ((lastWashDirection == WashDirection.Up && currentWashDirection == WashDirection.Down) ||
                    (lastWashDirection == WashDirection.Down && currentWashDirection == WashDirection.Up))
                {
                    OnWashCycleComplete();
                }
            }
        }
    }

    void OnWashCycleComplete()
    {
        currentWashCount++;

        if (showDebugInfo)
            Debug.Log($"씻기 한 세트 완료! ({currentWashCount}/{totalWashCount})");

        if (washAudioSource != null && washSound != null && !washAudioSource.isPlaying)
            washAudioSource.PlayOneShot(washSound);

        // 양동이 시스템에 헹굼 알림
        if (bucketWaterSystem != null)
        {
            // 물을 사용할 수 없으면 헹굼 불가
            if (!bucketWaterSystem.CanRinse())
            {
                if (showDebugInfo)
                    Debug.Log("물이 너무 더러워서 헹굴 수 없습니다! 물을 갈아주세요.");

                // 햅틱 피드백 (실패)
                if (targetDevice.isValid)
                    targetDevice.SendHapticImpulse(0, 0.6f, 0.2f);

                return;
            }

            // 현재 대걸레 더러움 정도 계산 (0~1)
            float mopDirtyRatio = GetDirtyRatio();

            // 더러운 경우에만 물에 전달
            if (mopDirtyRatio > 0f)
            {
                bucketWaterSystem.OnToolRinsed(mopDirtyRatio);

                if (showDebugInfo)
                    Debug.Log($"🧹 대걸레 더러움: {mopDirtyRatio * 100:F0}% → 물에 전달");
            }
        }

        // 햅틱 피드백
        if (targetDevice.isValid)
            targetDevice.SendHapticImpulse(0, 0.4f, 0.1f);

        // 부분적으로 깨끗해짐 (3번 중 현재 횟수만큼)
        PartialCleanMopHead(currentWashCount);

        // 완전히 깨끗해짐
        if (currentWashCount >= totalWashCount)
        {
            CleanMopHead();
            // 강한 햅틱
            if (targetDevice.isValid)
                targetDevice.SendHapticImpulse(0, 0.8f, 0.3f);

            if (showDebugInfo)
                Debug.Log("대걸레 완전히 깨끗해짐!");

            // 씻기 상태 초기화
            currentWashCount = 0;
            currentWashDirection = WashDirection.None;
            lastWashDirection = WashDirection.None;
        }
    }

    void PartialCleanMopHead(int washCount)
    {
        if (mopHeadMaterialInstance == null)
            return;

        // 현재 더러운 정도
        float currentDirtyRatio = Mathf.Clamp01((float)totalCleanActions / maxDirtyCount);

        // 씻은 비율 (0~1)
        float washRatio = Mathf.Clamp01((float)washCount / totalWashCount);

        // 새로운 더러운 정도 (씻을수록 감소)
        float newDirtyRatio = currentDirtyRatio * (1f - washRatio);

        // 새로운 청소 횟수 계산
        totalCleanActions = Mathf.RoundToInt(newDirtyRatio * maxDirtyCount);

        // 색상 보간
        Color newColor = Color.Lerp(cleanColor, dirtyColor, newDirtyRatio);

        // 색상 적용
        if (mopHeadMaterialInstance.HasProperty(colorPropertyName))
        {
            mopHeadMaterialInstance.SetColor(colorPropertyName, newColor);

            if (showDebugInfo)
                Debug.Log($"대걸레 부분적으로 깨끗해짐: {totalCleanActions}/{maxDirtyCount} ({newDirtyRatio * 100:F0}%)");
        }
    }

    // ===== Public Methods =====
    /// <summary>
    /// 대걸레를 깨끗하게 초기화 (외부 호출용)
    /// </summary>
    public void CleanMopHead()
    {
        totalCleanActions = 0;

        if (mopHeadMaterialInstance != null && mopHeadMaterialInstance.HasProperty(colorPropertyName))
        {
            mopHeadMaterialInstance.SetColor(colorPropertyName, cleanColor);

            if (showDebugInfo)
                Debug.Log("대걸레 헤드 깨끗해짐!");
        }
    }

    /// <summary>
    /// 현재 더러워진 정도 반환 (0~1)
    /// </summary>
    public float GetDirtyRatio()
    {
        return Mathf.Clamp01((float)totalCleanActions / maxDirtyCount);
    }

    // ===== Inner Class =====
    public class StainData
    {
        public GameObject stainObject;

        // URP Decal 방식
        public DecalProjector decalProjector;
        public float initialFadeFactor;

        // 일반 Renderer 머티리얼 알파 방식
        public Renderer stainRenderer;
        public Material stainMaterialInstance;
        public float initialAlpha;

        public bool isDecalType;
        public int cleanCount = 0;
        public bool isFullyCleaned = false;

        public StainData(GameObject stain, string alphaPropertyName)
        {
            stainObject = stain;
            decalProjector = stain.GetComponent<DecalProjector>();

            if (decalProjector != null)
            {
                // 기존 방식: URP Decal Projector의 fadeFactor로 얼룩을 지움
                isDecalType = true;
                initialFadeFactor = decalProjector.fadeFactor;
            }
            else
            {
                // 신규 방식: DecalProjector가 없으면 일반 Renderer 머티리얼의 알파값으로 얼룩을 지움
                isDecalType = false;
                stainRenderer = stain.GetComponent<Renderer>();

                if (stainRenderer == null)
                {
                    Debug.LogWarning("DecalProjector도 Renderer도 없음: " + stain.name);
                    return;
                }

                // Renderer.material은 최초 접근 시 자동으로 인스턴스를 만들어줘서
                // 같은 머티리얼을 공유하는 다른 얼룩에는 영향을 주지 않는다.
                stainMaterialInstance = stainRenderer.material;

                // Alpha Clipping이 켜져 있으면 알파가 Cutoff 밑으로 내려가는 순간 픽셀이
                // 통째로 잘려서 미리 사라져 보인다. fadeFactor처럼 매끄럽게 옅어지도록 꺼준다.
                if (stainMaterialInstance.HasProperty("_AlphaClip"))
                {
                    stainMaterialInstance.SetFloat("_AlphaClip", 0f);
                    stainMaterialInstance.DisableKeyword("_ALPHATEST_ON");
                }

                // "Preserve Specular Lighting"이 켜져 있으면(URP Lit) 알파가 0에 가까워져도
                // 스페큘러 하이라이트/반사가 그대로 남아, 얼룩이 거의 다 지워졌을 때도
                // 평면(Quad) 테두리 모양의 반사광이 도드라져 보인다.
                // 꺼서 스페큘러도 알파와 함께 자연스럽게 사라지도록 만든다.
                if (stainMaterialInstance.HasProperty("_BlendModePreserveSpecular"))
                {
                    stainMaterialInstance.SetFloat("_BlendModePreserveSpecular", 0f);
                    stainMaterialInstance.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    stainMaterialInstance.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    stainMaterialInstance.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                    stainMaterialInstance.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    stainMaterialInstance.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                }

                if (stainMaterialInstance.HasProperty(alphaPropertyName))
                {
                    initialAlpha = stainMaterialInstance.GetColor(alphaPropertyName).a;
                }
                else
                {
                    Debug.LogWarning($"머티리얼에 '{alphaPropertyName}' 속성이 없음: {stain.name} (Surface Type이 Transparent인지, 속성 이름이 맞는지 확인하세요)");
                    initialAlpha = 1f;
                }
            }
        }
    }
}