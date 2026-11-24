using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class XRUIDebugger : MonoBehaviour
{
    void Start()
    {
        CheckEventSystem();
        CheckCanvas();
        CheckXRRayInteractor();
    }

    void CheckEventSystem()
    {
        var es = FindFirstObjectByType<EventSystem>();
        if (es == null)
        {
            Debug.LogError("[UI Debug] ❌ EventSystem 없음!");
            return;
        }
        Debug.Log("[UI Debug] ✅ EventSystem 있음: " + es.gameObject.name);

        var xrInput = es.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
        if (xrInput == null)
            Debug.LogError("[UI Debug] ❌ XRUIInputModule 없음! EventSystem에 추가 필요");
        else
            Debug.Log("[UI Debug] ✅ XRUIInputModule 있음");

        var standalone = es.GetComponent<StandaloneInputModule>();
        if (standalone != null && standalone.enabled)
            Debug.LogWarning("[UI Debug] ⚠️ StandaloneInputModule 활성화됨 → 비활성화 필요");
    }

    void CheckCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        Debug.Log($"[UI Debug] 씬 내 Canvas 총 {canvases.Length}개");

        foreach (var canvas in canvases)
        {
            string name = canvas.gameObject.name;

            if (canvas.renderMode != RenderMode.WorldSpace)
            {
                Debug.LogWarning($"[UI Debug] ⚠️ [{name}] Render Mode가 World Space 아님: {canvas.renderMode}");
                continue;
            }

            Debug.Log($"[UI Debug] ✅ [{name}] World Space Canvas");

            var trackedRaycaster = canvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
            if (trackedRaycaster == null)
                Debug.LogError($"[UI Debug] ❌ [{name}] TrackedDeviceGraphicRaycaster 없음!");
            else
                Debug.Log($"[UI Debug] ✅ [{name}] TrackedDeviceGraphicRaycaster 있음");

            var graphicRaycaster = canvas.GetComponent<GraphicRaycaster>();
            if (graphicRaycaster != null)
                Debug.LogWarning($"[UI Debug] ⚠️ [{name}] GraphicRaycaster 있음 → 제거 권장");

            // 버튼 확인
            Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
            Debug.Log($"[UI Debug] [{name}] 버튼 {buttons.Length}개");
            foreach (var btn in buttons)
            {
                Debug.Log($"[UI Debug]   버튼: {btn.gameObject.name} | active: {btn.gameObject.activeInHierarchy} | interactable: {btn.interactable} | OnClick 연결 수: {btn.onClick.GetPersistentEventCount()}");
            }
        }
    }

    void CheckXRRayInteractor()
{
    var rays = FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(
        FindObjectsInactive.Include,  // 비활성화 포함
        FindObjectsSortMode.None
    );

    if (rays.Length == 0)
    {
        Debug.LogError("[UI Debug] ❌ XRRayInteractor 없음!");
        return;
    }

    foreach (var ray in rays)
    {
        Debug.Log($"[UI Debug] XRRayInteractor 발견: {ray.gameObject.name} | 오브젝트 활성화: {ray.gameObject.activeInHierarchy} | 컴포넌트 활성화: {ray.enabled}");
    }
}

    void Update()
    {
        // 매 프레임 Ray 방향 확인 (5초마다)
        if (Time.frameCount % 300 == 0)
        {
            var rays = FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(FindObjectsSortMode.None);
            foreach (var ray in rays)
            {
                Debug.Log($"[UI Debug] Ray [{ray.gameObject.name}] 위치: {ray.transform.position} | 방향: {ray.transform.forward}");
            }
        }
    }
}