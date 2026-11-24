using UnityEngine;

/// <summary>
/// 스테이지 씬을 직접 실행할 때 TutorialNarrationManager가 없으면 자동 생성합니다.
/// 각 스테이지 씬에 빈 오브젝트를 만들고 이 컴포넌트를 붙여두세요.
/// 튜토리얼 씬에서 정상 진행할 때는 이미 Instance가 있으므로 아무것도 하지 않습니다.
/// </summary>
public class NarrationBootstrapper : MonoBehaviour
{
    [Tooltip("TutorialNarrationManager가 설정된 프리팹. 비워두면 빈 오브젝트로 생성(오디오·자막 없음)")]
    public TutorialNarrationManager narrationManagerPrefab;

    void Awake()
    {
        if (TutorialNarrationManager.Instance != null) return;

        if (narrationManagerPrefab != null)
        {
            var go = Instantiate(narrationManagerPrefab).gameObject;

            // 튜토리얼 자동 재생 스크립트가 딸려왔으면 제거
            var sequencer = go.GetComponent<TutorialSequencer>();
            if (sequencer != null) { Destroy(sequencer); }
            var introSeq = go.GetComponent<TutorialIntroSequence>();
            if (introSeq != null) { Destroy(introSeq); }

            Debug.Log("[NarrationBootstrapper] TutorialNarrationManager 프리팹 생성");
        }
        else
        {
            var go = new GameObject("TutorialNarrationManager (Bootstrapped)");
            go.AddComponent<TutorialNarrationManager>();
            Debug.LogWarning("[NarrationBootstrapper] 프리팹 없이 빈 NarrationManager 생성 — 오디오·자막 미동작");
        }
    }
}
