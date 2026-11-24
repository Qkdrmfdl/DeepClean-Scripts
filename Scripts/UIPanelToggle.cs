using UnityEngine;
using UnityEngine.XR;

public class UIPanelToggle : MonoBehaviour
{
    [Header("UI 패널 설정")]
    [Tooltip("표시할 UI 패널 (Canvas)")]
    public GameObject uiPanel;

    [Header("UI 위치 설정")]
    [Tooltip("플레이어 카메라 (Main Camera 또는 XR Camera)")]
    public Transform playerCamera;

    [Tooltip("카메라로부터의 거리")]
    public float distanceFromCamera = 1.5f;

    [Tooltip("수평 오프셋 (양수: 오른쪽, 음수: 왼쪽)")]
    public float horizontalOffset = -0.3f;

    [Tooltip("수직 오프셋 (양수: 위, 음수: 아래)")]
    public float verticalOffset = 0f;

    [Header("컨트롤러 설정")]
    [Tooltip("왼쪽 컨트롤러 사용 (Y 버튼은 왼쪽에 있음)")]
    public InputDeviceCharacteristics controllerCharacteristics =
        InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller;

    [Header("추가 옵션")]
    [Tooltip("UI가 항상 카메라 방향으로 회전")]
    public bool alwaysFaceCamera = true;

    [Tooltip("UI가 플레이어 따라다니도록 설정")]
    public bool followPlayer = true;

    [Tooltip("트레일러 촬영용: 체크하면 UI가 씬에 배치된 위치에 고정됨")]
    public bool trailerMode = false;

    [Tooltip("따라다니는 속도 (부드러울수록 느림)")]
    [Range(1f, 10f)]
    public float followSpeed = 5f;

    [Tooltip("UI 열릴 때 효과음 (선택사항)")]
    public AudioClip openSound;

    [Tooltip("UI 닫힐 때 효과음 (선택사항)")]
    public AudioClip closeSound;

    [Header("디버그")]
    [Tooltip("콘솔에 정보를 로그 표시")]
    public bool showDebugInfo = true;

    // 내부 변수
    private InputDevice targetDevice;
    private bool xButtonPressedLastFrame = false;
    private float lastToggleTime = -1f;
    private const float toggleCooldown = 0.3f;
    private bool isPanelOpen = false;
    private AudioSource audioSource;
    private bool _controllerWarningLogged = false;
    private bool _inputBlocked = false;

    /// <summary>패널이 현재 열려 있는지 (외부에서 조회용)</summary>
    public bool IsOpen => isPanelOpen;

    // 외부에서 X버튼 입력을 차단/해제 (데모 중 패드 토글 방지)
    public void SetInputBlocked(bool blocked)
    {
        _inputBlocked = blocked;
        if (blocked) ClosePanel(); // 차단 시 열려 있던 패드도 닫음
    }

