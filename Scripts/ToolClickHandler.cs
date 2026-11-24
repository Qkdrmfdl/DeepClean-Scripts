using UnityEngine;

public class ToolClickHandler : MonoBehaviour
{
    private TrunkManager trunkManager;
    private GameObject toolObject;

    public void Initialize(TrunkManager manager, GameObject tool)
    {
        trunkManager = manager;
        toolObject = tool;

        // 콜라이더 확인
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
            if (trunkManager != null)
                Debug.Log($"BoxCollider 자동 추가: {tool.name}");
        }
    }

    void OnMouseDown()
    {
        if (trunkManager != null)
            trunkManager.OnToolSelected(toolObject);
    }
}