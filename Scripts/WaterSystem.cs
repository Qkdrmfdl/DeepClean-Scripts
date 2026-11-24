using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 통합 물 관리 시스템
/// - 세면대, 양동이, 대야 등 모든 물 컨테이너에 사용 가능
/// - 청소 도구의 더러움 정도에 비례해서 물 오염
/// - VR 환경 완벽 지원
/// </summary>
public class WaterSystem : MonoBehaviour
{
    [Header("물 컨테이너 설정")]
    [Tooltip("물 컨테이너 이름 (디버그용)")]
    public string containerName = "물통";

    [Tooltip("플레이어가 감지되는 거리")]
    public float detectionRadius = 2.0f;

    [Tooltip("물 오브젝트 (비활성화 상태로 시작)")]
    public GameObject waterObject;

    [Tooltip("물 머티리얼")]
    public Material waterMaterial;

    [Tooltip("깨끗한 물 색상")]
    public Color cleanWaterColor = new Color(0.5f, 0.7f, 1f, 0.3f);

    [Tooltip("더러운 물 색상")]
    public Color dirtyWaterColor = new Color(0.4f, 0.35f, 0.3f, 0.5f);

    [Tooltip("물 색상 속성 이름")]
    public string waterColorPropertyName = "_BaseColor";

    [Tooltip("최대 헹굼 횟수")]
    public int maxRinseCount = 10;

    [Header("UI 설정")]
    [Tooltip("물 받기 버튼이 있는 Canvas")]
    public GameObject fillWaterButtonCanvas;

    [Tooltip("물 받기 버튼")]
    public Button fillWaterButton;

    [Tooltip("물 갈기 버튼이 있는 Canvas")]
    public GameObject changeWaterButtonCanvas;

    [Tooltip("물 갈기 버튼")]
    public Button changeWaterButton;

    [Header("플레이어 감지 (VR)")]
    [Tooltip("XR Origin 또는 XR Rig 오브젝트 이름")]
    public string xrRigName = "XR Origin (XR Rig)";

    [Tooltip("카메라 오브젝트 이름 (LookAt용)")]
    public string cameraName = "Main Camera";

    [Tooltip("UI가 카메라를 바라보게 할지 여부")]
    public bool enableLookAtCamera = true;

    [Header("디버그")]
    [Tooltip("디버그 로그 표시")]
    public bool showDebugInfo = true;

    // ===== Private Variables =====
    private float currentRinseCount = 0f;
    private Material waterMaterialInstance;
    private Transform xrRigTransform;
    private Transform cameraTransform;
    private bool isWaterFilled = false;
    private bool isPlayerNearby = false;

    void Start()
    {
        InitializeWaterMaterial();
        InitializeUI();
        FindPlayer();

        // 물 오브젝트 초기 비활성화
        if (waterObject != null)
            waterObject.SetActive(false);
    }

    void Update()
    {
        CheckPlayerProximity();
        UpdateUIVisibility();
        UpdateUILookAt();
    }

    void OnDestroy()
    {
        RestoreWaterMaterial();
    }

    void OnDrawGizmosSelected()
    {
        // 감지 범위
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // XR Rig 위치 표시 (플레이 중일 때)
        if (Application.isPlaying && xrRigTransform != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(xrRigTransform.position, 0.3f);

            // 물통과 XR Rig 사이에 선 그리기
            Gizmos.color = isPlayerNearby ? Color.green : Color.red;
            Gizmos.DrawLine(transform.position, xrRigTransform.position);
        }
    }

    // ===== Initialization =====
    void InitializeWaterMaterial()
    {
        if (waterMaterial == null)
        {
            Debug.LogWarning($"{containerName}: 물 머티리얼이 할당되지 않았습니다!");
            return;
        }

        // 머티리얼 인스턴스 생성 (원본 보호)
        waterMaterialInstance = new Material(waterMaterial);

        // 물 오브젝트의 Renderer 찾기
        if (waterObject != null)
        {
            Renderer waterRenderer = waterObject.GetComponent<Renderer>();
            if (waterRenderer != null)
            {
                waterRenderer.material = waterMaterialInstance;

                if (showDebugInfo)
                    Debug.Log($"{containerName}: 물 머티리얼 인스턴스 생성 완료!");
            }
            else
            {
                Debug.LogWarning($"{containerName}: 물 오브젝트에 Renderer가 없습니다!");
            }
        }

        // 초기 색상 설정
        SetWaterColor(cleanWaterColor);
    }

