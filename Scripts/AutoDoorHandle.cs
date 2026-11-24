using UnityEngine;

public class AutoDoorHandle : MonoBehaviour
{
    [Header("문 설정")]
    [Tooltip("회전할 문 오브젝트")]
    public Transform door;

    [Tooltip("열린 각도 범위 (0~180)")]
    public float openAngle = 90f;

    [Tooltip("여닫는 속도")]
    public float openSpeed = 2f;

    [Tooltip("회전할 축 (Y축 = 위아래 회전)")]
    public Vector3 rotationAxis = Vector3.up;

    [Header("자동 닫힘 설정")]
    [Tooltip("자동으로 닫히는 여부")]
    public bool autoClose = true;

    [Tooltip("열린 후 자동으로 닫히는 시간")]
    public float autoCloseDelay = 3f;

    [Header("디버그")]
    public bool showDebugInfo = true;

    private Quaternion closedRotation; // 닫힌 상태의 회전값
    private Quaternion openRotation;   // 열린 상태의 회전값
    private bool isOpen = false;
    private bool isMoving = false;
    private float autoCloseTimer = 0f;

    void Start()
    {
        if (door == null)
        {
            Debug.LogError("문 오브젝트가 할당되지 않았습니다!");
            return;
        }

        // 초기 회전값 저장 (닫힌 상태)
        closedRotation = door.localRotation;

        // 열린 상태의 회전값 계산
        openRotation = closedRotation * Quaternion.AngleAxis(openAngle, rotationAxis);

        if (showDebugInfo)
        {
            Debug.Log($"문 초기화 완료: {door.name}");
        }
    }

    void Update()
    {
        // 문이 열려있고 자동 닫힘이 활성화되어 있으면
        if (isOpen && autoClose && !isMoving)
        {
            autoCloseTimer += Time.deltaTime;

            if (autoCloseTimer >= autoCloseDelay)
            {
                CloseDoor();
            }
        }

        // 문 애니메이션 처리
        if (isMoving)
        {
            AnimateDoor();
        }
    }

    // 컨트롤러(또는 손)가 트리거에 닿았을 때
    void OnTriggerEnter(Collider other)
    {
        // 컨트롤러 태그 확인 (XR Controller의 태그)
        if (other.CompareTag("Player") ||
            other.CompareTag("Cleanable_Hand") ||
            other.name.Contains("Controller"))
        {
            if (showDebugInfo)
            {
                Debug.Log($"트리거 감지: {other.name}");
            }

            // 닫혀 있으면 열기
            if (!isOpen)
            {
                OpenDoor();
            }
            // 열려 있으면 닫기
            else
            {
                CloseDoor();
            }
        }
    }

    void OpenDoor()
    {
        if (isMoving) return;

        isOpen = true;
        isMoving = true;
        autoCloseTimer = 0f;

        if (showDebugInfo)
        {
            Debug.Log("문 열기 시작");
        }
    }

    void CloseDoor()
    {
        if (isMoving) return;

        isOpen = false;
        isMoving = true;

        if (showDebugInfo)
        {
            Debug.Log("문 닫기 시작");
        }
    }

    void AnimateDoor()
    {
        Quaternion targetRotation = isOpen ? openRotation : closedRotation;

        door.localRotation = Quaternion.Slerp(
            door.localRotation,
            targetRotation,
            Time.deltaTime * openSpeed
        );

        // 목표 위치에 충분히 가까워지면 애니메이션 종료
        if (Quaternion.Angle(door.localRotation, targetRotation) < 0.5f)
        {
            door.localRotation = targetRotation;
            isMoving = false;

            if (showDebugInfo)
            {
                Debug.Log($"문 {(isOpen ? "열기" : "닫기")} 완료");
            }
        }
    }

    // 외부에서 문 열기/닫기 (버튼 등 호출 가능)
    public void ToggleDoor()
    {
        if (isOpen)
        {
            CloseDoor();
        }
        else
        {
            OpenDoor();
        }
    }
}
