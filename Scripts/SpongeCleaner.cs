using UnityEngine;
using UnityEngine.XR;
using UnityEngine.Rendering.Universal;

public class SpongeCleaner : MonoBehaviour
{
    [Header("청소 설정")]
    [Tooltip("이 도구로 청소 가능한 오브젝트의 태그")]
    [SerializeField] private string cleanableTag = "Cleanable_Sponge";

    [Tooltip("ToolSwitcher에서 이 도구를 식별하는 태그 prefix (StartsWith 비교)")]
    [SerializeField] private string toolTagPrefix = "Sponge";

    [Tooltip("완전히 청소하는 데 필요한 횟수")]
    public int totalCleanCount = 5;

    [Tooltip("URP Decal이 아닌, 일반 Renderer 머티리얼(알파값)로 표현된 얼룩의 색상 속성 이름")]
    public string stainAlphaPropertyName = "_BaseColor";

    [Header("스펀지 더러워지기 설정")]
    [Tooltip("스펀지 머티리얼")]
    public Material spongeMaterial;

    [Tooltip("스펀지가 있는 오브젝트의 Renderer")]
    public Renderer spongeRenderer;

    [Tooltip("깨끗한 상태의 색상")]
    public Color cleanColor = Color.white;

    [Tooltip("최대로 더러운 상태의 색상")]
    public Color dirtyColor = new Color(0.4f, 0.35f, 0.3f, 1f);

    [Tooltip("완전히 더러워지는 데 필요한 청소 횟수")]
    public int maxDirtyCount = 15;

    [Tooltip("머티리얼 색상 속성 이름")]
    public string colorPropertyName = "_BaseColor";

    [Tooltip("Renderer의 머티리얼 인덱스 (보통 0)")]
    public int materialIndex = 0;

    [Header("레이캐스트 설정")]
    [Tooltip("스펀지가 닿을 수 있는 최대 거리")]
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

    [Header("문지르기 감지 설정")]
    [Tooltip("위/아래로 움직이는 속도 임계값 (m/s)")]
    public float scrubVelocityThreshold = 0.3f;

    [Tooltip("문지르기 방향이 바뀌는 데 필요한 최소 시간 (초)")]
    public float directionChangeDelay = 0.2f;

    [Header("스펀지 헹구기 설정")]
    [Tooltip("위/아래 속도 임계값 (m/s) - 헹구기용")]
    public float rinseVelocityThreshold = 0.35f;

    [Tooltip("스펀지를 완전히 헹구는 데 필요한 제스처 횟수")]
    public int totalRinseCount = 3;

    [Tooltip("헹구기 방향 변경 최소 시간 (초)")]
    public float rinseDirectionChangeDelay = 0.2f;

    [Header("ToolSwitcher 연동")]
    [Tooltip("ToolSwitcher 스크립트")]
    public ToolSwitcher toolSwitcher;

    [Header("세면대 연동")]
    [Tooltip("SinkWaterSystem 스크립트 (자동 검색). 씬에 물통/세면대가 여러 개면 실제로 헹구고 있는 쪽이 우선 사용됨")]
    public WaterSystem sinkWaterSystem;