    void InitializeUI()
    {
        // 버튼 이벤트 연결
        if (fillWaterButton != null)
        {
            fillWaterButton.onClick.AddListener(OnFillWaterButtonClicked);
        }
        else
        {
            Debug.LogWarning($"{containerName}: 물 받기 버튼이 할당되지 않았습니다!");
        }

        if (changeWaterButton != null)
        {
            changeWaterButton.onClick.AddListener(OnChangeWaterButtonClicked);
        }
        else
        {
            Debug.LogWarning($"{containerName}: 물 갈기 버튼이 할당되지 않았습니다!");
        }

        // 초기 UI 상태 설정
        if (fillWaterButtonCanvas != null)
            fillWaterButtonCanvas.SetActive(false);

        if (changeWaterButtonCanvas != null)
            changeWaterButtonCanvas.SetActive(false);
    }

    void FindPlayer()
    {
        // XR Origin 또는 XR Rig 찾기
        GameObject xrRigObj = GameObject.Find(xrRigName);

        if (xrRigObj == null)
        {
            // 다른 가능한 이름들 시도
            string[] possibleNames = {
                "XR Origin (XR Rig)",
                "XR Origin",
                "XR Rig",
                "XRRig",
                "[XR Rig]",
                "XROrigin"
            };

            foreach (string name in possibleNames)
            {
                xrRigObj = GameObject.Find(name);
                if (xrRigObj != null)
                {
                    xrRigName = name;
                    if (showDebugInfo)
                        Debug.Log($"✅ XR Rig 이름 자동 감지: '{name}'");
                    break;
                }
            }
        }

        if (xrRigObj != null)
        {
            xrRigTransform = xrRigObj.transform;

            if (showDebugInfo)
                Debug.Log($"✅ XR Rig 발견: {xrRigObj.name}");
        }
        else
        {
            Debug.LogError($"❌ XR Rig을 찾을 수 없습니다!");
        }

        // 카메라 찾기 (LookAt용)
        GameObject cameraObj = GameObject.Find(cameraName);

        if (cameraObj == null)
        {
            cameraObj = Camera.main?.gameObject;
        }

        if (cameraObj != null)
        {
            cameraTransform = cameraObj.transform;

            if (showDebugInfo)
                Debug.Log($"✅ 카메라 발견: {cameraObj.name}");
        }
        else
        {
            Debug.LogWarning($"⚠️ 카메라를 찾을 수 없습니다! UI LookAt 기능이 작동하지 않습니다.");
        }
    }

    void RestoreWaterMaterial()
    {
        if (waterObject != null && waterMaterial != null)
        {
            Renderer waterRenderer = waterObject.GetComponent<Renderer>();
            if (waterRenderer != null)
            {
                waterRenderer.material = waterMaterial;

                if (showDebugInfo)
                    Debug.Log($"{containerName}: 원본 물 머티리얼 복원!");
            }
        }

        if (waterMaterialInstance != null)
        {
            Destroy(waterMaterialInstance);
            waterMaterialInstance = null;
        }
    }

    // ===== Player Detection =====
    void CheckPlayerProximity()
    {
        if (xrRigTransform == null)
        {
            FindPlayer();
            return;
        }

        float distance = Vector3.Distance(transform.position, xrRigTransform.position);

        bool wasNearby = isPlayerNearby;
        isPlayerNearby = distance <= detectionRadius;

        // 디버그 (1초마다)
        //if (showDebugInfo && Time.frameCount % 60 == 0)
        //{
        //    Debug.Log($"💧 {containerName}까지 거리: {distance:F2}m (감지 범위: {detectionRadius}m) - {(isPlayerNearby ? "근처" : "멀리")}");
        //}

        // 상태 변경 로그
        if (showDebugInfo && wasNearby != isPlayerNearby)
        {
            if (isPlayerNearby)
                Debug.Log($"✅ 플레이어가 {containerName} 근처에 들어왔습니다!");
            else
                Debug.Log($"❌ 플레이어가 {containerName}에서 멀어졌습니다.");
        }
    }

