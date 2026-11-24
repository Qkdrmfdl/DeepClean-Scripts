using UnityEngine;
using TMPro;

public class ToolNameUIFollow : MonoBehaviour
{
    public Camera targetCamera;
    public float distanceFromCamera = 2f;
    public float yOffset = 0.5f;
    [Range(0f, 0.9f)]
    public float smoothSpeed = 0.1f;
    public TextMeshProUGUI textComponent;

    private Vector3 targetPosition;
    private Quaternion targetRotation;

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (textComponent == null)
            textComponent = GetComponentInChildren<TextMeshProUGUI>();
    }

    void LateUpdate()
    {
        if (targetCamera == null) return;

        // 위치
        targetPosition = targetCamera.transform.position + targetCamera.transform.forward * distanceFromCamera + targetCamera.transform.up * yOffset;
        transform.position = Vector3.Lerp(transform.position, targetPosition, 1f - smoothSpeed);

        // 회전: Y축만 맞춰서 카메라를 바라보게
        Vector3 lookDirection = transform.position - targetCamera.transform.position;
        lookDirection.y = 0; // Y축 무시 (수직 뒤집힘 방지)
        if (lookDirection != Vector3.zero)
        {
            targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - smoothSpeed);
        }
    }

    public void SetText(string text)
    {
        if (textComponent != null)
            textComponent.text = text;
    }
}
