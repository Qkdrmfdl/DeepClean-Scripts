using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class FurniturePlacementManager : MonoBehaviour
{
    [Header("필수 연결")]
    public Transform vrCamera; // VR 카메라
    public LayerMask placementLayer; // 배치 가능한 레이어 (바닥)
    public GameObject confirmPanel; // Buy/Cancel 버튼 패널
    public XRNode rotationController = XRNode.RightHand; // 회전 조작 컨트롤러

    [Header("배치 설정")]
    public float placementDistance = 2f; // 카메라 앞 배치 거리
    public float rotationSnapAngle = 45f; // 회전 각도 (45도씩)
    public float rotationCooldown = 0.3f; // 회전 입력 대기 시간

    [Header("그리드 스냅 설정")]
    public bool enableGridSnap = true;
    public float defaultGridSize = 0.5f; // 기본 그리드 크기 (50cm)

    [Header("MoneyManager 연동")]
    public MoneyManager moneyManager;

    [Header("디버그")]
    public bool showDebugInfo = true;

    private GameObject currentPreviewFurniture; // 현재 미리보기 가구
    private FurnitureData currentFurnitureData;
    private bool isPlacementMode = false;
    private bool isValidPlacement = true; // 배치 가능 여부
    private Material[] originalMaterials; // 원본 재질 저장
    private Collider previewCollider; // 충돌 체크용

    // XR Input
    private InputDevice rightController;
    private float lastRotationTime = 0f;

    void Start()
    {
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false); // 시작시 버튼 패널 숨김
        }

        // MoneyManager 자동 찾기
        if (moneyManager == null)
            moneyManager = FindFirstObjectByType<MoneyManager>();

        if (moneyManager == null)
            Debug.LogWarning("[FurniturePlacementManager] MoneyManager를 찾을 수 없습니다!");

        // XR 컨트롤러 초기화
        InitializeControllers();
    }

    void InitializeControllers()
    {
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(rotationController, devices);

        if (devices.Count > 0)
        {
            rightController = devices[0];
            if (showDebugInfo)
                Debug.Log($"컨트롤러 연결됨: {rightController.name}");
        }
    }

    // UI 버튼에서 호출: 가구 배치 시작
    public void StartFurniturePlacement(FurnitureData furnitureData)
    {
        // 이미 배치 중이면 취소
        if (isPlacementMode)
        {
            CancelPlacement();
        }

        isPlacementMode = true;
        currentFurnitureData = furnitureData;

        // 프리뷰 가구 생성
        currentPreviewFurniture = Instantiate(furnitureData.prefab);
        currentPreviewFurniture.name = "Preview_" + furnitureData.furnitureName;

        // 원본 재질 저장
        SaveOriginalMaterials();

        // 충돌 체크용 트리거 콜라이더 추가
        SetupPreviewCollider();

        // Buy/Cancel 버튼 패널 표시
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(true);
        }

        if (showDebugInfo)
            Debug.Log($"배치 모드 시작: {furnitureData.furnitureName} (가격: {furnitureData.price}원)");
    }

    void Update()
    {
        if (isPlacementMode && currentPreviewFurniture != null)
        {
            UpdateFurniturePosition();
            HandleRotationInput();
            UpdatePlacementVisual(); // 배치 가능 여부에 따라 색상 변경
        }
    }

    void UpdateFurniturePosition()
    {
        if (vrCamera == null) return;

        // VR 카메라 정면 방향으로 레이 쏘기
        Ray ray = new Ray(vrCamera.position, vrCamera.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, placementDistance * 2, placementLayer))
        {
            Vector3 targetPosition = hit.point;

            // 그리드
            if (enableGridSnap && currentFurnitureData.useGridSnap)
                targetPosition = SnapToGrid(targetPosition, currentFurnitureData.gridSize);

            targetPosition.y = 0.5f;

            currentPreviewFurniture.transform.position = targetPosition;
        }
        else
        {
            Vector3 targetPosition = vrCamera.position + vrCamera.forward * placementDistance;

            if (enableGridSnap && currentFurnitureData.useGridSnap)
                targetPosition = SnapToGrid(targetPosition, currentFurnitureData.gridSize);

            targetPosition.y = 0.5f;

            currentPreviewFurniture.transform.position = targetPosition;
        }
    }

    // 그리드에 맞춰 위치 보정
    Vector3 SnapToGrid(Vector3 position, float gridSize)
    {
        position.x = Mathf.Round(position.x / gridSize) * gridSize;
        position.z = Mathf.Round(position.z / gridSize) * gridSize;
        // Y축은 바닥에 맞춤
        return position;
    }

    void HandleRotationInput()
    {
        // 컨트롤러 재연결 체크
        if (!rightController.isValid)
        {
            InitializeControllers();
            return;
        }

        // 쿨다운 체크
        if (Time.time - lastRotationTime < rotationCooldown)
        {
            return;
        }

        // 오른쪽 컨트롤러 썸스틱 값 가져오기
        Vector2 thumbstickValue;
        if (rightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out thumbstickValue))
        {
            // 좌우 입력이 일정 값 이상일 때만 회전
            if (thumbstickValue.x > 0.7f)
            {
                // 오른쪽으로 회전
                currentPreviewFurniture.transform.Rotate(Vector3.up, rotationSnapAngle);
                lastRotationTime = Time.time;

                if (showDebugInfo)
                    Debug.Log($"오른쪽 회전: {rotationSnapAngle}도");
            }
            else if (thumbstickValue.x < -0.7f)
            {
                // 왼쪽으로 회전
                currentPreviewFurniture.transform.Rotate(Vector3.up, -rotationSnapAngle);
                lastRotationTime = Time.time;

                if (showDebugInfo)
                    Debug.Log($"왼쪽 회전: {-rotationSnapAngle}도");
            }
        }
    }

    // 충돌 체크용 콜라이더 설정
    void SetupPreviewCollider()
    {
        // 기존 콜라이더는 비활성화
        Collider[] existingColliders = currentPreviewFurniture.GetComponentsInChildren<Collider>();
        foreach (Collider col in existingColliders)
        {
            col.enabled = false;
        }

        // 새 트리거 콜라이더 추가
        BoxCollider boxCollider = currentPreviewFurniture.AddComponent<BoxCollider>();
        boxCollider.isTrigger = true;
        previewCollider = boxCollider;

        // 콜라이더 크기 자동 계산
        Renderer[] renderers = currentPreviewFurniture.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }

            boxCollider.center = bounds.center - currentPreviewFurniture.transform.position;
            boxCollider.size = bounds.size * 1.1f; // 약간 여유 공간
        }

        // 충돌 체크 컴포넌트 추가
        PreviewCollisionChecker checker = currentPreviewFurniture.AddComponent<PreviewCollisionChecker>();
        checker.placementManager = this;
    }

    // 배치 가능 여부 업데이트 (PreviewCollisionChecker에서 호출)
    public void SetPlacementValid(bool valid)
    {
        isValidPlacement = valid;
    }

    // 배치 가능 여부에 따라 색상 변경
    void UpdatePlacementVisual()
    {
        Renderer[] renderers = currentPreviewFurniture.GetComponentsInChildren<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            Material[] mats = renderer.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                Color originalColor = mats[i].color;

                if (isValidPlacement)
                {
                    // 원래 색 유지 + 약간 투명
                    mats[i].color = new Color(originalColor.r, originalColor.g, originalColor.b, 255f);
                }
                else
                {
                    // 충돌 시 원래 색 유지 + 빨간빛 Tint + 반투명
                    mats[i].color = new Color(
                        Mathf.Max(originalColor.r, 1f),
                        originalColor.g * 0.3f,
                        originalColor.b * 0.3f,
                        0.5f
                    );
                }

                SetMaterialTransparent(mats[i]); // 투명 처리 유지
            }

            renderer.materials = mats;
        }
    }

    void SetMaterialTransparent(Material mat)
    {
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
    }

    void SaveOriginalMaterials()
    {
        Renderer[] renderers = currentPreviewFurniture.GetComponentsInChildren<Renderer>();
        int totalMats = 0;
        foreach (Renderer r in renderers)
        {
            totalMats += r.materials.Length;
        }

        originalMaterials = new Material[totalMats];
        int index = 0;

        foreach (Renderer renderer in renderers)
        {
            foreach (Material mat in renderer.materials)
            {
                originalMaterials[index] = new Material(mat);
                index++;
            }
        }
    }

    // Buy 버튼 클릭시 호출
    public void ConfirmPurchase()
    {
        if (!isPlacementMode || currentPreviewFurniture == null)
        {
            if (showDebugInfo)
                Debug.LogWarning("배치 모드가 아닙니다.");
            return;
        }

        // 충돌 체크: 다른 가구와 겹치면 구매 불가
        if (!isValidPlacement)
        {
            if (showDebugInfo)
                Debug.LogWarning("배치할 수 없는 위치입니다. 다른 가구와 겹칩니다!");
            return;
        }

        // MoneyManager 확인
        if (moneyManager == null)
        {
            Debug.LogError("MoneyManager를 찾을 수 없습니다!");
            return;
        }

        // 돈이 충분한지 확인
        int currentMoney = moneyManager.GetCurrentMoney();
        if (currentMoney < currentFurnitureData.price)
        {
            if (showDebugInfo)
                Debug.LogWarning($"돈이 부족합니다! 필요: {currentFurnitureData.price:N0}원, 보유: {currentMoney:N0}원");
            return;
        }

        // 돈 차감 (MoneyManager 사용)
        bool success = moneyManager.SpendMoney(currentFurnitureData.price);

        if (!success)
        {
            if (showDebugInfo)
                Debug.LogWarning("구매 실패!");
            return;
        }

        if (showDebugInfo)
            Debug.Log($"가구 구매 완료! {currentFurnitureData.furnitureName} ({currentFurnitureData.price:N0}원)");

        // 프리뷰를 실제 가구로 전환
        RestoreOriginalMaterials();

        // 충돌 체크용 트리거 제거
        Destroy(previewCollider);
        Destroy(currentPreviewFurniture.GetComponent<PreviewCollisionChecker>());

        // 실제 콜라이더 활성화
        Collider[] colliders = currentPreviewFurniture.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = true;
        }

        // VR에서 잡을 수 있게 만들기
        AddGrabInteraction(currentPreviewFurniture);

        // 구매한 가구 목록에 추가
        PlacedFurnitureManager.Instance.AddFurniture(currentPreviewFurniture, currentFurnitureData);

        // 이름 변경
        currentPreviewFurniture.name = currentFurnitureData.furnitureName;

        // 배치 모드 종료
        isPlacementMode = false;
        currentPreviewFurniture = null;

        // 버튼 패널 숨기기
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }
    }

    // Cancel 버튼 클릭시 호출
    public void CancelPlacement()
    {
        if (currentPreviewFurniture != null)
        {
            Destroy(currentPreviewFurniture);
        }

        isPlacementMode = false;
        currentPreviewFurniture = null;

        // 버튼 패널 숨기기
        if (confirmPanel != null)
        {
            confirmPanel.SetActive(false);
        }

        if (showDebugInfo)
            Debug.Log("배치 취소됨");
    }

    void RestoreOriginalMaterials()
    {
        Renderer[] renderers = currentPreviewFurniture.GetComponentsInChildren<Renderer>();
        int index = 0;

        foreach (Renderer renderer in renderers)
        {
            Material[] mats = new Material[renderer.materials.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                if (index < originalMaterials.Length)
                {
                    mats[i] = originalMaterials[index];
                    index++;
                }
            }
            renderer.materials = mats;
        }
    }

    void AddGrabInteraction(GameObject furniture)
    {
        // XR Grab Interactable 추가 (VR에서 잡을 수 있게)
        XRGrabInteractable grabInteractable = furniture.AddComponent<XRGrabInteractable>();

        // Rigidbody 추가
        Rigidbody rb = furniture.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = furniture.AddComponent<Rigidbody>();
        }
        rb.useGravity = false;
        rb.isKinematic = false;

        grabInteractable.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grabInteractable.throwOnDetach = false;
    }
}

// 충돌 체크용 헬퍼 클래스
public class PreviewCollisionChecker : MonoBehaviour
{
    public FurniturePlacementManager placementManager;
    private int collisionCount = 0;

    void OnTriggerEnter(Collider other)
    {
        // 바닥과 본인은 무시
        if (other.gameObject.layer == LayerMask.NameToLayer("Floor") ||
            other.transform.IsChildOf(transform))
        {
            return;
        }

        collisionCount++;
        placementManager.SetPlacementValid(false);
        Debug.Log($"충돌 감지: {other.gameObject.name}, 총 {collisionCount}개");
    }

    void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Floor") ||
            other.transform.IsChildOf(transform))
        {
            return;
        }

        collisionCount--;
        if (collisionCount <= 0)
        {
            collisionCount = 0;
            placementManager.SetPlacementValid(true);
            Debug.Log("배치 가능");
        }
    }
}