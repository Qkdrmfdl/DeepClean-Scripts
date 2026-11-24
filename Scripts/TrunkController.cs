using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TrunkController : MonoBehaviour
{
    [Header("Trunk Lid")]
    public Transform trunkLid;
    public Vector3 closedRotation = new Vector3(0, 0, 0);
    public Vector3 openRotation   = new Vector3(-110, 0, 0);
    public float openSpeed = 2.0f;

    [Header("Button")]
    public Button trunkOpenButton;

    [Header("Cleaning Tools")]
    public GameObject[] cleaningToolObjects; // Sponge, Broom, Mop

    private bool isOpen = false;

    void Start()
    {
        trunkOpenButton.onClick.AddListener(OpenTrunk);
        SetToolsHighlight(false);
    }

    public void OpenTrunk()
    {
        if (isOpen) return;
        isOpen = true;
        trunkOpenButton.gameObject.SetActive(false);
        StartCoroutine(RotateTrunkLid());
    }

    IEnumerator RotateTrunkLid()
    {
        Quaternion from = Quaternion.Euler(closedRotation);
        Quaternion to   = Quaternion.Euler(openRotation);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * openSpeed;
            trunkLid.localRotation = Quaternion.Lerp(from, to, t);
            yield return null;
        }

        trunkLid.localRotation = to;
        ActivateToolHighlights();
    }

    void ActivateToolHighlights()
    {
        SetToolsHighlight(true);
        foreach (var tool in cleaningToolObjects)
        {
            var pickup = tool.GetComponent<ToolPickup>();
            if (pickup != null) pickup.enabled = true;
        }
    }

    void SetToolsHighlight(bool active)
    {
        foreach (var tool in cleaningToolObjects)
        {
            var highlighter = tool.GetComponent<ToolHighlighter>();
            if (highlighter == null) continue;
            if (active) highlighter.StartHighlight();
            else        highlighter.StopHighlight();
        }
    }
}