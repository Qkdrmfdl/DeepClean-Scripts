using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class MainSceneTransitionButton : MonoBehaviour
{
    [Header("Scene Settings")]
    [SerializeField] private string targetSceneName = "MainScene";

    [Header("Audio Settings")]
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField][Range(0.1f, 5f)] private float volume = 1.5f;
    private AudioSource audioSource;

    private XRSimpleInteractable interactable;

    void Start()
    {
        // AudioSource 추가
        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;

        // XRSimpleInteractable 컴포넌트 가져오기/추가
        interactable = GetComponent<XRSimpleInteractable>();
        if (interactable == null)
        {
            interactable = gameObject.AddComponent<XRSimpleInteractable>();
        }

        // ★ Grip 트리거(Activate) 사용
        interactable.activated.AddListener(OnButtonPressed);

        Debug.Log("MainSceneTransitionButton 초기화 완료 (Grip Trigger 사용)");
    }

    void OnButtonPressed(ActivateEventArgs args)
    {
        Debug.Log($"★★★ Start 버튼이 눌렸습니다! {targetSceneName}으로 이동합니다. (Grip Trigger) ★★★");

        // 사운드 재생
        PlayButtonSound();

        // 씬 로드 (약간의 딜레이 추가)
        Invoke("LoadScene", 0.1f);
    }

    void PlayButtonSound()
    {
        if (buttonClickSound != null && audioSource != null)
        {
            if (volume > 1f)
            {
                audioSource.volume = Mathf.Min(volume, 5f);
                audioSource.PlayOneShot(buttonClickSound);
            }
            else
            {
                audioSource.PlayOneShot(buttonClickSound, volume);
            }
        }
        else if (buttonClickSound == null)
        {
            Debug.LogWarning("버튼 클릭 사운드가 할당되지 않았습니다!");
        }
    }

    void LoadScene()
    {
        Debug.Log($"씬 로딩 시작: {targetSceneName}");

        if (Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            SceneManager.LoadSceneAsync(targetSceneName);
        }
        else
        {
            Debug.LogError($"씬 '{targetSceneName}'을 찾을 수 없습니다! Build Settings에 추가했는지 확인하세요.");
        }
    }

    void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.activated.RemoveListener(OnButtonPressed);
        }
    }
}
