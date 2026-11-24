using System.Collections;
using UnityEngine;

// 튜토리얼 씬 시작 시 나레이션 1~3을 순차 재생합니다.
// TutorialNarrationManager와 같은 GameObject에 두거나 narrationManager를 Inspector에서 연결하세요.
public class TutorialIntroSequence : MonoBehaviour
{
    [Tooltip("없으면 씬의 TutorialNarrationManager.Instance 자동 사용")]
    public TutorialNarrationManager narrationManager;

    [Tooltip("씬 로드 후 첫 나레이션 시작까지 대기 시간 (초)")]
    public float startDelay = 1.5f;

    void Start()
    {
        if (narrationManager == null)
            narrationManager = TutorialNarrationManager.Instance;

        if (narrationManager != null)
            StartCoroutine(PlayIntro());
    }

    IEnumerator PlayIntro()
    {
        yield return new WaitForSeconds(startDelay);

        // 나레이션 1: 환영 인사 + 도구 소개 + 쓰레기 손으로 줍기
        // 나레이션 2: 도구 선택 방법 (A버튼 + 트리거)
        // 나레이션 3: X버튼 / 컨트롤러 패드 안내
        narrationManager.PlaySequence(0, 1, 2);
    }
}
