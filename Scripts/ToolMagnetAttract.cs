using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// 컨트롤러가 도구 근처에 오면 자석처럼 끌려오는 효과.
/// XRGrabInteractable의 hover 이벤트를 활용.
/// TrunkInteractionController가 Init()을 호출해 초기화.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class ToolMagnetAttract : MonoBehaviour
{
    [Tooltip("끌어당기는 속도 (높을수록 빠르게 달라붙음)")]
    public float attractSpeed = 14f;

    private XRGrabInteractable grab;
    private Transform attractTarget;
    private bool isGrabbed;
    private Vector3 restorePos;
    private Quaternion restoreRot;

    public void Init()
    {
        grab = GetComponent<XRGrabInteractable>();
        restorePos = transform.position;
        restoreRot = transform.rotation;

        grab.hoverEntered.AddListener(OnHoverEntered);
        grab.hoverExited.AddListener(OnHoverExited);
        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);
    }

    private void Update()
    {
        if (attractTarget == null || isGrabbed) return;
        transform.position = Vector3.Lerp(transform.position, attractTarget.position, Time.deltaTime * attractSpeed);
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (isGrabbed) return;
        // 인터랙터의 attach point를 타겟으로 설정
        attractTarget = args.interactorObject.GetAttachTransform(grab);
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        if (isGrabbed) return;
        attractTarget = null;
        transform.position = restorePos;
        transform.rotation = restoreRot;
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        attractTarget = null;
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        isGrabbed = false;
        // 양동이 미투입 시 위치 복귀는 TrunkInteractionController의 selectExited 리스너가 담당
    }
}
