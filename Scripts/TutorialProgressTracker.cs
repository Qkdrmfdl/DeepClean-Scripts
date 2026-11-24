using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// 튜토리얼 청소 진행 전담 스크립트.
// 도구 사용 추적, 청소 순서 하이라이트, 상황별 나레이션을 한 곳에서 관리.
// (TutorialCleaningGuide 기능 통합 — TutorialCleaningGuide는 씬 참조 유지용 래퍼로만 남음)
public class TutorialProgressTracker : MonoBehaviour
{
    [System.Serializable]
    public class CleaningStep
    {
        [Tooltip("이 단계를 완료하는 도구 타입")]
        public ToolPickup.ToolType toolType = ToolPickup.ToolType.Sponge;
        [Tooltip("하이라이트할 청소 대상 태그 (예: Cleanable_Sponge). 비워두면 하이라이트 없음")]
        public string cleanableTag = "";
        [Tooltip("이 단계 차례가 아닌 행동 시 재생할 나레이션. -1이면 재생 안 함")]
        public int stepHintNarrationIndex = -1;
        [Tooltip("이 단계에서 하이라이트할 도구 오브젝트의 ToolHighlighter. 없으면 도구 하이라이트 없음")]
        public ToolHighlighter toolHighlighter;
    }

    // ── Inspector ──────────────────────────────────────────────────────

    [Header("필요한 도구")]
    public bool needSponge = true;
    public bool needBroom  = true;
    public bool needMop    = true;

    [Header("청소 단계 (순서 + 하이라이트)")]
    [Tooltip("StartGuide() 호출 후 이 순서대로 하이라이트가 진행됨")]
    public CleaningStep[] steps = new CleaningStep[]
    {
        new CleaningStep { toolType = ToolPickup.ToolType.Sponge, cleanableTag = "Cleanable_Sponge" },
        new CleaningStep { toolType = ToolPickup.ToolType.Broom,  cleanableTag = "Cleanable_Broom"  },
        new CleaningStep { toolType = ToolPickup.ToolType.Mop,    cleanableTag = "Cleanable_Mop"    }
    };

    public enum HighlightStyle
    {
        Opacity,    // 데칼·오브젝트 모두 '투명도'만 깜빡임 (느낌 일치, 이질감 적음)
        ColorTint   // _BaseColor/_Color를 원색↔강조색으로 직접 틴트 (빌드에서도 안정적으로 보임)
    }

    [Header("하이라이트 설정")]
    [Tooltip("Opacity: 데칼·오브젝트 모두 투명도만 깜빡임 — 둘의 느낌이 일치\nColorTint: _BaseColor/_Color를 강조색으로 직접 바꿔 깜빡임 — 실제 눈에 띄는 색 하이라이트가 필요할 때 사용.\n(예전 EmissionColor 모드는 _EMISSION 키워드가 빌드에서 스트립되어 안 보일 수 있어 이 방식으로 대체함)")]
    public HighlightStyle highlightStyle = HighlightStyle.Opacity;
    public float blinkSpeed = 2f;
    [Tooltip("깜빡임 최솟값. 데칼 fadeFactor·오브젝트 알파 공용.\n0 = 대상이 완전히 사라짐. 사라지는 게 싫으면 0.3~0.6")]
    public float decalFadeMin = 0.0f;
    [Tooltip("깜빡임 최댓값 (1 = 원래 상태)")]
    public float decalFadeMax = 1.0f;

    [Header("하이라이트 · ColorTint 모드 전용")]
    [Tooltip("깜빡일 때 틴트할 강조색 (원색과 이 색 사이를 오감)")]
    public Color rendererHighlightColor = new Color(1f, 1f, 0f, 1f);
    [Tooltip("강조색 밝기 배율. _BaseColor는 알베도 텍스처에 곱해지는 값이라 텍스처가 어두우면 강조색을 그대로 넣어도 흐리게 보임 —\n이 값을 올려서(예: 4~6) 텍스처 밝기와 무관하게 진하게 보이도록 증폭. 1이면 증폭 없음. 너무 높으면 흰색에 가깝게 날아가 보일 수 있음")]
    public float rendererHighlightIntensity = 4.0f;

