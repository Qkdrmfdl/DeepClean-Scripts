using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// 양동이 오브젝트의 자식으로 추가되는 트리거 영역.
/// XRGrabInteractable로 잡은 트렁크 도구가 이 영역에 들어오면 양동이에 담김.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BucketDropZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        var marker = other.GetComponent<TrunkToolMarker>();
        if (marker == null)
            marker = other.GetComponentInParent<TrunkToolMarker>();
        if (marker == null || marker.controller == null) return;

        // 현재 잡고 있는 상태일 때만 담기 (의도치 않은 접촉 방지)
        var grab = other.GetComponent<XRGrabInteractable>();
        if (grab == null)
            grab = other.GetComponentInParent<XRGrabInteractable>();
        if (grab != null && !grab.isSelected) return;

        marker.controller.OnToolSelected(marker.toolTag);
    }
}
