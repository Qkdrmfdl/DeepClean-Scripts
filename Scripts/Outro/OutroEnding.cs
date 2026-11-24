using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 아웃트로 최종 연출: 랭크가 뜨고 몇 초 뒤 화면을 검게 페이드아웃하고 게임을 종료한다.
///
/// 연결 방법:
///   TotalResultSequencer.onSequenceComplete → 이 컴포넌트의 BeginEnding()
/// </summary>
public class OutroEnding : MonoBehaviour
{
    [Header("타이밍")]
    [Tooltip("랭크(결과)가 다 뜬 뒤 페이드아웃을 시작하기까지 대기 시간 (초)")]
    public float delayBeforeFade = 3f;

    [Tooltip("검은 화면으로 페이드아웃되는 시간 (초)")]
    public float fadeDuration = 2.5f;

    [Tooltip("페이드아웃 완료 후 게임 종료(Application.Quit)까지 추가 대기 시간 (초)")]
    public float holdBlackBeforeQuit = 0.5f;

    [Header("화면")]
    [Tooltip("카메라로부터 페이드 캔버스까지의 거리 (m)")]
    public float canvasDistance = 0.3f;

    private Image fadeImage;
    private Transform vrCamera;
    private bool started;

    /// <summary>TotalResultSequencer.onSequenceComplete 에서 호출</summary>
    public void BeginEnding()
    {
        if (started) return;
        started = true;
        StartCoroutine(EndingRoutine());
    }

    IEnumerator EndingRoutine()
    {
        if (delayBeforeFade > 0f)
            yield return new WaitForSeconds(delayBeforeFade);

        CreateFadeCanvas();

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetAlpha(Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }
        SetAlpha(1f);

        if (holdBlackBeforeQuit > 0f)
            yield return new WaitForSeconds(holdBlackBeforeQuit);

        QuitGame();
    }

    void CreateFadeCanvas()
    {
        vrCamera = Camera.main != null ? Camera.main.transform : null;

        GameObject canvasObj = new GameObject("OutroFadeCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 999;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        RectTransform rt = canvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(4f, 4f);

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);

        fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);

        RectTransform irt = fadeImage.rectTransform;
        irt.anchorMin = Vector2.zero;
        irt.anchorMax = Vector2.one;
        irt.offsetMin = Vector2.zero;
        irt.offsetMax = Vector2.zero;

        SnapToCamera(canvasObj.transform);
    }

    void LateUpdate()
    {
        if (fadeImage != null && fadeImage.color.a < 1f)
            SnapToCamera(fadeImage.transform.parent);
    }

    void SnapToCamera(Transform canvasTransform)
    {
        if (vrCamera == null || canvasTransform == null) return;
        canvasTransform.position = vrCamera.position + vrCamera.forward * canvasDistance;
        canvasTransform.rotation = vrCamera.rotation;
    }

    void SetAlpha(float a)
    {
        if (fadeImage == null) return;
        Color c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;
    }

    void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