    [Header("도구 사용 완료 나레이션")]
    [Tooltip("스펀지 청소 완료 시 칭찬. -1이면 재생 안 함")]
    public int spongeUsedNarrationIndex     = -1;
    [Tooltip("스펀지 완료 후 다음 도구(빗자루) 안내. -1이면 재생 안 함")]
    public int spongeNextToolNarrationIndex = -1;
    [Tooltip("빗자루 청소 완료 시 칭찬. -1이면 재생 안 함")]
    public int broomUsedNarrationIndex      = -1;
    [Tooltip("빗자루 완료 후 다음 도구(대걸레) 안내. -1이면 재생 안 함")]
    public int broomNextToolNarrationIndex  = -1;
    [Tooltip("대걸레 청소 완료 시 칭찬. -1이면 재생 안 함")]
    public int mopUsedNarrationIndex        = -1;

    [Header("잘못된 도구 나레이션")]
    [Tooltip("스펀지가 필요한데 다른 도구 사용 시. -1이면 재생 안 함")]
    public int wrongToolNeedSpongeIndex = -1;
    [Tooltip("빗자루가 필요한데 다른 도구 사용 시. -1이면 재생 안 함")]
    public int wrongToolNeedBroomIndex  = -1;
    [Tooltip("대걸레가 필요한데 다른 도구 사용 시. -1이면 재생 안 함")]
    public int wrongToolNeedMopIndex    = -1;
    [Tooltip("잘못된 도구 나레이션 반복 방지 쿨다운 (초)")]
    public float wrongToolHintCooldown  = 5f;

    [Header("청소완료 버튼 안내 나레이션")]
    [Tooltip("모든 도구 완료 후 첫 번째 버튼 안내. -1이면 재생 안 함")]
    public int cleaningButtonNar1Index = -1;
    [Tooltip("청소완료 버튼 두 번째 안내 (앞으로도 안내). -1이면 재생 안 함")]
    public int cleaningButtonNar2Index = -1;
    [Tooltip("버튼 누른 후 나레이션 시작까지 대기 시간 (초)")]
    public float cleaningButtonNarDelay = 5f;
    [Tooltip("청소완료 버튼 활성화에 사용할 ToolSwitcher")]
    public ToolSwitcher toolSwitcher;

    [Header("튜토리얼 완료")]
    [Tooltip("모든 도구 사용 완료 시 재생할 나레이션. -1이면 바로 씬 전환")]
    public int allDoneNarrationIndex = -1;

    // ── 공개 상태 ──────────────────────────────────────────────────────

    public bool AllToolsPicked { get; private set; } = false;
    public bool AllToolsUsed   { get; private set; } = false;

    // ── 내부 상태 ──────────────────────────────────────────────────────

    private TutorialNarrationManager _narration;
    private bool pickedSponge, pickedBroom, pickedMop;
    private bool usedSponge,   usedBroom,   usedMop;
    private float _lastWrongToolHintTime = -999f;

    // 청소 순서 가이드
    private int _currentStep        = -1;
    private bool _guideStarted      = false;
    private bool _buttonFlowStarted = false;

    // TutorialSequencer 페이즈 제어 (한 단계씩 외부에서 시작/완료 확인)
    private bool _externalMode          = false;
    private bool _externalWaiting       = false;
    private bool _externalStepComplete  = false;

    // BeginStep()으로 정식 시작되기 전(칭찬 나레이션 직후 ~ 다음 단계 시범 등)에
    // 미리 그 도구로 청소를 끝내버린 단계 번호. BeginStep이 실제로 그 단계에 도달하면 바로 완료 처리한다.
    private readonly HashSet<int> _preCompletedSteps = new HashSet<int>();

    // 하이라이트
    private Coroutine _blinkCoroutine;
    private readonly List<HighlightTarget> _activeTargets = new List<HighlightTarget>();
    private ToolHighlighter _activeToolHighlighter;

