using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

// 튜토리얼 전체 흐름을 제어하는 마스터 시퀀서.
//
// ── 두 가지 실행 방식 ────────────────────────────────────────────────
//  1) phases 리스트 방식 (usePhaseList = true, 기본값)
//     인스펙터의 phases 배열을 위에서 아래로 순서대로 실행한다.
//     "설명 → 바로 그 도구 따라하기 → 다음 설명 → 따라하기" 처럼
//     설명과 따라하기를 자유롭게 교차 배치할 수 있고,
//     원소를 드래그로 재배치 / 추가 / 삭제하면 흐름이 바뀐다.
//     컴포넌트 우클릭 → "기본 흐름으로 phases 채우기" 로 템플릿 생성 가능.
//
//  2) 레거시 방식 (usePhaseList = false 또는 phases 비어 있음)
//     아래 RunSequenceLegacy() 의 하드코딩된 순서를 그대로 사용한다.
public class TutorialSequencer : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────
    //  phases 리스트 정의
    // ─────────────────────────────────────────────────────────────────

    public enum TutorialPhaseKind
    {
        PlayNarrations,        // 나레이션 여러 개 순서대로 재생
        PlayDemo,              // 시범(데모) 하나 재생
        WaitForPractice,       // 플레이어가 특정 도구로 청소 1회 할 때까지 대기
        WaitForCleaningButton  // 플레이어가 '청소완료' 버튼을 누를 때까지 대기
    }

    [System.Serializable]
    public class TutorialPhase
    {
        [Tooltip("인스펙터에서 알아보기 위한 이름 (동작에는 영향 없음)")]
        public string label = "";

        public TutorialPhaseKind kind = TutorialPhaseKind.PlayNarrations;

        // ── PlayNarrations ──
        [Tooltip("순서대로 재생할 나레이션 인덱스들 (TutorialNarrationManager.narrations 기준)")]
        public int[] narrationIndices;
        [Tooltip("마지막 나레이션은 오디오가 끝나는 즉시 다음으로 진행 (페이드아웃·여운을 기다리지 않음).\n헹구기 설명처럼 '설명 끝나자마자 시범 시작' 이 필요할 때 사용")]
        public bool lastNarrationAudioOnly = false;

        // ── PlayDemo ──
        [Tooltip("재생할 시범 종류")]
        public ToolDemoType demoType = ToolDemoType.Sponge;
        [Tooltip("시범 시작 전 대기 시간. -1이면 TutorialDemoController 기본값 사용")]
        public float demoOverrideDelay = -1f;
        [Tooltip("시범 시작과 동시에 재생할 나레이션 인덱스. -1이면 없음")]
        public int demoConcurrentNarration = -1;

        // ── WaitForPractice ──
        [Tooltip("따라하기 시작 직전에 재생할 나레이션 (예: '이제 스펀지로 직접 해보세요'). 비어 있으면 재생 안 함")]
        public int[] narrationsBeforePractice;
        [Tooltip("TutorialProgressTracker.steps 배열의 인덱스.\n그 단계의 도구/하이라이트 태그/힌트 나레이션 설정을 그대로 사용한다")]
        public int practiceStepIndex = 0;
        [Tooltip("따라하기 완료 후 순서대로 재생할 나레이션 (칭찬·다음 안내 등). 비어 있으면 재생 안 함")]
        public int[] narrationsAfterPractice;

        // ── 공통 ──
        [Tooltip("이 단계가 끝나면 플레이어(XR Origin)를 시작 위치로 되돌린다.\n따라하기 단계는 '칭찬 나레이션(Narrations After Practice)까지 끝난 뒤' 되돌린다.\n자막 패널이 고정 위치라, 플레이어가 이동하는 단계마다 체크할 것")]
        public bool respawnPlayerWhenDone = false;
        [Tooltip("이 단계 시작 시 도구/조작을 잠근다 (설명·시범 단계는 켜두는 것을 권장).\n따라하기 단계는 이 값과 무관하게 자동으로 잠금을 해제한다")]
        public bool lockToolsAtStart = true;
        [Tooltip("이 단계가 끝난 뒤 추가로 대기할 시간 (초)")]
        public float postDelay = 0f;
    }

    // ─────────────────────────────────────────────────────────────────
    //  참조
    // ─────────────────────────────────────────────────────────────────

    [Header("참조")]
    [Tooltip("없으면 TutorialNarrationManager.Instance 자동 사용")]
    public TutorialNarrationManager narrationManager;
    public TutorialDemoController demoController;
    [Tooltip("따라하기 진행 담당. 없으면 씬에서 자동 검색")]
    public TutorialProgressTracker tracker;

    [Header("따라하기 단계 진입 시")]
    [Tooltip("따라하기 단계에서 활성화할 Pad UI 오브젝트")]
    public GameObject padUI;
    [Tooltip("따라하기 단계에서 버튼 잠금 해제할 ToolSwitcher")]
    public ToolSwitcher toolSwitcher;
    [Tooltip("(레거시 호환) 청소 순서 가이드 래퍼. 없으면 자동 검색")]
    public TutorialCleaningGuide cleaningGuide;

    [Header("플레이어 복귀 위치")]
    [Tooltip("따라하기 후 플레이어(XR Origin)를 되돌릴 위치.\n비우면 씬 시작 시점의 플레이어(Tag=Player) 위치를 자동 저장해 사용")]
    public Transform playerStartPoint;
    [Tooltip("모든 따라하기/버튼 단계가 끝나면 항상 플레이어를 시작 위치로 되돌린다 (각 phase의 respawnPlayerWhenDone 체크 안 해도 됨)")]
    public bool respawnAfterEveryPractice = true;
    [Tooltip("따라하기가 아닌 단계(설명·시범)에서는 플레이어 자유 이동을 잠근다")]
    public bool lockMovementDuringExplanations = true;

    [Tooltip("씬 로드 후 첫 단계 시작까지 대기 시간 (초)")]
    public float startDelay = 1.5f;

    // ─────────────────────────────────────────────────────────────────
    //  phases 리스트
    // ─────────────────────────────────────────────────────────────────

    [Header("튜토리얼 흐름")]
    [Tooltip("체크: 아래 phases 리스트를 순서대로 실행 / 해제: 레거시 하드코딩 순서 사용")]
    public bool usePhaseList = true;

    [Tooltip("위에서 아래로 순서대로 실행. 우클릭 → '기본 흐름으로 phases 채우기' 로 템플릿 생성")]
    public TutorialPhase[] phases = new TutorialPhase[0];

    [Tooltip("phases 를 모두 실행한 뒤 Street 씬으로 전환")]
    public bool transitionToStreetWhenDone = true;

    // ─────────────────────────────────────────────────────────────────
    //  레거시(usePhaseList = false)에서만 사용되는 값들
    // ─────────────────────────────────────────────────────────────────

    [Header("레거시 전용 · 대기 시간")]
    public float demoPause = 1.5f;

    [Header("레거시 전용 · 나레이션 인덱스")]
    public int narIntro         = 0;
    public int narPad           = 1;
    public int narButtonExplain = 2;
    public int narToolSelect    = 3;
    public int narDemoIntro     = 4;
    public int narSpongeDemo    = 5;
    public int narBroomDemo     = 6;
    public int narMopDemo       = 7;
    public int[] narRinseSequence = new int[] { 8, 9, 10 };
    public int narMopRinseDemo  = 11;
    public int narPlayerPhase   = 12;

    // ─────────────────────────────────────────────────────────────────
    //  디버그
    // ─────────────────────────────────────────────────────────────────

    [Header("디버그")]
    [Tooltip("체크하면 설명·시범 전부 건너뛰고 즉시 따라하기 단계로 진입")]
    public bool skipToPlayerPhase = false;

    private bool _practiceEntered = false;

    private Transform _player;
    private Vector3 _playerStartPos;
    private Quaternion _playerStartRot;
    private bool _playerCaptured = false;

    // ─────────────────────────────────────────────────────────────────
    //  시작
    // ─────────────────────────────────────────────────────────────────

    void Start()
    {
        if (narrationManager == null)
            narrationManager = TutorialNarrationManager.Instance;

        if (cleaningGuide == null)
            cleaningGuide = FindFirstObjectByType<TutorialCleaningGuide>();

        if (tracker == null)
            tracker = FindFirstObjectByType<TutorialProgressTracker>();

        CachePlayer();

        // 씬 시작 즉시 로코모션 비활성화 (따라하기 단계 진입 전까지 이동 불가)
        demoController?.SetLocomotionEnabled(false);

        // 따라하기 진입 전까지 패드 버튼 전체 잠금 + 패드 자체도 X버튼으로 못 열게 차단
        toolSwitcher?.LockAllPadButtons();
        toolSwitcher?.padPanelToggle?.SetInputBlocked(true);

        // 인트로에서 선택한 도구만 메뉴에 표시
        if (toolSwitcher != null && StageFlowManager.Instance != null)
            toolSwitcher.ShowOnlyTools(StageFlowManager.Instance.SelectedToolTags);

        if (skipToPlayerPhase)
        {
            StartCoroutine(JumpToPlayerPhase());
            return;
        }

        bool hasPhases = usePhaseList && phases != null && phases.Length > 0;
        if (hasPhases)
        {
            tracker?.SetExternalMode(true); // 종료 안내·씬전환은 시퀀서가 담당
            StartCoroutine(RunPhases());
        }
        else
        {
            if (usePhaseList)
                Debug.LogWarning("[TutorialSequencer] phases 리스트가 비어 있어 레거시 순서로 진행합니다. " +
                                 "컴포넌트 우클릭 → '기본 흐름으로 phases 채우기' 로 템플릿을 만들 수 있습니다.");
            StartCoroutine(RunSequenceLegacy());
        }
    }

    void CachePlayer()
    {
        var go = GameObject.FindWithTag("Player");
        if (go == null) return;
        _player = go.transform;
        _playerStartPos = _player.position;
        _playerStartRot = _player.rotation;
        _playerCaptured = true;
    }

    // ─────────────────────────────────────────────────────────────────
    //  phases 리스트 실행
    // ─────────────────────────────────────────────────────────────────

    IEnumerator RunPhases()
    {
        yield return new WaitForSeconds(startDelay);

        for (int i = 0; i < phases.Length; i++)
        {
            TutorialPhase p = phases[i];
            if (p == null) continue;

            bool practicePhase = p.kind == TutorialPhaseKind.WaitForPractice
                              || p.kind == TutorialPhaseKind.WaitForCleaningButton;

            if (practicePhase)
            {
                // 따라하기 단계 : 도구 잠금 해제
                EnterPracticePhaseOnce();
                UnlockToolsForPractice();

                if (p.kind == TutorialPhaseKind.WaitForCleaningButton)
                {
                    demoController?.SetLocomotionEnabled(true);
                    toolSwitcher?.RestrictToolSelection(null); // 도구 제한 해제
                }
                else // WaitForPractice
                {
                    // 이동은 안내 나레이션이 끝난 뒤 WaitForPractice case에서 허용.
                    demoController?.SetLocomotionEnabled(false);
                    // 지금 청소할 차례의 도구만 선택 가능 (나머지는 회색·비활성)
                    if (tracker != null)
                        toolSwitcher?.RestrictToolSelection(tracker.GetStepToolTag(p.practiceStepIndex));
                }
            }
            else
            {
                // 설명·시범 단계 : 이동·도구 잠금
                if (p.lockToolsAtStart)
                    LockToolsForExplanation();
                if (lockMovementDuringExplanations)
                    demoController?.SetLocomotionEnabled(false);
            }

            bool respawnWhenDone = p.respawnPlayerWhenDone
                                || (practicePhase && respawnAfterEveryPractice);

            switch (p.kind)
            {
                case TutorialPhaseKind.PlayNarrations:
                    yield return PlayNarrationList(p.narrationIndices, p.lastNarrationAudioOnly);
                    if (respawnWhenDone) yield return RespawnPlayerRoutine();
                    break;

                case TutorialPhaseKind.PlayDemo:
                    if (p.demoConcurrentNarration >= 0 && narrationManager != null)
                        narrationManager.Play(p.demoConcurrentNarration);
                    if (demoController != null)
                        yield return demoController.PlayDemo(p.demoType, p.demoOverrideDelay);
                    if (respawnWhenDone) yield return RespawnPlayerRoutine();
                    break;

                case TutorialPhaseKind.WaitForPractice:
                    // 안내 나레이션("이제 직접 해보세요")이 완전히 끝난 뒤 이동 허용
                    yield return PlayNarrationList(p.narrationsBeforePractice, false);
                    demoController?.SetLocomotionEnabled(true);

                    if (tracker != null)
                    {
                        tracker.BeginStep(p.practiceStepIndex);
                        yield return new WaitUntil(() => tracker.ExternalStepComplete);
                    }
                    else
                    {
                        Debug.LogWarning("[TutorialSequencer] TutorialProgressTracker가 없어 WaitForPractice 단계를 건너뜁니다.");
                    }

                    toolSwitcher?.RestrictToolSelection(null); // 청소 끝 — 도구 제한 해제

                    // 칭찬 나레이션이 있으면 먼저 끝까지 재생한 뒤 시작 위치로 복귀
                    yield return PlayNarrationList(p.narrationsAfterPractice, false);

                    // 칭찬 나레이션이 끝난 직후부터 다음 따라하기 도구의 차례로 인식시킨다.
                    // (다음 단계는 보통 시범(PlayDemo) 단계를 거친 뒤에야 정식 BeginStep이 불리는데,
                    //  그 사이에 플레이어가 먼저 다음 도구로 청소해버려도 "차례가 아님" 힌트가 뜨지 않게 하기 위함)
                    if (tracker != null)
                    {
                        int nextPracticeStepIndex = FindNextPracticeStepIndex(i);
                        if (nextPracticeStepIndex >= 0)
                            tracker.PreAdvanceStep(nextPracticeStepIndex);
                    }

                    if (respawnWhenDone) yield return RespawnPlayerRoutine();
                    break;

                case TutorialPhaseKind.WaitForCleaningButton:
                    toolSwitcher?.UnlockCleaningCompleteButton(); // 안전용 (이미 열려 있어도 무해)

                    if (tracker != null)
                        yield return new WaitUntil(() => tracker.CleaningButtonPressed);

                    if (respawnWhenDone) yield return RespawnPlayerRoutine();
                    break;
            }

            if (p.postDelay > 0f)
                yield return new WaitForSeconds(p.postDelay);
        }

        // 마지막 단계까지 끝났으면 이동 잠금 해제 후 씬 전환
        demoController?.SetLocomotionEnabled(true);

        if (usePhaseList && transitionToStreetWhenDone)
        {
            if (StageFlowManager.Instance != null)
                StageFlowManager.Instance.GoToStreet();
            else
                SceneManager.LoadSceneAsync("Street");
        }
    }

    // phases[fromIndex] 다음에 나오는 가장 가까운 WaitForPractice 단계의 practiceStepIndex를 찾는다.
    // 없으면 -1 (예: 마지막 따라하기 단계이거나 다음은 청소완료 버튼 단계인 경우).
    int FindNextPracticeStepIndex(int fromIndex)
    {
        if (phases == null) return -1;
        for (int j = fromIndex + 1; j < phases.Length; j++)
        {
            if (phases[j] != null && phases[j].kind == TutorialPhaseKind.WaitForPractice)
                return phases[j].practiceStepIndex;
        }
        return -1;
    }

    // 나레이션 배열을 순서대로 재생. lastAudioOnly면 마지막 항목은 오디오가 끝나는 즉시 반환.
    IEnumerator PlayNarrationList(int[] indices, bool lastAudioOnly)
    {
        if (indices == null || narrationManager == null) yield break;

        for (int k = 0; k < indices.Length; k++)
        {
            int idx = indices[k];
            if (idx < 0) continue;

            narrationManager.Play(idx);

            bool last = k == indices.Length - 1;
            if (last && lastAudioOnly)
                yield return new WaitForSeconds(narrationManager.GetAudioEndTime(idx));
            else
                yield return narrationManager.WaitUntilDone();
        }
    }

    // 플레이어(XR Origin)를 시작 위치로 되돌린다. (PlayerSeater와 동일한 방식)
    IEnumerator RespawnPlayerRoutine()
    {
        if (_player == null)
        {
            CachePlayer();
            if (_player == null) yield break;
        }

        if (!_playerCaptured && playerStartPoint == null)
        {
            Debug.LogWarning("[TutorialSequencer] 플레이어 복귀 실패 — playerStartPoint를 지정하거나 XR Origin에 Tag=Player를 설정하세요.");
            yield break;
        }

        Vector3    pos = playerStartPoint != null ? playerStartPoint.position : _playerStartPos;
        Quaternion rot = playerStartPoint != null ? playerStartPoint.rotation : _playerStartRot;

        // XRBodyTransformer가 이동을 되돌리지 못하도록 한 프레임 끈다
        var bt = _player.GetComponent<XRBodyTransformer>()
                 ?? _player.GetComponentInChildren<XRBodyTransformer>();
        bool wasEnabled = bt != null && bt.enabled;
        if (bt != null) bt.enabled = false;

        _player.position = pos;
        _player.rotation = rot;

        yield return null;

        if (bt != null) bt.enabled = wasEnabled;
    }

    void LockToolsForExplanation()
    {
        if (toolSwitcher == null) return;
        toolSwitcher.demoMode = true;
        toolSwitcher.CloseAllUI();
        toolSwitcher.LockAllPadButtons();
        toolSwitcher.padPanelToggle?.SetInputBlocked(true); // 설명·시범 중엔 X버튼으로도 패드를 못 엶
    }

    // Pad UI 표시 — 최초 따라하기 진입 시 1회만 (이동 잠금/해제는 phase별로 별도 처리)
    void EnterPracticePhaseOnce()
    {
        if (_practiceEntered) return;
        _practiceEntered = true;

        if (padUI != null)
            padUI.SetActive(true);
    }

    // 도구 조작 잠금 해제 — 따라하기 단계마다 호출 (idempotent)
    void UnlockToolsForPractice()
    {
        if (toolSwitcher == null) return;
        toolSwitcher.demoMode = false;
        toolSwitcher.padPanelToggle?.SetInputBlocked(false); // 따라하기 단계에서만 X버튼으로 패드 열기 허용
        toolSwitcher.UnlockButtons();
        toolSwitcher.ForceShowRay();
        toolSwitcher.UnlockPadDescriptionOnly(); // 조작법/설명 버튼만 활성화
    }

    // ─────────────────────────────────────────────────────────────────
    //  디버그: 즉시 따라하기 단계로 점프
    // ─────────────────────────────────────────────────────────────────

    IEnumerator JumpToPlayerPhase()
    {
        yield return null;

        if (toolSwitcher != null) toolSwitcher.demoMode = false;
        demoController?.SetLocomotionEnabled(true);

        if (narrationManager != null)
        {
            narrationManager.Play(narPlayerPhase);
            yield return narrationManager.WaitUntilDone();
        }

        if (padUI != null)
            padUI.SetActive(true);

        UnlockToolsForPractice();
        cleaningGuide?.StartGuide();
    }

    // ─────────────────────────────────────────────────────────────────
    //  레거시 하드코딩 순서 (usePhaseList = false)
    // ─────────────────────────────────────────────────────────────────

    IEnumerator RunSequenceLegacy()
    {
        yield return new WaitForSeconds(startDelay);

        narrationManager.Play(narIntro);
        yield return narrationManager.WaitUntilDone();

        yield return demoController.PlayDemo(ToolDemoType.Hand);
        yield return new WaitForSeconds(demoPause);

        narrationManager.Play(narPad);
        yield return narrationManager.WaitUntilDone();
        narrationManager.Play(narButtonExplain);
        yield return narrationManager.WaitUntilDone();
        narrationManager.Play(narToolSelect);
        yield return narrationManager.WaitUntilDone();
        narrationManager.Play(narDemoIntro);
        yield return narrationManager.WaitUntilDone();

        yield return demoController.PlayDemo(ToolDemoType.Sponge);
        yield return demoController.PlayDemo(ToolDemoType.Broom);
        yield return demoController.PlayDemo(ToolDemoType.Mop);

        for (int i = 0; i < narRinseSequence.Length - 1; i++)
        {
            narrationManager.Play(narRinseSequence[i]);
            yield return narrationManager.WaitUntilDone();
        }

        int lastRinse = narRinseSequence[narRinseSequence.Length - 1];
        float lastRinseEnd = narrationManager.GetAudioEndTime(lastRinse);
        narrationManager.Play(lastRinse);
        yield return new WaitForSeconds(lastRinseEnd);

        narrationManager.Play(narMopRinseDemo);
        yield return demoController.PlayDemo(ToolDemoType.MopRinse, 0f);
        yield return narrationManager.WaitUntilDone();

        narrationManager.Play(narPlayerPhase);
        yield return narrationManager.WaitUntilDone();

        demoController?.SetLocomotionEnabled(true);
        if (padUI != null) padUI.SetActive(true);
        UnlockToolsForPractice();

        cleaningGuide?.StartGuide();
    }

    // ─────────────────────────────────────────────────────────────────
    //  에디터: 현재 나레이션 인덱스에 맞춘 템플릿 생성
    //  (나레이션 5/6/7 은 TutorialDemoController가 시범 재생 시 자동으로 재생함)
    // ─────────────────────────────────────────────────────────────────

    [ContextMenu("기본 흐름으로 phases 채우기")]
    void FillDefaultPhases()
    {
        var list = new List<TutorialPhase>
        {
            new TutorialPhase { label = "인사", kind = TutorialPhaseKind.PlayNarrations,
                narrationIndices = new[] { 0 } },
            new TutorialPhase { label = "쓰레기 줍기 시범", kind = TutorialPhaseKind.PlayDemo,
                demoType = ToolDemoType.Hand, postDelay = demoPause },
            new TutorialPhase { label = "조작 설명", kind = TutorialPhaseKind.PlayNarrations,
                narrationIndices = new[] { 1, 2, 3, 4 } },

            new TutorialPhase { label = "스펀지 시범", kind = TutorialPhaseKind.PlayDemo,
                demoType = ToolDemoType.Sponge },
            new TutorialPhase { label = "스펀지 따라하기", kind = TutorialPhaseKind.WaitForPractice,
                practiceStepIndex = 0, narrationsBeforePractice = new[] { 15 }, respawnPlayerWhenDone = true },

            new TutorialPhase { label = "빗자루 시범", kind = TutorialPhaseKind.PlayDemo,
                demoType = ToolDemoType.Broom },
            new TutorialPhase { label = "빗자루 따라하기", kind = TutorialPhaseKind.WaitForPractice,
                practiceStepIndex = 1, narrationsBeforePractice = new[] { 16 }, respawnPlayerWhenDone = true },

            new TutorialPhase { label = "대걸레 시범", kind = TutorialPhaseKind.PlayDemo,
                demoType = ToolDemoType.Mop },
            new TutorialPhase { label = "대걸레 따라하기", kind = TutorialPhaseKind.WaitForPractice,
                practiceStepIndex = 2, narrationsBeforePractice = new[] { 17 }, respawnPlayerWhenDone = true },

            new TutorialPhase { label = "헹구기 설명", kind = TutorialPhaseKind.PlayNarrations,
                narrationIndices = new[] { 8, 9, 10 }, lastNarrationAudioOnly = true },
            new TutorialPhase { label = "헹구기 시범", kind = TutorialPhaseKind.PlayDemo,
                demoType = ToolDemoType.MopRinse, demoOverrideDelay = 0f, demoConcurrentNarration = 11 },

            new TutorialPhase { label = "청소도구 설명 끝", kind = TutorialPhaseKind.PlayNarrations,
                narrationIndices = new[] { 12 } },
            new TutorialPhase { label = "하이라이트 시범", kind = TutorialPhaseKind.PlayNarrations,
                narrationIndices = new[] { 13 } },

            new TutorialPhase { label = "청소완료 버튼 안내", kind = TutorialPhaseKind.PlayNarrations,
                narrationIndices = new[] { 18 } },
            new TutorialPhase { label = "청소완료 버튼 따라하기", kind = TutorialPhaseKind.WaitForCleaningButton,
                respawnPlayerWhenDone = true },
            new TutorialPhase { label = "마무리·출발 멘트", kind = TutorialPhaseKind.PlayNarrations,
                narrationIndices = new[] { 19, 20 } },
        };
        phases = list.ToArray();
        usePhaseList = true;

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
        Debug.Log($"[TutorialSequencer] phases {phases.Length}개 생성 완료. " +
                  "나레이션 5/6/7(스펀지·빗자루·대걸레 시범)은 TutorialDemoController가 자동 재생합니다.");
    }
}
