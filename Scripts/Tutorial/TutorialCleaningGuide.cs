using UnityEngine;

// TutorialProgressTracker로 통합 후 남긴 씬 참조 보호용 래퍼.
// 기능은 모두 TutorialProgressTracker에 있으므로 이 컴포넌트는 건드리지 마세요.
public class TutorialCleaningGuide : MonoBehaviour
{
    private TutorialProgressTracker _tracker;

    void Awake()
    {
        _tracker = FindFirstObjectByType<TutorialProgressTracker>();
    }

    public void StartGuide()                               => _tracker?.StartGuide();
    public void BeginStep(int stepIndex)                   => _tracker?.BeginStep(stepIndex);
    public bool ExternalStepComplete                       => _tracker?.ExternalStepComplete ?? true;
    public bool IsWrongTool(ToolPickup.ToolType toolType)  => _tracker?.IsWrongTool(toolType) ?? false;
    public ToolPickup.ToolType GetRequiredTool()           => _tracker?.GetRequiredTool() ?? ToolPickup.ToolType.Sponge;
    public void PlayCurrentStepHint()                      => _tracker?.PlayCurrentStepHint();
    public void NotifyToolUsed(ToolPickup.ToolType t)      { } // TutorialProgressTracker.OnToolUsed가 내부 처리
}
