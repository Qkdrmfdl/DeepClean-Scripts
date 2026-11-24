using UnityEngine;
using UnityEngine.SceneManagement;

public class CarEntryTrigger : MonoBehaviour
{
    [Header("UI")]
    public GameObject enterCarButtonUI; // WorldSpace Canvas (버튼 포함)

    [Header("Scene")]
    public string nextSceneName = "Stage2_W"; // 전환할 씬 이름

    void Start()
    {
        enterCarButtonUI.SetActive(false);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            enterCarButtonUI.SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            enterCarButtonUI.SetActive(false);
    }

    // "차 타기" 버튼 onClick에 연결
    public void OnEnterCarButtonPressed()
    {
        SceneManager.LoadSceneAsync(nextSceneName);
    }
}
