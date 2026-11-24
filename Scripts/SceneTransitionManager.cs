using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance;

    [Header("씬 이름")]
    public string trunkSceneName    = "TrunkScene";
    public string tutorialSceneName = "TutorialScene";
    public string nextSceneName     = "GameScene";

    public bool TutorialCompleted { get; private set; } = false;

    // 선택된 도구 태그 목록
    public List<string> SelectedToolTags { get; private set; } = new List<string>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void RegisterSelectedTool(string toolTag)
    {
        if (!SelectedToolTags.Contains(toolTag))
            SelectedToolTags.Add(toolTag);
    }

    public void OnReachedRoomEntrance()
    {
        if (!TutorialCompleted)
            StartCoroutine(TransitionToScene(tutorialSceneName));
        else
            StartCoroutine(TransitionToScene(nextSceneName));
    }

    public void ReturnToTrunk()
    {
        TutorialCompleted = true;
        SelectedToolTags.Clear();
        StartCoroutine(TransitionToScene(trunkSceneName));
    }

    IEnumerator TransitionToScene(string sceneName)
    {
        SceneManager.LoadSceneAsync(sceneName);
        yield break;
    }
}