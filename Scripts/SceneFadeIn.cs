using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using System.Collections;
using System.Collections.Generic;

public class SceneFadeIn : MonoBehaviour
{
    [Tooltip("트래킹 안정화 대기 시간 (초) - 이 시간만큼 검은 화면 유지")]
    public float holdDuration = 5f;

    [Tooltip("검은 화면에서 페이드인되는 시간 (초)")]
    public float fadeDuration = 1f;

    [Tooltip("카메라로부터 캔버스까지의 거리 (m)")]
    public float canvasDistance = 0.5f;

    [Tooltip("씬 시작 후 자동 리센터 대기 시간 (초). 0이면 리센터 안 함.")]
    public float recenterDelay = 1f;

    private Image fadeImage;
    private Transform vrCamera;

    void Awake()
    {
        vrCamera = Camera.main != null ? Camera.main.transform : null;

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 999;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        RectTransform rt = GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(4f, 4f);

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(transform, false);

        fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = Color.black;

        RectTransform irt = fadeImage.rectTransform;
        irt.anchorMin = Vector2.zero;
        irt.anchorMax = Vector2.one;
        irt.offsetMin = Vector2.zero;
        irt.offsetMax = Vector2.zero;

        SnapToCamera();
        StartCoroutine(HoldThenFadeIn());
    }

    void SnapToCamera()
    {
        if (vrCamera == null) return;
        transform.position = vrCamera.position + vrCamera.forward * canvasDistance;
        transform.rotation = vrCamera.rotation;
    }

    void LateUpdate()
    {
        if (fadeImage != null && fadeImage.color.a > 0f)
            SnapToCamera();
    }

    IEnumerator HoldThenFadeIn()
    {
        // 잠깐 기다렸다가 자동 리센터 (Meta 버튼 효과)
        if (recenterDelay > 0f)
        {
            yield return new WaitForSeconds(recenterDelay);
            TryRecenter();
        }

        // 나머지 대기 시간
        float remaining = holdDuration - recenterDelay;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        // 페이드인
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fadeImage.color = new Color(0f, 0f, 0f, 1f - (elapsed / fadeDuration));
            yield return null;
        }

        Destroy(gameObject);
    }

    void TryRecenter()
    {
        var subsystems = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        foreach (var subsystem in subsystems)
            subsystem.TryRecenter();
    }
}