    [Header("효과음")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip scrubSound;
    [SerializeField] private AudioSource rinseAudioSource;
    [SerializeField] private AudioClip rinseSound;

    [Header("디버그")]
    [Tooltip("콘솔에 청소 정보 표시")]
    public bool showDebugInfo = true;

    // ===== Private Variables =====
    // 얼룩 청소
    private System.Collections.Generic.Dictionary<GameObject, StainData> stainProgress =
        new System.Collections.Generic.Dictionary<GameObject, StainData>();
    private GameObject currentStainBeingCleaned = null;

    // 컨트롤러 입력 관련
    private InputDevice targetDevice;
    private bool isGripping = false;

    // 문지르기 동작 감지 변수
    private enum ScrubDirection { None, Up, Down }
    private ScrubDirection currentDirection = ScrubDirection.None;
    private ScrubDirection lastDirection = ScrubDirection.None;
    private float directionChangeTimer = 0f;
    private bool isScrubbing = false;
    private float scrubStopTimer = 0f;
    [SerializeField] private float scrubStopDelay = 0.25f;

    // 스펀지 더러워지기
    private int totalCleanActions = 0;
    private Material spongeMaterialInstance; // 인스턴스 (원본 보호)
    private Color originalSpongeColor;

    // 튜토리얼
    private bool reportedTutorial = false;
    private TutorialProgressTracker tutorialTracker;
    private TutorialCleaningGuide cleaningGuide;
    private float lastWrongToolHintTime = -999f;
    public float wrongToolHintCooldown = 3f;

    // 스펀지 헹구기
    private enum RinseDirection { None, Up, Down }
    private RinseDirection currentRinseDirection = RinseDirection.None;
    private RinseDirection lastRinseDirection = RinseDirection.None;
    private float rinseDirectionChangeTimer = 0f;
    private int currentRinseCount = 0;
    private bool isInWater = false;
    private GameObject currentWaterObject = null;

    // 씬에 물통/세면대가 여러 개(예: Stage3 개수대 2개)일 때, 실제로 접촉 중인 물통을 가리킴
    private WaterSystem[] allWaterSystems;
    private WaterSystem activeWaterSystem;

    // ===== Unity Lifecycle =====
    void Start()
    {
        InitializeController();
        InitializeRightHandController();
        InitializeToolSwitcher();
        InitializeSponge();
        InitializeSinkWaterSystem();
        tutorialTracker = FindFirstObjectByType<TutorialProgressTracker>();

        AudioSource[] sources = GetComponents<AudioSource>();
        if (audioSource == null)
            audioSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
        if (rinseAudioSource == null)
            rinseAudioSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {
        // 컨트롤러 재연결
        if (!targetDevice.isValid)
            InitializeController();

        // 타이머 감소
        if (directionChangeTimer > 0)
            directionChangeTimer -= Time.deltaTime;

        if (rinseDirectionChangeTimer > 0)
            rinseDirectionChangeTimer -= Time.deltaTime;

        if (scrubStopTimer > 0f)
        {
            scrubStopTimer -= Time.deltaTime;
            if (scrubStopTimer <= 0f) { isScrubbing = false; audioSource?.Stop(); }
        }

        // 스펀지를 들고 있지 않으면 리턴
        bool holding = IsHoldingSponge();
        if (showDebugInfo && Time.frameCount % 120 == 0)
        {
            string tag = toolSwitcher != null ? toolSwitcher.GetCurrentToolTag() : "toolSwitcher=null";
            Debug.Log($"[SpongeCleaner] holding={holding} | tag='{tag}' | device={targetDevice.isValid}");
        }
        if (!holding)
        {
            ResetCleaningState();
            ResetRinsingState();
            return;
        }

        // 잡기 확인
        CheckGripInput();
        if (showDebugInfo && Time.frameCount % 120 == 0)
        {
            targetDevice.TryGetFeatureValue(CommonUsages.trigger, out float tv);
            targetDevice.TryGetFeatureValue(CommonUsages.grip,    out float gv);
            Debug.Log($"[SpongeCleaner] isGripping={isGripping} | trigger={tv:F2} grip={gv:F2} (threshold={triggerThreshold}/{gripThreshold})");
        }

        // 잡고 있을 때만 작업
        if (isGripping)
        {
            // 물과의 충돌 확인
            CheckWaterContact();

            // 물 안에 있으면 헹구기 제스처 감지
            if (isInWater)
            {
                DetectRinseMotion();
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
            ResetRinsingState();
        }
    }

    void OnDestroy()
    {
        // 원본 머티리얼 색상 복원
        RestoreSpongeMaterial();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, reachDistance);
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

    void InitializeSinkWaterSystem()
    {
        if (sinkWaterSystem == null)
            sinkWaterSystem = FindFirstObjectByType<WaterSystem>();

        if (sinkWaterSystem == null)
        {
            Debug.LogWarning("SinkWaterSystem을 찾을 수 없습니다! 세면대 헹굼 기능이 제한될 수 있습니다.");
        }
        else if (showDebugInfo)
        {
            Debug.Log("SinkWaterSystem 연동 완료!");
        }

        // 씬에 물통/세면대가 여러 개일 수 있으므로 전부 캐싱해두고,
        // 실제로 접촉한 오브젝트(waterObject)로 어느 WaterSystem인지 매 순간 판별한다.
        allWaterSystems = FindObjectsByType<WaterSystem>(FindObjectsSortMode.None);
    }

    // hit이 어느 WaterSystem의 물 오브젝트(waterObject)인지 찾아 반환. 못 찾으면 null.
    WaterSystem ResolveWaterSystem(GameObject hitObject)
    {
        if (allWaterSystems == null) return null;

        foreach (var ws in allWaterSystems)
        {
            if (ws != null && ws.waterObject == hitObject)
                return ws;
        }
        return null;
    }

    void InitializeSponge()
    {
        if (spongeMaterial == null)
        {
            Debug.LogWarning("스펀지 머티리얼 미할당! Inspector에서 할당하세요.");
            return;
        }

        // Renderer 자동 찾기
        if (spongeRenderer == null)
        {
            spongeRenderer = GetComponentInChildren<Renderer>();

            if (spongeRenderer == null)
            {
                Debug.LogWarning("스펀지 Renderer를 찾을 수 없습니다! Inspector에서 할당하세요.");
                return;
            }

            if (showDebugInfo)
                Debug.Log($"Renderer 자동 발견: {spongeRenderer.gameObject.name}");
        }

        // 머티리얼 인스턴스 생성 (원본 보호)
        spongeMaterialInstance = new Material(spongeMaterial);

        // Renderer에 인스턴스 머티리얼 할당
        Material[] materials = spongeRenderer.materials;

        if (materialIndex >= 0 && materialIndex < materials.Length)
        {
            materials[materialIndex] = spongeMaterialInstance;
            spongeRenderer.materials = materials;

            if (showDebugInfo)
                Debug.Log($"인스턴스 머티리얼 할당 완료! (인덱스: {materialIndex})");
        }
        else
        {
            Debug.LogWarning($"잘못된 머티리얼 인덱스: {materialIndex} (총 {materials.Length}개)");
            return;
        }

        // 색상 속성 찾기 및 저장
        if (spongeMaterialInstance.HasProperty(colorPropertyName))
        {
            originalSpongeColor = spongeMaterialInstance.GetColor(colorPropertyName);
            cleanColor = originalSpongeColor;

            if (showDebugInfo)
                Debug.Log($"스펀지 머티리얼 초기화! 원본 색상: {originalSpongeColor}");
        }
        else if (TryFindColorProperty())
        {
            originalSpongeColor = spongeMaterialInstance.GetColor(colorPropertyName);
            cleanColor = originalSpongeColor;

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
            if (spongeMaterialInstance.HasProperty(propName))
            {
                colorPropertyName = propName;
                return true;
            }
        }

        return false;
    }

    Transform FindRightHandController()
    {
        GameObject xrOrigin = GameObject.Find("XR Origin");
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

    void RestoreSpongeMaterial()
    {
        // Renderer에 원본 머티리얼 다시 할당
        if (spongeRenderer != null && spongeMaterial != null)
        {
            Material[] materials = spongeRenderer.materials;

            if (materialIndex >= 0 && materialIndex < materials.Length)
            {
                materials[materialIndex] = spongeMaterial;
                spongeRenderer.materials = materials;

                if (showDebugInfo)
                    Debug.Log("원본 머티리얼 복원!");
            }
        }

        // 인스턴스 머티리얼 메모리 해제
        if (spongeMaterialInstance != null)
        {
            Destroy(spongeMaterialInstance);
            spongeMaterialInstance = null;
        }
    }

    // ===== Input Handling =====
    bool IsHoldingSponge()
    {
        if (toolSwitcher == null) return false;
        string current = toolSwitcher.GetCurrentToolTag();
        if (string.IsNullOrEmpty(current)) return false;
        if (current.StartsWith(toolTagPrefix)) return true;
        // 스프레이는 스펀지 전용 보조 도구 (손걸레 등 다른 prefix에서는 제외)
        return toolTagPrefix == "Sponge" && current == "Spray";
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
                Debug.Log("스펀지 잡음!");
        }
        else if (!isGripping && wasPreviouslyGripping)
        {
            if (showDebugInfo)
                Debug.Log("스펀지 놓음!");
        }
    }

    void SetScrubAudio(bool playing)
    {
        if (audioSource == null || scrubSound == null) return;
        if (playing)
        {
            scrubStopTimer = 0f;
            if (!isScrubbing)
            {
                isScrubbing = true;
                audioSource.clip = scrubSound;
                audioSource.loop = true;
                audioSource.Play();
            }
        }
        else if (isScrubbing && scrubStopTimer <= 0f)
        {
            scrubStopTimer = scrubStopDelay;
        }
    }

    void ResetCleaningState()
    {
        currentStainBeingCleaned = null;
        currentDirection = ScrubDirection.None;
        lastDirection = ScrubDirection.None;
        SetScrubAudio(false);
    }

    void ResetRinsingState()
    {
        currentRinseDirection = RinseDirection.None;
        lastRinseDirection = RinseDirection.None;
        currentRinseCount = 0;
        isInWater = false;
        currentWaterObject = null;
        activeWaterSystem = null;
        rinseAudioSource?.Stop();
    }

    // ===== Cleaning Logic =====
    void CheckForStainContact()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, reachDistance);
        if (showDebugInfo && Time.frameCount % 60 == 0)
            Debug.Log($"[SpongeCleaner] OverlapSphere hit {hits.Length}개 | sponge pos={transform.position} | reach={reachDistance}");

        // 가장 가까운 얼룩 찾기
        GameObject nearestStain = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag(cleanableTag))
            {
                float distance = Vector3.Distance(transform.position, hit.transform.position);
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
                currentDirection = ScrubDirection.None;
                lastDirection = ScrubDirection.None;

                if (showDebugInfo)
                    Debug.Log($"얼룩 감지: {nearestStain.name}");
            }

            DetectScrubMotion(nearestStain);
        }
        else
        {
            ResetCleaningState();

            // 잘못된 도구 감지 (스펀지로 빗자루/대걸레 대상 청소 시도)
            if (isGripping && tutorialTracker != null
                && Time.time - lastWrongToolHintTime >= wrongToolHintCooldown)
            {
                foreach (Collider hit in hits)
                {
                    if (hit.CompareTag("Cleanable_Broom"))
                    {
                        lastWrongToolHintTime = Time.time;
                        tutorialTracker.PlayWrongToolHint(ToolPickup.ToolType.Broom);
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

    void DetectScrubMotion(GameObject stain)
    {
        if (!targetDevice.isValid)
            return;

        // 컨트롤러 속도 가져오기
        targetDevice.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vector3 velocity);

        // Y축 속도만 확인 (위/아래 움직임)
        float verticalVelocity = velocity.y;

        // 디버그 (0.5초마다)
        if (showDebugInfo && Time.frameCount % 30 == 0)
            Debug.Log($"문지르기 속도 (Y축): {verticalVelocity:F3} m/s");

        // 문지르기 중 여부에 따라 효과음 재생/정지
        bool moving = Mathf.Abs(verticalVelocity) > scrubVelocityThreshold;
        SetScrubAudio(moving);

        // 방향 감지
        ScrubDirection detectedDirection = ScrubDirection.None;

        if (verticalVelocity > scrubVelocityThreshold)
            detectedDirection = ScrubDirection.Up;
        else if (verticalVelocity < -scrubVelocityThreshold)
            detectedDirection = ScrubDirection.Down;

        // 방향 변경 확인
        if (detectedDirection != ScrubDirection.None && detectedDirection != currentDirection)
        {
            if (directionChangeTimer <= 0)
            {
                lastDirection = currentDirection;
                currentDirection = detectedDirection;
                directionChangeTimer = directionChangeDelay;

                if (showDebugInfo)
                    Debug.Log($"문지르기 방향: {currentDirection}");

                // 방향이 바뀌면 한 세트 완료 (Down↔Up 양방향)
                if ((lastDirection == ScrubDirection.Down && currentDirection == ScrubDirection.Up) ||
                    (lastDirection == ScrubDirection.Up && currentDirection == ScrubDirection.Down))
                {
                    OnScrubCycleComplete(stain);
                }
            }
        }
    }

    void OnScrubCycleComplete(GameObject stain)
    {
        if (showDebugInfo)
            Debug.Log("문지르기 한 세트 완료!");

        cleaningGuide ??= FindFirstObjectByType<TutorialCleaningGuide>();
        if (cleaningGuide != null && cleaningGuide.IsWrongTool(ToolPickup.ToolType.Sponge))
        {
            cleaningGuide.PlayCurrentStepHint();
            return;
        }

        // 15번 이상 청소했으면 청소 차단
        if (totalCleanActions >= maxDirtyCount)
        {
            if (showDebugInfo)
                Debug.Log("스펀지가 너무 더러워서 청소할 수 없습니다! 물에서 헹구세요.");

            // 청소는 진행하지 않음
            return;
        }

        // 15번 이하일 때만 청소 진행
        CleanStain(stain);
        DirtySponge();
    }

    void CleanStain(GameObject stain)
    {
        if (!stainProgress.ContainsKey(stain))
            stainProgress[stain] = new StainData(stain, stainAlphaPropertyName);

        StainData data = stainProgress[stain];
        if (data.isFullyCleaned) return;

        // Decal도 없고, 알파 방식 Renderer도 없으면 청소 불가능한 오브젝트
        if (data.isDecalType && data.decalProjector == null) return;
        if (!data.isDecalType && data.stainMaterialInstance == null) return;

        data.cleanCount++;

        // 스프레이 맞은 얼룩은 필요 횟수 절반
        int requiredCount = SprayCleaner.sprayedStains.Contains(stain)
            ? totalCleanCount / 2  // 2번
            : totalCleanCount;     // 5번

        // 0(안 지움) ~ 1(완전히 지움) 진행도
        float progress = Mathf.Clamp01((float)data.cleanCount / requiredCount);

        if (data.isDecalType)
        {
            // URP Decal 얼룩: fadeFactor를 낮춰서 지움
            float newFadeFactor = data.initialFadeFactor * (1f - progress);
            data.decalProjector.fadeFactor = Mathf.Max(0f, newFadeFactor);
        }
        else
        {
            // 일반 Renderer 얼룩: 머티리얼 알파값을 낮춰서 지움
            Color c = data.stainMaterialInstance.GetColor(stainAlphaPropertyName);
            c.a = Mathf.Max(0f, data.initialAlpha * (1f - progress));
            data.stainMaterialInstance.SetColor(stainAlphaPropertyName, c);
        }

        if (targetDevice.isValid)
            targetDevice.SendHapticImpulse(0, 0.3f, 0.1f);

        if (showDebugInfo)
            Debug.Log($"얼룩 청소 중... ({data.cleanCount}/{requiredCount}) - 타입: {(data.isDecalType ? "Decal" : "Renderer")} - 스프레이: {SprayCleaner.sprayedStains.Contains(stain)}");

        if (data.cleanCount >= requiredCount)
        {
            data.isFullyCleaned = true;
            if (!reportedTutorial)
            {
                reportedTutorial = true;
                FindFirstObjectByType<TutorialProgressTracker>()?.OnToolUsed(ToolPickup.ToolType.Sponge);
            }
            SprayCleaner.sprayedStains.Remove(stain); // 청소 완료 시 제거

            MoneyManager moneyManager = FindFirstObjectByType<MoneyManager>();
            if (moneyManager != null)
            {
                if (toolTagPrefix == "Rag")
                    moneyManager.OnRagCleaned();
                else
                    moneyManager.OnWaterStainCleaned();
            }

            if (targetDevice.isValid)
                targetDevice.SendHapticImpulse(0, 0.8f, 0.3f);

            Destroy(stain);
            stainProgress.Remove(stain);
            currentStainBeingCleaned = null;
        }
    }

    void DirtySponge()
    {
        if (spongeMaterialInstance == null)
            return;

        // 이미 최대로 더러움 - 더 이상 색상 변경 안 함
        if (totalCleanActions >= maxDirtyCount)
        {
            if (showDebugInfo && totalCleanActions == maxDirtyCount)
                Debug.Log("스펀지 최대 더러움! 물에서 헹구세요!");
            return;
        }

        // 청소 횟수 증가
        totalCleanActions++;

        // 더러워지는 비율 (0~1)
        float dirtyRatio = Mathf.Clamp01((float)totalCleanActions / maxDirtyCount);

        // 색상 보간
        Color newColor = Color.Lerp(cleanColor, dirtyColor, dirtyRatio);

        // 색상 적용
        if (spongeMaterialInstance.HasProperty(colorPropertyName))
        {
            spongeMaterialInstance.SetColor(colorPropertyName, newColor);

            if (showDebugInfo)
                Debug.Log($"스펀지 더러워짐: {totalCleanActions}/{maxDirtyCount} ({dirtyRatio * 100:F0}%)");
        }
    }

    // ===== Rinsing Logic =====
    void CheckWaterContact()
    {
        // 물 오브젝트 찾기 (Trigger Collider 감지)
        Collider[] hits = Physics.OverlapSphere(transform.position, reachDistance);

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
                    activeWaterSystem = ResolveWaterSystem(hit.gameObject);
                    currentRinseCount = 0;
                    currentRinseDirection = RinseDirection.None;
                    lastRinseDirection = RinseDirection.None;

                    if (showDebugInfo)
                        Debug.Log($"물 감지: {hit.gameObject.name} (WaterSystem: {(activeWaterSystem != null ? activeWaterSystem.GetContainerName() : "못 찾음")})");
                }

                isInWater = true;
                break;
            }
        }

        // 물에서 벗어남
        if (!foundWater && isInWater)
        {
            if (showDebugInfo)
                Debug.Log("물에서 벗어남");

            ResetRinsingState();
        }
    }