    void UpdateUIVisibility()
    {
        // 플레이어가 근처에 없으면 모든 UI 숨김
        if (!isPlayerNearby)
        {
            if (fillWaterButtonCanvas != null && fillWaterButtonCanvas.activeSelf)
            {
                fillWaterButtonCanvas.SetActive(false);

                if (showDebugInfo)
                    Debug.Log("🔴 물 받기 버튼 숨김 (플레이어 멀리)");
            }

            if (changeWaterButtonCanvas != null && changeWaterButtonCanvas.activeSelf)
            {
                changeWaterButtonCanvas.SetActive(false);

                if (showDebugInfo)
                    Debug.Log("🔴 물 갈기 버튼 숨김 (플레이어 멀리)");
            }

            return;
        }

        // 플레이어가 근처에 있을 때 상태에 따라 UI 표시
        if (!isWaterFilled)
        {
            // 물이 없으면 "물 받기" 버튼 표시
            if (fillWaterButtonCanvas != null && !fillWaterButtonCanvas.activeSelf)
            {
                fillWaterButtonCanvas.SetActive(true);

                if (showDebugInfo)
                    Debug.Log("🟢 물 받기 버튼 표시!");
            }

            if (changeWaterButtonCanvas != null && changeWaterButtonCanvas.activeSelf)
            {
                changeWaterButtonCanvas.SetActive(false);
            }
        }
        else if (currentRinseCount >= maxRinseCount)
        {
            // 물이 꽉 더러우면 "물 갈기" 버튼 표시
            if (fillWaterButtonCanvas != null && fillWaterButtonCanvas.activeSelf)
            {
                fillWaterButtonCanvas.SetActive(false);
            }

            if (changeWaterButtonCanvas != null && !changeWaterButtonCanvas.activeSelf)
            {
                changeWaterButtonCanvas.SetActive(true);

                if (showDebugInfo)
                    Debug.Log("🟢 물 갈기 버튼 표시!");
            }
        }
        else
        {
            // 물이 있고 아직 사용 가능하면 UI 숨김
            if (fillWaterButtonCanvas != null && fillWaterButtonCanvas.activeSelf)
            {
                fillWaterButtonCanvas.SetActive(false);
            }

            if (changeWaterButtonCanvas != null && changeWaterButtonCanvas.activeSelf)
            {
                changeWaterButtonCanvas.SetActive(false);
            }
        }
    }

    void UpdateUILookAt()
    {
        if (!enableLookAtCamera || cameraTransform == null)
            return;

        // 물 받기 버튼 Canvas
        if (fillWaterButtonCanvas != null && fillWaterButtonCanvas.activeSelf)
        {
            Vector3 directionToCamera = cameraTransform.position - fillWaterButtonCanvas.transform.position;
            fillWaterButtonCanvas.transform.rotation = Quaternion.LookRotation(-directionToCamera);
        }

        // 물 갈기 버튼 Canvas
        if (changeWaterButtonCanvas != null && changeWaterButtonCanvas.activeSelf)
        {
            Vector3 directionToCamera = cameraTransform.position - changeWaterButtonCanvas.transform.position;
            changeWaterButtonCanvas.transform.rotation = Quaternion.LookRotation(-directionToCamera);
        }
    }

    // ===== Button Events =====
    void OnFillWaterButtonClicked()
    {
        if (showDebugInfo)
            Debug.Log($"{containerName}: 물 받기 버튼 클릭!");

        // 물 오브젝트 활성화
        if (waterObject != null)
        {
            waterObject.SetActive(true);
            isWaterFilled = true;

            if (showDebugInfo)
                Debug.Log($"{containerName}: 물 활성화!");
        }

        // UI 숨김
        if (fillWaterButtonCanvas != null)
            fillWaterButtonCanvas.SetActive(false);
    }