    private class HighlightTarget
    {
        // 데칼 방식 (스펀지·대걸레 얼룩)
        public DecalProjector decalProjector;
        public float cleanerFade;
        public float lastSetFade;
        // 데칼 색상 하이라이트용 (decalColorHighlight = true 일 때)
        public Material decalOrigMaterial;   // 원래 머티리얼 (복원용)
        public Material decalMatInstance;    // 색을 깜빡이게 할 인스턴스
        public string decalTintProp = "";    // 틴트로 쓸 색상 프로퍼티 이름
        public Color decalTintOrig = Color.white;

        // Renderer 방식 (빗자루 먼지 등 데칼이 아닌 오브젝트)
        public Renderer renderer;
        public Material[] originalMaterials;
        public Material[] instanceMaterials;
        public float rendererOrigAlpha = 1f;   // Opacity 모드 알파 깜빡임용
        public string rendererAlphaProp = "";
        // ColorTint 모드용 (머티리얼별 원래 색 보존 — _EMISSION 키워드 없이 _BaseColor/_Color 자체를 틴트)
        public string rendererTintProp = "";
        public Color[] rendererTintOrig;
    }

    // ── 초기화 ─────────────────────────────────────────────────────────

    void Awake()
    {
        _narration = TutorialNarrationManager.Instance;
        if (toolSwitcher == null)
            toolSwitcher = FindFirstObjectByType<ToolSwitcher>();
    }

    // ── 외부 API : 청소 순서 가이드 ────────────────────────────────────

    // TutorialSequencer에서 플레이어 따라하기 단계 진입 시 호출
    public void StartGuide()
    {
        if (_guideStarted) return;
        _guideStarted = true;

        if (_narration == null)
            _narration = TutorialNarrationManager.Instance;

        if (steps.Length > 0)
            AdvanceToStep(0);
    }

    // ── 외부 API : TutorialSequencer 페이즈 제어 ───────────────────────

    // 시퀀서가 마지막 WaitForPractice 완료를 확인하는 플래그
    public bool ExternalStepComplete => _externalStepComplete;

    // 시퀀서가 '청소완료 버튼 눌림'을 확인하는 플래그
    public bool CleaningButtonPressed { get; private set; }

    // 시퀀서 모드 on/off (씬 시작 시 TutorialSequencer가 호출).
    // on이면 칭찬·종료 안내 나레이션과 씬 전환을 시퀀서가 전담한다.
    public void SetExternalMode(bool on) => _externalMode = on;

    // steps[stepIndex] 한 단계만 시작한다.
    //  - 하이라이트 + 도구 하이라이터 재생
    //  - _currentStep 설정 → Cleaner의 IsWrongTool / PlayCurrentStepHint 정상 동작
    //  - 해당 도구 사용이 감지되면 ExternalStepComplete = true 로 전환
    // 칭찬·다음 안내 나레이션과 종료 처리는 시퀀서가 담당한다.
    public void BeginStep(int stepIndex)
    {
        if (_narration == null)
            _narration = TutorialNarrationManager.Instance;

        _guideStarted = true;
        _externalMode = true;
        _externalStepComplete = false;

        if (stepIndex < 0 || stepIndex >= steps.Length)
        {
            Debug.LogWarning($"[TutorialProgressTracker] BeginStep 인덱스 범위 밖: {stepIndex} (steps.Length={steps.Length})");
            _externalWaiting = false;
            _externalStepComplete = true; // 시퀀서가 멈추지 않도록 즉시 완료 처리
            return;
        }

        // 정식 시작 전에(칭찬 나레이션 직후 PreAdvanceStep으로 미리 인식된 상태에서) 이미 이 도구로
        // 청소를 끝내놓았다면, 다시 시키지 않고 바로 완료 처리한다.
        if (_preCompletedSteps.Remove(stepIndex))
        {
            _currentStep = stepIndex;
            _externalWaiting = false;
            _externalStepComplete = true;
            StopHighlight();
            return;
        }

        _externalWaiting = true;
        StopHighlight();
        AdvanceToStep(stepIndex);
    }

