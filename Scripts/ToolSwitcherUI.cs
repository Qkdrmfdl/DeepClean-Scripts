using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ToolSwitcherUI : MonoBehaviour
{
    [Header("Slots")]
    public Image spongeSlot;
    public Image broomSlot;
    public Image mopSlot;

    [Header("Icons")]
    public Sprite spongeIcon;
    public Sprite broomIcon;
    public Sprite mopIcon;

    [Header("Colors")]
    public Color emptyColor  = new Color(1, 1, 1, 0.2f);
    public Color filledColor = new Color(1, 1, 1, 1.0f);

    void Start()
    {
        SetSlotEmpty(spongeSlot);
        SetSlotEmpty(broomSlot);
        SetSlotEmpty(mopSlot);
    }

    public void AddTool(ToolPickup.ToolType toolType)
    {
        switch (toolType)
        {
            case ToolPickup.ToolType.Sponge: FillSlot(spongeSlot, spongeIcon); break;
            case ToolPickup.ToolType.Broom:  FillSlot(broomSlot,  broomIcon);  break;
            case ToolPickup.ToolType.Mop:    FillSlot(mopSlot,    mopIcon);    break;
        }
    }

    void FillSlot(Image slot, Sprite icon)
    {
        slot.sprite = icon;
        slot.color  = filledColor;
        StartCoroutine(SlotFillAnimation(slot));
    }

    void SetSlotEmpty(Image slot)
    {
        slot.color = emptyColor;
    }

    IEnumerator SlotFillAnimation(Image slot)
    {
        Vector3 original = slot.transform.localScale;
        slot.transform.localScale = original * 1.3f;
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            slot.transform.localScale = Vector3.Lerp(original * 1.3f, original, t / 0.2f);
            yield return null;
        }
        slot.transform.localScale = original;
    }
}