    void OnChangeWaterButtonClicked()
    {
        if (showDebugInfo)
            Debug.Log($"{containerName}: 물 갈기 버튼 클릭!");

        // 물 리셋
        ResetWater();

        // UI 숨김
        if (changeWaterButtonCanvas != null)
            changeWaterButtonCanvas.SetActive(false);
    }

    // ===== Water Management =====
    void SetWaterColor(Color color)
    {
        if (waterMaterialInstance == null)
            return;

        if (waterMaterialInstance.HasProperty(waterColorPropertyName))
        {
            waterMaterialInstance.SetColor(waterColorPropertyName, color);
        }
        else
        {
            Debug.LogWarning($"{containerName}: 물 머티리얼에 '{waterColorPropertyName}' 속성이 없습니다!");
        }
    }

    void ResetWater()
    {
        // 헹굼 횟수 초기화
        currentRinseCount = 0;

        // 물 색상 초기화
        SetWaterColor(cleanWaterColor);

        if (showDebugInfo)
            Debug.Log($"{containerName}: 물 갈기 완료! 깨끗한 물로 리셋되었습니다.");
    }

    // ===== Public Methods (청소 도구에서 호출) =====
    /// <summary>
    /// 청소 도구가 헹굼 모션을 완료했을 때 호출
    /// </summary>
    /// <param name="toolDirtyRatio">청소 도구의 더러운 정도 (0~1)</param>
    /// <param name="toolName">청소 도구 이름 (디버그용, 선택)</param>
    public void OnToolRinsed(float toolDirtyRatio = 1.0f, string toolName = "청소 도구")
    {
        if (!isWaterFilled)
        {
            if (showDebugInfo)
                Debug.Log($"{containerName}: 물이 없어서 헹굴 수 없습니다!");
            return;
        }

        if (currentRinseCount >= maxRinseCount)
        {
            if (showDebugInfo)
                Debug.Log($"{containerName}: 물이 너무 더러워서 더 이상 헹굴 수 없습니다!");
            return;
        }

        // 청소 도구 더러움 정도에 비례해서 물 오염 증가
        float dirtinessAmount = toolDirtyRatio;

        // 헹굼 횟수 증가
        currentRinseCount += dirtinessAmount;

        // 최대 횟수 제한
        if (currentRinseCount > maxRinseCount)
            currentRinseCount = maxRinseCount;

        // 물 더러워지기
        float dirtyRatio = Mathf.Clamp01(currentRinseCount / maxRinseCount);
        Color newColor = Color.Lerp(cleanWaterColor, dirtyWaterColor, dirtyRatio);
        SetWaterColor(newColor);

        if (showDebugInfo)
        {
            Debug.Log($"💧 {containerName} 물 더러워짐:");
            Debug.Log($"  - {toolName} 더러움: {toolDirtyRatio * 100:F0}%");
            Debug.Log($"  - 물 오염 증가량: {dirtinessAmount:F2}");
            Debug.Log($"  - 현재 오염도: {currentRinseCount:F1}/{maxRinseCount} ({dirtyRatio * 100:F0}%)");
        }

        // 최대 횟수 도달 시
        if (currentRinseCount >= maxRinseCount)
        {
            if (showDebugInfo)
                Debug.Log($"❌ {containerName} 물이 최대로 더러워졌습니다! 물을 갈아주세요.");
        }
    }

    /// <summary>
    /// 현재 물을 사용할 수 있는지 확인
    /// </summary>
    public bool CanRinse()
    {
        return isWaterFilled && currentRinseCount < maxRinseCount;
    }

    /// <summary>
    /// 현재 헹굼 횟수 반환
    /// </summary>
    public float GetCurrentRinseCount()
    {
        return currentRinseCount;
    }

    /// <summary>
    /// 물이 활성화되어 있는지 확인
    /// </summary>
    public bool IsWaterFilled()
    {
        return isWaterFilled;
    }

    /// <summary>
    /// 컨테이너 이름 반환
    /// </summary>
    public string GetContainerName()
    {
        return containerName;
    }
}