    // 칭찬 나레이션이 끝난 직후 TutorialSequencer가 호출.
    // 아직 이 단계가 정식으로 시작(BeginStep)되지 않았어도 "지금 차례"로 인식시켜서
    // IsWrongTool / PlayCurrentStepHint가 다음 도구를 기준으로 판단하게 한다 (하이라이트는 아직 켜지 않음).
    public void PreAdvanceStep(int nextStepIndex)
    {
        if (nextStepIndex < 0 || nextStepIndex >= steps.Length) return;
        if (_externalWaiting) return; // 이미 그 단계가 정식으로 시작된 상태면 건드리지 않음
        _currentStep = nextStepIndex;
    }

    void MarkToolUsed(ToolPickup.ToolType toolType)
    {
        switch (toolType)
        {
            case ToolPickup.ToolType.Sponge: usedSponge = true; break;
            case ToolPickup.ToolType.Broom:  usedBroom  = true; break;
            case ToolPickup.ToolType.Mop:    usedMop    = true; break;
        }
    }

    // 가이드 진행 중이고 지금 단계와 다른 도구를 쓰면 true (Cleaner에서 사용)
    public bool IsWrongTool(ToolPickup.ToolType toolType)
    {
        if (!_guideStarted || _currentStep < 0 || _currentStep >= steps.Length) return false;
        return steps[_currentStep].toolType != toolType;
    }

    // 현재 단계에서 필요한 도구 반환
    public ToolPickup.ToolType GetRequiredTool()
    {
        if (_currentStep >= 0 && _currentStep < steps.Length)
            return steps[_currentStep].toolType;
        return ToolPickup.ToolType.Sponge;
    }

    // 지정 단계의 도구 태그 문자열 ("Sponge" / "Broom" / "Mop"). 범위 밖이면 "".
    public string GetStepToolTag(int stepIndex)
        => (stepIndex >= 0 && stepIndex < steps.Length) ? steps[stepIndex].toolType.ToString() : "";

    // 이 단계 차례가 아닌 행동 시 호출 — CleaningStep.stepHintNarrationIndex 재생
    public void PlayCurrentStepHint()
    {
        if (!_guideStarted || _currentStep < 0 || _currentStep >= steps.Length) return;
        if (Time.time - _lastWrongToolHintTime < wrongToolHintCooldown) return;

        int idx = steps[_currentStep].stepHintNarrationIndex;
        if (idx < 0) return;

        _lastWrongToolHintTime = Time.time;
        _narration?.Play(idx);
    }

    // ── 외부 API : 이벤트 ──────────────────────────────────────────────

    // ToolPickup에서 호출 — 도구를 집었을 때 (나레이션 없음)
    public void OnToolPicked(ToolPickup.ToolType toolType)
    {
        switch (toolType)
        {
            case ToolPickup.ToolType.Sponge: pickedSponge = true; break;
            case ToolPickup.ToolType.Broom:  pickedBroom  = true; break;
            case ToolPickup.ToolType.Mop:    pickedMop    = true; break;
        }
        CheckAllPicked();
    }

