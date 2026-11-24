using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class ToolPickup : MonoBehaviour
{
    [Header("Tool Info")]
    public ToolPickup.ToolType toolType;

    [Header("References")]
    public ToolSwitcherUI toolSwitcherUI;
    public TutorialProgressTracker progressTracker;

    public enum ToolType { Sponge, Broom, Mop }

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable;

    void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        this.enabled = false;
    }

    void OnEnable()
    {
        if (interactable != null)
            interactable.selectEntered.AddListener(OnToolSelected);
    }

    void OnDisable()
    {
        if (interactable != null)
            interactable.selectEntered.RemoveListener(OnToolSelected);
    }

    void OnToolSelected(SelectEnterEventArgs args)
    {
        GetComponent<ToolHighlighter>()?.StopHighlight();

        toolSwitcherUI.AddTool(toolType);
        progressTracker.OnToolPicked(toolType);

        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.RegisterSelectedTool(toolType.ToString());

        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        // 양동이 슬롯으로 순간이동 + 덜그럭 소리 (BucketAttacher에 위임)
        if (BucketAttacher.Instance != null)
            BucketAttacher.Instance.PlaceToolInBucket(gameObject, toolType.ToString());
        else
            gameObject.SetActive(false);

        Debug.Log($"[ToolPickup] {toolType} 선택됨 → DontDestroyOnLoad 적용");
    }
}
