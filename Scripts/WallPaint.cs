using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

public class WallPaint : MonoBehaviour
{
    [Header("플레이어 위치 확인")]
    [Tooltip("플레이어의 XR Origin (카메라 리그)")]
    public Transform playerOrigin;

    [Tooltip("바닥 체크 높이 (짧게 설정하세요: 0.1~0.3)")]
    public float floorCheckDistance = 0.15f;

    [Tooltip("레이캐스트 시작 높이 (바닥에서 약간 위)")]
    public float rayStartHeight = 0.05f;

    [Header("컨트롤러 설정")]
    [Tooltip("오른손/왼손 컨트롤러 특성")]
    public InputDeviceCharacteristics controllerCharacteristics = InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;

    [Header("디버그")]
    [Tooltip("콘솔에 디버그 정보 표시")]
    public bool showDebugInfo = true;

    [Tooltip("Scene 뷰에서 레이캐스트 표시")]
    public bool showRaycastGizmo = true;

    // 현재 플레이어가 있는 방 (바닥 태그)
    private string currentRoomTag = "";

    // 컨트롤러 입력
    private InputDevice targetDevice;

    // 디버그용 레이캐스트 정보
    private Vector3 lastRayStart;
    private Vector3 lastRayEnd;
    private bool lastRayHit = false;

    void Start()
    {
        // 플레이어 Origin 자동 찾기
        if (playerOrigin == null)
        {
            GameObject xrOrigin = GameObject.Find("XR Origin");
            if (xrOrigin != null)
            {
                playerOrigin = xrOrigin.transform;
            }
            else
            {
                Debug.LogError("XR Origin을 찾을 수 없습니다! Inspector에서 수동으로 설정하세요.");
            }
        }

        // 컨트롤러 초기화
        InitializeController();
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
        // 컨트롤러가 연결 안 되어 있으면 다시 시도
        if (!targetDevice.isValid)
        {
            InitializeController();
        }

        // 플레이어 현재 위치(방) 확인
        CheckCurrentRoom();
    }

    // 플레이어가 현재 어느 방에 있는지 확인
    void CheckCurrentRoom()
    {
        if (playerOrigin == null)
            return;

        // 플레이어 발 위치에서 약간 위에서 레이캐스트 시작
        Vector3 rayStart = playerOrigin.position;
        rayStart.y += rayStartHeight; // 바닥에서 약간 위

        Vector3 rayDirection = Vector3.down;

        // 디버그용 저장
        lastRayStart = rayStart;

        RaycastHit hit;
        if (Physics.Raycast(rayStart, rayDirection, out hit, floorCheckDistance))
        {
            lastRayHit = true;
            lastRayEnd = hit.point;

            // 바닥의 태그 확인
            string floorTag = hit.collider.tag;

            if (showDebugInfo)
            {
                // 매 프레임 현재 감지된 바닥 정보 표시
                //Debug.Log($"[감지] 오브젝트: {hit.collider.gameObject.name}, 태그: {floorTag}, 거리: {hit.distance:F3}m");
            }

            // 태그가 변경되었을 때만 방 변경
            if (floorTag != currentRoomTag && floorTag != "Untagged")
            {
                currentRoomTag = floorTag;

                if (showDebugInfo)
                {
                    Debug.Log($"★★★ 방 변경됨 → {currentRoomTag} ★★★");
                }
            }
        }
        else
        {
            lastRayHit = false;
            lastRayEnd = rayStart + rayDirection * floorCheckDistance;

            if (showDebugInfo)
            {
                Debug.LogWarning("바닥을 감지하지 못했습니다!");
            }
        }
    }

    // UI 버튼에서 호출할 Public 함수
    // Unity Button의 OnClick()에 이 함수를 연결하세요
    public void ApplyPaintColorByName(string colorName)
    {
        // 색상 이름으로 색상 가져오기
        Color selectedColor = GetColorByName(colorName);

        if (selectedColor == Color.clear)
        {
            Debug.LogError($"알 수 없는 색상 이름: {colorName}");
            return;
        }

        ApplyPaintToWalls(selectedColor, colorName);
    }

    // UI 버튼에서 직접 Color를 전달하는 함수
    public void ApplyPaintColorDirect(Color selectedColor)
    {
        string colorName = ColorToName(selectedColor);
        ApplyPaintToWalls(selectedColor, colorName);
    }