    // 각 Cleaner에서 호출 — 실제 청소 동작을 1번 했을 때
    public void OnToolUsed(ToolPickup.ToolType toolType)
    {
        // 시퀀서 페이즈 제어 모드: 칭찬/진행/하이라이트 이동은 시퀀서가 담당한다.
        if (_externalWaiting)
        {
            bool matchesCurrentStep = _currentStep >= 0 && _currentStep < steps.Length
                                      && steps[_currentStep].toolType == toolType;

            MarkToolUsed(toolType);

            if (matchesCurrentStep)
            {
                _externalWaiting = false;
                _externalStepComplete = true;
                StopHighlight();
            }

            CheckAllUsed(); // 모든 도구 완료 시 종료 흐름(FinishTutorial)은 그대로 진행
            return;
        }

        // PreAdvanceStep으로 다음 단계가 미리 "현재 차례"로 인식된 상태(BeginStep 정식 시작 전)에서
        // 그 도구로 먼저 청소해버린 경우 — 잘못된 도구로 취급하지 않고 완료를 기억해둔다.
        // BeginStep이 이 단계에 도달하면 바로 통과된다.
        if (_externalMode && _currentStep >= 0 && _currentStep < steps.Length
            && steps[_currentStep].toolType == toolType)
        {
            MarkToolUsed(toolType);
            _preCompletedSteps.Add(_currentStep);
            CheckAllUsed();
            return;
        }

        switch (toolType)
        {
            case ToolPickup.ToolType.Sponge:
                if (!usedSponge)
                {
                    usedSponge = true;
                    StartCoroutine(PlayPraiseThenAdvance(spongeUsedNarrationIndex, spongeNextToolNarrationIndex, toolType));
                }
                break;
            case ToolPickup.ToolType.Broom:
                if (!usedBroom)
                {
                    usedBroom = true;
                    StartCoroutine(PlayPraiseThenAdvance(broomUsedNarrationIndex, broomNextToolNarrationIndex, toolType));
                }
                break;
            case ToolPickup.ToolType.Mop:
                if (!usedMop)
                {
                    usedMop = true;
                    AdvanceStep(toolType);
                    PlayIfValid(mopUsedNarrationIndex);
                }
                break;
        }
        CheckAllUsed();
    }

    // 각 Cleaner에서 호출 — 잘못된 도구로 청소하려 할 때
    public void PlayWrongToolHint(ToolPickup.ToolType neededTool)
    {
        if (Time.time - _lastWrongToolHintTime < wrongToolHintCooldown) return;
        _lastWrongToolHintTime = Time.time;

        int idx = neededTool switch
        {
            ToolPickup.ToolType.Sponge => wrongToolNeedSpongeIndex,
            ToolPickup.ToolType.Broom  => wrongToolNeedBroomIndex,
            ToolPickup.ToolType.Mop    => wrongToolNeedMopIndex,
            _ => -1
        };
        PlayIfValid(idx);
    }

    // 기존 호환 유지
    public void PlayWrongToolHint(bool needMopForFloor)
    {
        PlayWrongToolHint(needMopForFloor ? ToolPickup.ToolType.Mop : ToolPickup.ToolType.Broom);
    }

    // ── 내부 : 청소 순서 진행 ──────────────────────────────────────────

    IEnumerator PlayPraiseThenAdvance(int praiseIndex, int nextToolIndex, ToolPickup.ToolType toolType)
    {
        if (praiseIndex >= 0)
        {
            _narration.Play(praiseIndex);
            yield return _narration.WaitUntilDone();
        }
        if (nextToolIndex >= 0)
        {
            _narration.Play(nextToolIndex);
            AdvanceStep(toolType); // 나레이션 시작과 동시에 하이라이트
            yield return _narration.WaitUntilDone();
        }
        else
        {
            AdvanceStep(toolType);
        }
    }

    void AdvanceStep(ToolPickup.ToolType toolType)
    {
        if (!_guideStarted || _currentStep < 0 || _currentStep >= steps.Length) return;
        if (steps[_currentStep].toolType != toolType) return;

        StopHighlight();
        int next = _currentStep + 1;
        if (next < steps.Length)
            AdvanceToStep(next);
        else
            _currentStep = steps.Length; // 모든 단계 완료
    }

    void AdvanceToStep(int index)
    {
        _currentStep = index;
        var step = steps[index];
        if (!string.IsNullOrEmpty(step.cleanableTag))
            StartHighlightForTag(step.cleanableTag);

        _activeToolHighlighter = step.toolHighlighter;
        _activeToolHighlighter?.StartHighlight();
    }

    // ── 내부 : 완료 체크 ───────────────────────────────────────────────