    void Start()
    {
        // 카메라 자동 찾기
        if (playerCamera == null)
        {
            playerCamera = Camera.main?.transform;

            if (playerCamera == null)
            {
                // XR Origin의 Main Camera 찾기
                GameObject xrOrigin = GameObject.Find("XR Origin");
                if (xrOrigin != null)
                {
                    Transform cameraOffset = xrOrigin.transform.Find("Camera Offset");
                    if (cameraOffset != null)
                    {
                        playerCamera = cameraOffset.Find("Main Camera");
                        if (playerCamera == null)
                        {
                            playerCamera = cameraOffset.Find("Camera");
                        }
                    }
                }
            }

            if (playerCamera != null && showDebugInfo)
            {
                Debug.Log($"카메라 자동 발견: {playerCamera.name}");
            }
        }

        if (playerCamera == null)
        {
            Debug.LogError("플레이어 카메라를 찾을 수 없습니다!");
        }

        // UI 패널 초기 설정 (숨김)
        if (uiPanel != null)
        {
            uiPanel.SetActive(false);
            isPanelOpen = false;
        }
        else
        {
            Debug.LogError("UI 패널이 할당되지 않았습니다!");
        }

        // 컨트롤러 초기화
        InitializeController();

        // 오디오 소스 설정
        if (openSound != null || closeSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D 사운드
        }
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
                Debug.Log($"컨트롤러 연결됨: {targetDevice.name}");
            }
        }
        else
        {
            if (showDebugInfo && !_controllerWarningLogged)
            {
                _controllerWarningLogged = true;
                Debug.LogWarning("[UIPanelToggle] 왼쪽 컨트롤러를 찾을 수 없습니다. X 버튼 입력이 작동하지 않습니다.");
            }
        }
    }

    void Update()
    {
        // 컨트롤러가 연결 안 되어 있으면 다시 시도
        if (!targetDevice.isValid)
        {
            InitializeController();
        }

        // X 버튼 입력 체크 (차단 중이면 무시)
        if (!_inputBlocked && IsXButtonPressed())
        {
            TogglePanel();
        }

        // 패널이 열려 있을 때
        if (isPanelOpen && uiPanel != null && playerCamera != null && !trailerMode)
        {
            // 플레이어 따라다니도록 설정되어 있으면
            if (followPlayer)
            {
                UpdatePanelPosition();
            }

            // 항상 카메라 방향으로 설정되어 있으면
            if (alwaysFaceCamera)
            {
                FaceCamera();
            }
        }
    }

    // X 버튼이 눌렸는지 확인
    bool IsXButtonPressed()
    {
        if (!targetDevice.isValid)
        {
            xButtonPressedLastFrame = false;
            return false;
        }

        // X 버튼 (primaryButton - 왼쪽 컨트롤러)
        bool xButtonPressed = false;
        if (targetDevice.TryGetFeatureValue(CommonUsages.primaryButton, out bool primaryButtonValue))
        {
            xButtonPressed = primaryButtonValue;
        }

        bool wasPressed = xButtonPressedLastFrame;
        xButtonPressedLastFrame = xButtonPressed;

        if (xButtonPressed && !wasPressed && Time.time - lastToggleTime > toggleCooldown)
        {
            lastToggleTime = Time.time;
            return true;
        }

        return false;
    }

    // UI 패널 토글
    void TogglePanel()
    {
        if (uiPanel == null || playerCamera == null)
            return;

        isPanelOpen = !isPanelOpen;
        //Debug.Log($"[UIPanelToggle:{GetInstanceID()}] TogglePanel called → isPanelOpen={isPanelOpen}, Time={Time.time:F3}, lastToggleTime={lastToggleTime:F3}");
        uiPanel.SetActive(isPanelOpen);

        if (isPanelOpen)
        {
            // UI 패널을 카메라 앞 정면에 위치 (트레일러 모드면 고정 위치 유지)
            if (!trailerMode) PositionPanel();

            // 효과음 재생
            if (audioSource != null && openSound != null)
            {
                audioSource.PlayOneShot(openSound);
            }

            if (showDebugInfo)
            {
                Debug.Log("UI 패널 열림");
            }
        }
        else
        {
            // 효과음 재생
            if (audioSource != null && closeSound != null)
            {
                audioSource.PlayOneShot(closeSound);
            }

            if (showDebugInfo)
            {
                Debug.Log("UI 패널 닫힘");
            }
        }

        // 햅틱 피드백
        if (targetDevice.isValid)
        {
            targetDevice.SendHapticImpulse(0, 0.3f, 0.1f);
        }
    }

    // UI 패널을 카메라 앞 정면에 위치
    void PositionPanel()
    {
        // 카메라의 앞 방향
        Vector3 forward = playerCamera.forward;
        forward.y = 0; // 수평으로만 (선택사항)
        forward.Normalize();

        // 카메라의 오른쪽 방향
        Vector3 right = playerCamera.right;
        right.y = 0;
        right.Normalize();

        // 위 방향
        Vector3 up = Vector3.up;

        // 목표 위치 계산
        Vector3 targetPosition = playerCamera.position
            + forward * distanceFromCamera
            + right * horizontalOffset
            + up * verticalOffset;

        uiPanel.transform.position = targetPosition;

        // 카메라 방향으로 회전
        FaceCamera();
    }

    // UI 패널을 플레이어가 부드럽게 따라다니도록 위치 업데이트
    void UpdatePanelPosition()
    {
        // 목표 위치 계산
        Vector3 forward = playerCamera.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 right = playerCamera.right;
        right.y = 0;
        right.Normalize();

        Vector3 targetPosition = playerCamera.position
            + forward * distanceFromCamera
            + right * horizontalOffset
            + Vector3.up * verticalOffset;

        // ToolSwitcher UI처럼 즉각 추적 (Lerp 없이 직접 대입)
        uiPanel.transform.position = targetPosition;
    }

    // UI를 카메라 방향으로 즉시 회전
    void FaceCamera()
    {
        Vector3 directionToCamera = playerCamera.position - uiPanel.transform.position;
        directionToCamera.y = 0; // 수평 회전만 (선택사항)

        if (directionToCamera != Vector3.zero)
        {
            uiPanel.transform.rotation = Quaternion.LookRotation(-directionToCamera);
        }
    }

    // UI를 카메라 방향으로 부드럽게 회전
    void FaceCameraSmooth()
    {
        Vector3 directionToCamera = playerCamera.position - uiPanel.transform.position;
        directionToCamera.y = 0;

        if (directionToCamera != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(-directionToCamera);
            uiPanel.transform.rotation = Quaternion.Slerp(
                uiPanel.transform.rotation,
                targetRotation,
                Time.deltaTime * 5f
            );
        }
    }

    // 패널을 열거나 닫는 Public 함수 (외부에서 호출 가능)
    public void OpenPanel()
    {
        if (!isPanelOpen)
        {
            TogglePanel();
        }
    }

    public void ClosePanel()
    {
        if (isPanelOpen)
        {
            TogglePanel();
        }
    }

    // 에디터에서 UI 위치 시각화
    void OnDrawGizmosSelected()
    {
        if (playerCamera != null)
        {
            Vector3 forward = playerCamera.forward;
            forward.y = 0;
            forward.Normalize();

            Vector3 right = playerCamera.right;
            right.y = 0;
            right.Normalize();

            Vector3 targetPosition = playerCamera.position
                + forward * distanceFromCamera
                + right * horizontalOffset
                + Vector3.up * verticalOffset;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(targetPosition, new Vector3(0.5f, 0.3f, 0.1f));
            Gizmos.DrawLine(playerCamera.position, targetPosition);
        }
    }
}