    // 페인트를 현재 방의 모든 벽에 적용
    void ApplyPaintToWalls(Color selectedPaintColor, string colorName)
    {
        if (showDebugInfo)
        {
            Debug.Log($"페인트 선택: {colorName} (RGB: {selectedPaintColor})");
        }

        // 현재 방이 어디인지 확인
        if (string.IsNullOrEmpty(currentRoomTag) || currentRoomTag == "Untagged")
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("현재 위치를 확인할 수 없습니다! 바닥에 태그를 설정하세요.");
            }

            // 실패 진동
            if (targetDevice.isValid)
            {
                targetDevice.SendHapticImpulse(0, 0.1f, 0.05f);
            }
            return;
        }

        // 현재 방의 벽 태그 생성
        string wallTag = currentRoomTag + "Wall";

        if (showDebugInfo)
        {
            Debug.Log($"찾는 벽 태그: {wallTag}");
        }

        // Scene에서 해당 태그를 가진 모든 벽 찾기
        GameObject[] walls = GameObject.FindGameObjectsWithTag(wallTag);

        if (walls.Length == 0)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning($"'{wallTag}' 태그를 가진 벽을 찾을 수 없습니다!");
            }

            // 실패 진동
            if (targetDevice.isValid)
            {
                targetDevice.SendHapticImpulse(0, 0.1f, 0.05f);
            }
            return;
        }

        // 찾은 모든 벽의 색상 변경
        int paintedWallCount = 0;
        foreach (GameObject wall in walls)
        {
            Renderer wallRenderer = wall.GetComponent<Renderer>();
            if (wallRenderer != null)
            {
                wallRenderer.material.color = selectedPaintColor;
                paintedWallCount++;
            }
        }

        // 성공 메시지 및 피드백
        if (paintedWallCount > 0)
        {
            if (showDebugInfo)
            {
                Debug.Log($"벽 페인팅 완료! {paintedWallCount}개의 벽을 {colorName} 색상으로 변경했습니다.");
            }

            // 성공 진동 (강함)
            if (targetDevice.isValid)
            {
                targetDevice.SendHapticImpulse(0, 0.6f, 0.25f);
            }
        }
    }

    // 색상 이름을 Color로 변환
    Color GetColorByName(string colorName)
    {
        switch (colorName.ToLower())
        {
            case "red": return Color.red;
            case "blue": return Color.blue;
            case "green": return Color.green;
            case "yellow": return Color.yellow;
            case "white": return Color.white;
            case "black": return Color.black;
            case "cyan": return Color.cyan;
            case "magenta": return Color.magenta;
            case "gray": return Color.gray;

            // 커스텀 색상 추가 가능
            case "beige": return new Color(0.96f, 0.96f, 0.86f);
            case "mint": return new Color(0.737255f, 0.905882f, 0.729412f);
            case "pink": return new Color(0.866667f, 0.701961f, 0.866667f);
            case "lightyellow": return new Color(0.945098f, 0.929412f, 0.752941f);
            case "skyblue": return new Color(0.709804f, 0.701961f, 0.831373f);
            case "lavender": return new Color(0.9f, 0.9f, 0.98f);

            default:
                Debug.LogWarning($"알 수 없는 색상: {colorName}");
                return Color.clear;
        }
    }

    // Color를 이름으로 변환 (로그용)
    string ColorToName(Color color)
    {
        if (color == Color.red) return "Red";
        if (color == Color.blue) return "Blue";
        if (color == Color.green) return "Green";
        if (color == Color.yellow) return "Yellow";
        if (color == Color.white) return "White";
        return "Custom Color";
    }

    // 기즈모로 레이캐스트 표시 (Scene 뷰에서 보임)
    void OnDrawGizmos()
    {
        if (!showRaycastGizmo || playerOrigin == null)
            return;

        // 레이캐스트 선 그리기
        if (lastRayHit)
        {
            // 히트했을 때 - 초록색
            Gizmos.color = Color.green;
            Gizmos.DrawLine(lastRayStart, lastRayEnd);
            Gizmos.DrawWireSphere(lastRayEnd, 0.05f);
        }
        else
        {
            // 히트 안 했을 때 - 빨간색
            Gizmos.color = Color.red;
            Gizmos.DrawLine(lastRayStart, lastRayEnd);
        }

        // 시작점 표시
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(lastRayStart, 0.03f);
    }
}