    void CheckAllPicked()
    {
        if (AllToolsPicked) return;
        bool done = (!needSponge || pickedSponge)
                 && (!needBroom  || pickedBroom)
                 && (!needMop    || pickedMop);
        if (done) AllToolsPicked = true;
    }

    void CheckAllUsed()
    {
        if (AllToolsUsed) return;
        bool done = (!needSponge || usedSponge)
                 && (!needBroom  || usedBroom)
                 && (!needMop    || usedMop);
        if (done)
        {
            AllToolsUsed = true;
            StartCoroutine(FinishTutorial());
        }
    }

    IEnumerator FinishTutorial()
    {
        // 시퀀서 모드에서는 종료 안내 나레이션·버튼 잠금 해제·씬 전환을 모두
        // TutorialSequencer(청소완료 버튼 안내 / WaitForCleaningButton 단계)가 담당한다.
        if (_externalMode) yield break;

        if (_narration != null && cleaningButtonNar1Index >= 0)
        {
            _narration.Play(cleaningButtonNar1Index);
            yield return _narration.WaitUntilDone();
        }

        toolSwitcher?.UnlockCleaningCompleteButton();
        // 이후 흐름은 플레이어가 청소완료 버튼을 눌렀을 때 OnCleaningButtonPressed()에서 처리
    }

    // 청소완료 버튼 스크립트에서 호출
    public void OnCleaningButtonPressed()
    {
        if (!AllToolsUsed || _buttonFlowStarted) return;
        _buttonFlowStarted = true;
        CleaningButtonPressed = true;

        // 시퀀서 모드에서는 이후 멘트(19·20)와 씬 전환을 시퀀서가 담당한다.
        if (_externalMode) return;

        StartCoroutine(CleaningButtonFlow());
    }

    IEnumerator CleaningButtonFlow()
    {
        if (cleaningButtonNarDelay > 0f)
            yield return new WaitForSeconds(cleaningButtonNarDelay);

        if (_narration != null)
        {
            if (cleaningButtonNar2Index >= 0)
            {
                _narration.Play(cleaningButtonNar2Index);
                yield return _narration.WaitUntilDone();
            }
            if (allDoneNarrationIndex >= 0)
            {
                _narration.Play(allDoneNarrationIndex);
                yield return _narration.WaitUntilDone();
            }
        }

        if (StageFlowManager.Instance != null)
            StageFlowManager.Instance.GoToStreet();
        else
            SceneManager.LoadSceneAsync("Street");
    }

    void PlayIfValid(int index)
    {
        if (index >= 0) _narration?.Play(index);
    }

    // ── 내부 : 하이라이트 ──────────────────────────────────────────────