    void DetectRinseMotion()
    {
        if (!targetDevice.isValid)
            return;

        // 컨트롤러 속도 가져오기
        targetDevice.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vector3 velocity);

        // Y축 속도만 확인 (위/아래 움직임)
        float verticalVelocity = velocity.y;

        // 디버그 (0.5초마다)
        if (showDebugInfo && Time.frameCount % 30 == 0)
            Debug.Log($"헹구기 속도 (Y축): {verticalVelocity:F3} m/s");

        // 방향 감지
        RinseDirection detectedDirection = RinseDirection.None;

        if (verticalVelocity > rinseVelocityThreshold)
            detectedDirection = RinseDirection.Up;
        else if (verticalVelocity < -rinseVelocityThreshold)
            detectedDirection = RinseDirection.Down;

        // 방향 변경 확인
        if (detectedDirection != RinseDirection.None && detectedDirection != currentRinseDirection)
        {
            if (rinseDirectionChangeTimer <= 0)
            {
                lastRinseDirection = currentRinseDirection;
                currentRinseDirection = detectedDirection;
                rinseDirectionChangeTimer = rinseDirectionChangeDelay;

                if (showDebugInfo)
                    Debug.Log($"헹구기 방향: {currentRinseDirection}");

                // 위↔아래 한 세트 완료
                if ((lastRinseDirection == RinseDirection.Up && currentRinseDirection == RinseDirection.Down) ||
                    (lastRinseDirection == RinseDirection.Down && currentRinseDirection == RinseDirection.Up))
                {
                    OnRinseCycleComplete();
                }
            }
        }
    }

    void OnRinseCycleComplete()
    {
        currentRinseCount++;

        if (showDebugInfo)
            Debug.Log($"헹구기 한 세트 완료! ({currentRinseCount}/{totalRinseCount})");

        if (rinseAudioSource != null && rinseSound != null && !rinseAudioSource.isPlaying)
            rinseAudioSource.PlayOneShot(rinseSound);

        // 세면대 시스템에 헹굼 알림 — 실제로 접촉 중인 물통(activeWaterSystem)을 우선 사용.
        // 못 찾았을 때만 Inspector에 수동으로 연결해둔 sinkWaterSystem으로 대체(단일 물통 스테이지 호환용)
        WaterSystem targetWaterSystem = activeWaterSystem != null ? activeWaterSystem : sinkWaterSystem;

        if (targetWaterSystem != null)
        {
            // 물을 사용할 수 없으면 헹굼 불가
            if (!targetWaterSystem.CanRinse())
            {
                if (showDebugInfo)
                    Debug.Log($"{targetWaterSystem.GetContainerName()}: 물이 너무 더러워서 헹굴 수 없습니다! 물을 갈아주세요.");

                // 햅틱 피드백 (실패)
                if (targetDevice.isValid)
                    targetDevice.SendHapticImpulse(0, 0.6f, 0.2f);

                return;
            }

            // 현재 스펀지 더러움 정도 계산 (0~1)
            float spongeDirtyRatio = GetDirtyRatio();

            // 물 더러워지기 (스펀지 더러움 정도 전달)
            targetWaterSystem.OnToolRinsed(spongeDirtyRatio);

            if (showDebugInfo)
                Debug.Log($"🧽 스펀지 더러움: {spongeDirtyRatio * 100:F0}% → {targetWaterSystem.GetContainerName()}에 전달");
        }

        // 햅틱 피드백
        if (targetDevice.isValid)
            targetDevice.SendHapticImpulse(0, 0.4f, 0.1f);

        // 부분적으로 깨끗해짐 (3번 중 현재 횟수만큼)
        PartialCleanSponge(currentRinseCount);

        // 완전히 깨끗해짐
        if (currentRinseCount >= totalRinseCount)
        {
            CleanSponge();

            // 강한 햅틱
            if (targetDevice.isValid)
                targetDevice.SendHapticImpulse(0, 0.8f, 0.3f);

            if (showDebugInfo)
                Debug.Log("스펀지 완전히 깨끗해짐!");

            // 헹구기 상태 초기화
            currentRinseCount = 0;
            currentRinseDirection = RinseDirection.None;
            lastRinseDirection = RinseDirection.None;
        }
    }

    void PartialCleanSponge(int rinseCount)
    {
        if (spongeMaterialInstance == null)
            return;

        // 현재 더러운 정도
        float currentDirtyRatio = Mathf.Clamp01((float)totalCleanActions / maxDirtyCount);

        // 헹군 비율 (0~1)
        float rinseRatio = Mathf.Clamp01((float)rinseCount / totalRinseCount);

        // 새로운 더러운 정도 (헹굴수록 감소)
        float newDirtyRatio = currentDirtyRatio * (1f - rinseRatio);

        // 새로운 청소 횟수 계산
        totalCleanActions = Mathf.RoundToInt(newDirtyRatio * maxDirtyCount);

        // 색상 보간
        Color newColor = Color.Lerp(cleanColor, dirtyColor, newDirtyRatio);

        // 색상 적용
        if (spongeMaterialInstance.HasProperty(colorPropertyName))
        {
            spongeMaterialInstance.SetColor(colorPropertyName, newColor);

            if (showDebugInfo)
                Debug.Log($"스펀지 부분적으로 깨끗해짐: {totalCleanActions}/{maxDirtyCount} ({newDirtyRatio * 100:F0}%)");
        }
    }

    // ===== Public Methods =====
    /// <summary>
    /// 스펀지를 깨끗하게 초기화 (외부 호출용)
    /// </summary>
    public void CleanSponge()
    {
        totalCleanActions = 0;

        if (spongeMaterialInstance != null && spongeMaterialInstance.HasProperty(colorPropertyName))
        {
            spongeMaterialInstance.SetColor(colorPropertyName, cleanColor);

            if (showDebugInfo)
                Debug.Log("스펀지 깨끗해짐!");
        }
    }

    /// <summary>
    /// 현재 더러워진 정도 반환 (0~1)
    /// </summary>
    public float GetDirtyRatio()
    {
        return Mathf.Clamp01((float)totalCleanActions / maxDirtyCount);
    }
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

            // Alpha Clipping이 켜져 있으면 알파가 Cutoff 밑으로 내려가는 순간
            // (예: 5번 중 2번만 닦았는데) 픽셀이 통째로 잘려서 미리 사라져 보인다.
            // Decal의 fadeFactor처럼 알파값에 비례해 매끄럽게 옅어지도록 꺼준다.
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