    // Opacity 모드에서 불투명 머티리얼을 임시로 반투명 렌더링으로 전환 (인스턴스에만 적용, 원본은 그대로)
    static void MakeMaterialTransparent(Material m)
    {
        if (m == null) return;

        if (m.HasProperty("_Surface")) // URP Lit 계열
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else if (m.HasProperty("_Mode")) // Built-in Standard
        {
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_ALPHABLEND_ON");
            m.renderQueue = 3000;
        }
        else
        {
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }

    void StartHighlightForTag(string tag)
    {
        StopHighlight();
        _activeTargets.Clear();

        GameObject[] found;
        try { found = GameObject.FindGameObjectsWithTag(tag); }
        catch { return; }

        foreach (var obj in found)
        {
            if (obj == null) continue;

            var dp = obj.GetComponent<DecalProjector>();
            if (dp != null)
            {
                var ht = new HighlightTarget
                {
                    decalProjector = dp,
                    cleanerFade    = dp.fadeFactor,
                    lastSetFade    = dp.fadeFactor
                };

                // ColorTint 모드: 데칼 머티리얼을 인스턴스로 교체해 색상(에미션/틴트)을 깜빡이게 한다
                if (highlightStyle == HighlightStyle.ColorTint && dp.material != null)
                {
                    ht.decalOrigMaterial = dp.material;

                    var inst = new Material(dp.material);
                    inst.EnableKeyword("_EMISSION");
                    inst.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

                    foreach (var prop in new[] { "Tint_Color", "_BaseColor", "_Color" })
                    {
                        if (!inst.HasProperty(prop)) continue;
                        ht.decalTintProp = prop;
                        ht.decalTintOrig = inst.GetColor(prop);
                        break;
                    }

                    ht.decalMatInstance = inst;
                    dp.material = inst;
                }

                _activeTargets.Add(ht);
                continue;
            }

            // 데칼이 없으면 Renderer(빗자루 먼지 등)를 하이라이트
            var rend = obj.GetComponent<Renderer>() ?? obj.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                var originals = rend.sharedMaterials;
                var instances = new Material[originals.Length];
                for (int i = 0; i < originals.Length; i++)
                {
                    if (originals[i] == null) continue;
                    instances[i] = new Material(originals[i]);
                }

                var htR = new HighlightTarget
                {
                    renderer          = rend,
                    originalMaterials = originals,
                    instanceMaterials = instances
                };

                if (highlightStyle == HighlightStyle.ColorTint)
                {
                    Material tintRef = System.Array.Find(instances, m => m != null);
                    foreach (var p in new[] { "_BaseColor", "_Color" })
                    {
                        if (tintRef != null && tintRef.HasProperty(p)) { htR.rendererTintProp = p; break; }
                    }
                    if (!string.IsNullOrEmpty(htR.rendererTintProp))
                    {
                        htR.rendererTintOrig = new Color[instances.Length];
                        for (int i = 0; i < instances.Length; i++)
                            htR.rendererTintOrig[i] = instances[i] != null
                                ? instances[i].GetColor(htR.rendererTintProp)
                                : Color.white;
                    }

                    // _BaseColor는 알베도 텍스처에 곱해지는 값이라 발자국처럼 텍스처 자체가 검정에 가까우면
                    // 아무리 밝은 색을 곱해도 검정×색 = 검정이라 하이라이트가 전혀 안 보인다.
                    // 지원되는 머티리얼은 _EmissionColor(더하기 방식)도 같이 켜서 텍스처 밝기와 무관하게 보이게 한다.
                    // (프로젝트 내 얼룩 머티리얼 중 최소 하나는 _EMISSION 키워드를 미리 켜둬야 빌드에서 스트립되지 않음)
                    foreach (var m in instances)
                    {
                        if (m == null || !m.HasProperty("_EmissionColor")) continue;
                        m.EnableKeyword("_EMISSION");
                        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                    }
                }
                else // Opacity — 데칼 fadeFactor처럼 알파를 깜빡이게 하려고 반투명 전환
                {
                    Material first = System.Array.Find(instances, m => m != null);
                    foreach (var p in new[] { "_BaseColor", "_Color" })
                    {
                        if (first != null && first.HasProperty(p)) { htR.rendererAlphaProp = p; break; }
                    }
                    htR.rendererOrigAlpha = (first != null && !string.IsNullOrEmpty(htR.rendererAlphaProp))
                        ? first.GetColor(htR.rendererAlphaProp).a : 1f;
                    foreach (var m in instances) MakeMaterialTransparent(m);
                }

                rend.materials = instances;
                _activeTargets.Add(htR);
            }
        }

        if (_activeTargets.Count > 0)
            _blinkCoroutine = StartCoroutine(BlinkLoop());
    }

    IEnumerator BlinkLoop()
    {
        while (true)
        {
            float blink = Mathf.PingPong(Time.time * blinkSpeed, 1f);

            foreach (var t in _activeTargets)
            {
                if (t.decalProjector != null)
                {
                    // 청소 스크립트가 fadeFactor를 변경했으면 진행도 반영
                    if (Mathf.Abs(t.decalProjector.fadeFactor - t.lastSetFade) > 0.001f)
                        t.cleanerFade = t.decalProjector.fadeFactor;

                    float blinkFade = Mathf.Lerp(t.cleanerFade * decalFadeMin, t.cleanerFade * decalFadeMax, blink);
                    t.decalProjector.fadeFactor = blinkFade;
                    t.lastSetFade = blinkFade;

                    if (t.decalMatInstance != null)
                    {
                        if (t.decalMatInstance.HasProperty("_EmissionColor"))
                            t.decalMatInstance.SetColor("_EmissionColor",
                                rendererHighlightColor * rendererHighlightIntensity * blink);
                        if (!string.IsNullOrEmpty(t.decalTintProp))
                            t.decalMatInstance.SetColor(t.decalTintProp,
                                Color.Lerp(t.decalTintOrig, rendererHighlightColor, blink));
                    }
                }
                else if (t.instanceMaterials != null)
                {
                    if (highlightStyle == HighlightStyle.ColorTint)
                    {
                        Color boosted = rendererHighlightColor * rendererHighlightIntensity;

                        if (!string.IsNullOrEmpty(t.rendererTintProp) && t.rendererTintOrig != null)
                        {
                            // _BaseColor는 알베도 텍스처에 곱해지는 값이라, 텍스처 자체가 어두우면
                            // 강조색을 그대로 넣어도 곱해진 결과가 흐리게 보인다.
                            // rendererHighlightIntensity로 목표색을 밝게 증폭해 텍스처 밝기와 무관하게 진하게 나오도록 한다.
                            for (int i = 0; i < t.instanceMaterials.Length; i++)
                            {
                                var mat = t.instanceMaterials[i];
                                if (mat == null) continue;
                                Color orig = t.rendererTintOrig[i];
                                Color tinted = Color.Lerp(orig, boosted, blink);
                                tinted.a = orig.a; // 알파는 원래 값 유지 — 색상만 하이라이트
                                mat.SetColor(t.rendererTintProp, tinted);
                            }
                        }

                        // 발자국처럼 텍스처 자체가 검정에 가까우면 _BaseColor 곱셈만으론 절대 밝아지지 않는다.
                        // 지원되는 머티리얼은 _EmissionColor(더하기 방식)도 같이 깜빡여서 텍스처 밝기와 무관하게 보이게 한다.
                        foreach (var mat in t.instanceMaterials)
                        {
                            if (mat == null || !mat.HasProperty("_EmissionColor")) continue;
                            mat.SetColor("_EmissionColor", boosted * blink);
                        }
                    }
                    else if (!string.IsNullOrEmpty(t.rendererAlphaProp)) // Opacity: 데칼처럼 알파 깜빡임
                    {
                        float a = Mathf.Lerp(t.rendererOrigAlpha * decalFadeMin,
                                             t.rendererOrigAlpha * decalFadeMax, blink);
                        foreach (var mat in t.instanceMaterials)
                        {
                            if (mat == null || !mat.HasProperty(t.rendererAlphaProp)) continue;
                            Color c = mat.GetColor(t.rendererAlphaProp);
                            c.a = a;
                            mat.SetColor(t.rendererAlphaProp, c);
                        }
                    }
                }
            }

            yield return null;
        }
    }

    void StopHighlight()
    {
        if (_blinkCoroutine != null)
        {
            StopCoroutine(_blinkCoroutine);
            _blinkCoroutine = null;
        }

        foreach (var t in _activeTargets)
        {
            if (t.decalProjector != null)
            {
                t.decalProjector.fadeFactor = t.cleanerFade;
                if (t.decalOrigMaterial != null)
                    t.decalProjector.material = t.decalOrigMaterial;
            }
            else if (t.renderer != null || t.instanceMaterials != null)
            {
                if (t.renderer != null)                    // 먼지가 파괴됐으면 null
                    t.renderer.sharedMaterials = t.originalMaterials;
            }

            if (t.decalMatInstance != null) Destroy(t.decalMatInstance);
            if (t.instanceMaterials != null)
                foreach (var mat in t.instanceMaterials)
                    if (mat != null) Destroy(mat);
        }

        _activeTargets.Clear();

        _activeToolHighlighter?.StopHighlight();
        _activeToolHighlighter = null;
    }

    void OnDestroy() => StopHighlight();
}
