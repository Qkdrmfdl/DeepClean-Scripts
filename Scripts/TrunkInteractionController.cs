using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TrunkNextScene
{
    Tutorial,   // 인트로 씬에서 사용 — 완료 후 튜토리얼로 이동
    NextStage,  // 스트릿 씬에서 사용 — 완료 후 다음 스테이지로 이동
}

/// <summary>
/// 트렁크 상호작용 컨트롤러.
/// UnlockTrunk() 호출 → 뚜껑 열림 → 필요 도구 하이라이트 → 클릭 시 StageFlowManager에 등록.
/// 모든 도구 선택 완료 → 뚜껑 자동 닫힘 → NextSceneMode에 따라 씬 전환.
///
/// 설정 방법:
///   1. 트렁크 뚜껑 오브젝트에 이 스크립트 부착 (또는 빈 오브젝트)
///   2. Trunk Lid : 트렁크 뚜껑 Transform
///   3. Tool Entries : 씬에 배치된 도구 오브젝트 + 태그 목록 설정
///   4. Next Scene Mode : 인트로면 Tutorial, 스트릿씬이면 NextStage
///   5. 스트릿씬 한정 — AutoDrive.onDrivingComplete → UnlockTrunk() 연결
/// </summary>
public class TrunkInteractionController : MonoBehaviour
{
    // ═══════════════════════════════════════════════════
    //  Inspector 설정
    // ═══════════════════════════════════════════════════

    [Header("씬 전환 설정")]
    [Tooltip("Tutorial: 인트로 씬에서 사용 (완료 후 튜토리얼 이동)\nNextStage: 스트릿 씬에서 사용 (완료 후 다음 스테이지 이동)")]
    public TrunkNextScene nextSceneMode = TrunkNextScene.NextStage;

    [Header("인트로 전용 설정")]
    [Tooltip("인트로 씬에서 선택해야 할 도구 태그 목록.\n비어있으면 StageFlowManager.GetRequiredToolTags()를 사용합니다 (스트릿 씬 동작).")]
    public string[] overrideRequiredTags = new string[0];

    [Header("컨트롤러")]
    [Tooltip("오른손 컨트롤러 Transform. 반드시 Inspector에서 연결해야 합니다.")]
    public Transform rightHandController;

    [Header("도구 담기 범위")]
    [Tooltip("양동이 초록구 반경 (m). 도구가 이 범위 안에 들어오면 자동으로 담깁니다.")]
    public float bucketDetectRadius = 0.5f;

    [Header("트렁크 뚜껑")]
    [Tooltip("회전할 트렁크 뚜껑 Transform")]
    public Transform trunkLid;

    [Tooltip("닫힌 상태 로컬 오일러각")]
    public Vector3 closedRotation = new Vector3(0f, 0f, 0f);

    [Tooltip("열린 상태 로컬 오일러각")]
    public Vector3 openRotation = new Vector3(-110f, 0f, 0f);

    [Tooltip("뚜껑 열리는 속도 (1 = 1초)")]
    public float openSpeed = 1.5f;

    [Tooltip("뚜껑 닫히는 속도 (1 = 1초)")]
    public float closeSpeed = 1.2f;

    [Header("도구 항목")]
    [Tooltip("트렁크 안에 있는 도구 오브젝트 목록.\n각 항목의 Tool Tag는 StageFlowManager.toolRequirements와 동일해야 합니다.")]
    public TrunkToolEntry[] toolEntries;

    [Header("하이라이트")]
    [Tooltip("필요 도구에 적용할 하이라이트 색상")]
    public Color highlightColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Tooltip("필요 없는 도구에 적용할 비활성 색상 (선택)")]
    public Color inactiveColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    [Tooltip("하이라이트 깜빡임 속도")]
    public float blinkSpeed = 2f;

    [Header("완료 연출")]
    [Tooltip("모든 도구 선택 완료 시 재생할 효과음 (트렁크 잠금음 등)")]
    public AudioClip completionSound;

    [Tooltip("완료 효과음 볼륨")]
    [Range(0f, 1f)]
    public float completionSoundVolume = 1f;

    [Tooltip("트렁크 닫힌 후 씬 전환까지 대기 시간 (초)")]
    public float transitionDelay = 1.0f;

    [Header("디버그")]
    public bool showDebugInfo = true;

    // ═══════════════════════════════════════════════════
    //  내부 상태
    // ═══════════════════════════════════════════════════

    private bool isUnlocked = false;
    private bool isOpen = false;
    private bool isClosing = false;

    // 도구별 내부 데이터
    private class ToolRuntime
    {
        public TrunkToolEntry entry;
        public Renderer[] renderers;
        public Color[][] originalColors;
        // interactable 없음 — TrunkToolGrabber가 직접 transform 조작
        public bool isRequired;
        public bool isSelected;
        public Coroutine blinkCoroutine;
    }

    private List<ToolRuntime> toolRuntimes = new List<ToolRuntime>();

    // ═══════════════════════════════════════════════════
    //  Unity 생명주기
    // ═══════════════════════════════════════════════════

    void Start()
    {
        // 모든 도구 초기 비활성화 (트렁크가 열리기 전까지 잠금)
        foreach (var entry in toolEntries)
        {
            if (entry.toolObject != null)
                entry.toolObject.SetActive(false);
        }

    }

    // ═══════════════════════════════════════════════════
    //  공개 메서드
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// AutoDrive.onDrivingComplete에 연결.
    /// 트렁크 뚜껑을 열고 필요 도구를 활성화.
    /// </summary>
    public void UnlockTrunk()
    {
        if (isUnlocked) return;
        isUnlocked = true;

        Log("트렁크 잠금 해제 → 뚜껑 열기 시작");
        StartCoroutine(OpenTrunkSequence());
    }

    // ═══════════════════════════════════════════════════
    //  트렁크 열기 시퀀스
    // ═══════════════════════════════════════════════════

    private IEnumerator OpenTrunkSequence()
    {
        // 1. 뚜껑 회전 시작 — 기다리지 않고 바로 도구 준비를 병행한다.
        // (뚜껑이 다 열린 뒤에야 도구를 활성화하면, 뚜껑은 열려 있는데 트렁크가 잠깐 비어있다가
        //  도구들이 한 박자 늦게 툭 튀어나오는 것처럼 보인다. 뚜껑이 열리는 동안 — 즉 도구가
        //  아직 안 보이는 동안 — 미리 활성화/설정을 끝내두면 뚜껑이 열리는 순간 바로 도구가 보인다.)
        Coroutine lidCoroutine = trunkLid != null ? StartCoroutine(RotateTrunkLid()) : null;

        // 2. 필요 도구 목록 결정
        // overrideRequiredTags가 설정된 경우(인트로) 우선 사용, 아니면 StageFlowManager에서 가져옴
        string[] requiredTags;
        if (overrideRequiredTags != null && overrideRequiredTags.Length > 0)
        {
            requiredTags = overrideRequiredTags;
            Log($"[인트로 모드] 오버라이드 도구 태그 사용: [{string.Join(", ", requiredTags)}]");
        }
        else
        {
            requiredTags = StageFlowManager.Instance != null
                ? StageFlowManager.Instance.GetRequiredToolTags()
                : new string[0];
            Log($"필요 도구: [{string.Join(", ", requiredTags)}]");
        }

        // 트렁크 열리면 양동이를 오른손에 부착 (이번 스테이지 필요 도구로 슬롯 매핑)
        BucketAttacher.Instance?.AttachToLeftHand(requiredTags);

        // 3. 각 도구 활성화 및 런타임 설정
        foreach (var entry in toolEntries)
        {
            if (entry.toolObject == null) continue;

            entry.toolObject.SetActive(true);

            // XRGrabInteractable 비활성화 — TrunkToolGrabber와 transform 제어 충돌 방지
            foreach (var grab in entry.toolObject.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(true))
                grab.enabled = false;

            // XRSimpleInteractable: 레이가 도구까지 늘어나도록 유지 (오브젝트는 움직이지 않음)
            // 기존 selectEntered 리스너 제거 후 활성화
            var simpleInteractable = entry.toolObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            if (simpleInteractable == null)
                simpleInteractable = entry.toolObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            simpleInteractable.selectEntered.RemoveAllListeners();
            simpleInteractable.enabled = true;

            ToolRuntime rt = new ToolRuntime
            {
                entry = entry,
                isRequired = IsTagRequired(entry.toolTag, requiredTags),
                isSelected = false
            };

            // 렌더러 & 원본 색상 캐싱
            rt.renderers = entry.toolObject.GetComponentsInChildren<Renderer>();
            rt.originalColors = new Color[rt.renderers.Length][];
            for (int i = 0; i < rt.renderers.Length; i++)
            {
                Material[] mats = rt.renderers[i].materials;
                rt.originalColors[i] = new Color[mats.Length];
                for (int m = 0; m < mats.Length; m++)
                    rt.originalColors[i][m] = mats[m].color;
            }

            if (rt.isRequired)
            {
                // TrunkToolGrabber가 인식하기 위한 마커
                var marker = entry.toolObject.GetComponent<TrunkToolMarker>();
                if (marker == null) marker = entry.toolObject.AddComponent<TrunkToolMarker>();
                marker.toolTag = entry.toolTag;
                marker.controller = this;

                // Physics.OverlapSphere 감지를 위해 Collider 필요
                EnsureCollider(entry.toolObject);

                rt.blinkCoroutine = StartCoroutine(BlinkHighlight(rt));
                Log($"도구 활성화 (필요): {entry.toolTag}");
            }
            else
            {
                // 불필요 도구: 흐린 색상, 인터랙션 없음
                ApplyFlatColor(rt, inactiveColor, 0.8f);
                Log($"도구 비활성화 (불필요): {entry.toolTag}");
            }

            toolRuntimes.Add(rt);
        }

        // 자석 그랩 활성화
        var grabber = GetComponent<TrunkToolGrabber>();
        if (grabber == null) grabber = gameObject.AddComponent<TrunkToolGrabber>();

        // 컨트롤러 직접 주입 — auto-search 실패 방지
        if (rightHandController != null)
            grabber.rightControllerTransform = rightHandController;
        else
            Log("⚠ rightHandController가 비어있습니다. Inspector에서 RightHand Controller를 연결해주세요.");

        // 양동이 감지 반경 적용
        grabber.bucketRadius = bucketDetectRadius;

        grabber.enabled = true;

        // 뚜껑 애니메이션이 아직 덜 끝났으면 마저 기다린다 (도구 준비가 뚜껑보다 먼저 끝나는 게 보통)
        if (lidCoroutine != null)
            yield return lidCoroutine;
        else
        {
            yield return new WaitForSeconds(0.5f);
            Log("⚠ Trunk Lid가 연결되지 않았습니다. 회전 생략.");
        }

        isOpen = true;
    }

    private IEnumerator RotateTrunkLid()
    {
        Quaternion from = Quaternion.Euler(closedRotation);
        Quaternion to = Quaternion.Euler(openRotation);
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime * openSpeed;
            trunkLid.localRotation = Quaternion.Lerp(from, to, Mathf.Clamp01(t));
            yield return null;
        }

        trunkLid.localRotation = to;
        Log("트렁크 뚜껑 열림 완료");
    }

    // ═══════════════════════════════════════════════════
    //  도구 선택 처리
    // ═══════════════════════════════════════════════════

    public void OnToolSelected(string toolTag)
    {
        ToolRuntime rt = FindRuntime(toolTag);
        if (rt == null || rt.isSelected) return;

        rt.isSelected = true;

        // 깜빡임 중단
        if (rt.blinkCoroutine != null)
        {
            StopCoroutine(rt.blinkCoroutine);
            rt.blinkCoroutine = null;
        }

        // 도구를 양동이 슬롯으로 순간이동 + 덜그럭 소리
        if (BucketAttacher.Instance != null)
            BucketAttacher.Instance.PlaceToolInBucket(rt.entry.toolObject, toolTag);
        else
            rt.entry.toolObject.SetActive(false);

        // StageFlowManager에 등록 (스트릿 씬 전용 — 인트로는 자체 추적으로 충분)
        bool usingOverride = overrideRequiredTags != null && overrideRequiredTags.Length > 0;
        if (!usingOverride)
        {
            if (StageFlowManager.Instance != null)
                StageFlowManager.Instance.RegisterSelectedTool(toolTag);
            else
                Debug.LogError("[TrunkInteractionController] StageFlowManager.Instance가 null입니다!");
        }

        Log($"도구 선택됨: {toolTag}");

        // 모든 필요 도구가 선택됐는지 확인 (UI 처리)
        CheckAllRequiredSelected();
    }

    private void CheckAllRequiredSelected()
    {
        foreach (var rt in toolRuntimes)
        {
            if (rt.isRequired && !rt.isSelected) return;
        }

        if (isClosing) return;
        isClosing = true;

        Log("✅ 모든 도구 선택 완료 → 트렁크 닫힘 시작");
        StartCoroutine(CloseTrunkAndTransition());
    }

    private IEnumerator CloseTrunkAndTransition()
    {
        // 잠깐 텀 (마지막 선택 피드백이 끝날 시간)
        var grabber = GetComponent<TrunkToolGrabber>();
        if (grabber != null) grabber.enabled = false;

        yield return new WaitForSeconds(0.4f);

        // 효과음 재생
        if (completionSound != null)
            AudioSource.PlayClipAtPoint(completionSound, Camera.main != null ? Camera.main.transform.position : Vector3.zero, completionSoundVolume);

        // 트렁크 뚜껑 닫힘
        if (trunkLid != null)
        {
            Quaternion from = Quaternion.Euler(openRotation);
            Quaternion to   = Quaternion.Euler(closedRotation);
            float t = 0f;

            while (t < 1f)
            {
                t += Time.deltaTime * closeSpeed;
                trunkLid.localRotation = Quaternion.Lerp(from, to, Mathf.Clamp01(t));
                yield return null;
            }

            trunkLid.localRotation = to;
            Log("트렁크 뚜껑 닫힘 완료");
        }

        // 씬 전환 전 대기
        yield return new WaitForSeconds(transitionDelay);

        if (StageFlowManager.Instance == null)
        {
            Debug.LogError("[TrunkInteractionController] StageFlowManager.Instance가 null입니다!");
            yield break;
        }

        if (nextSceneMode == TrunkNextScene.Tutorial)
            StageFlowManager.Instance.GoToTutorial();
        else
            StageFlowManager.Instance.GoToNextStage();
    }

    // ═══════════════════════════════════════════════════
    //  색상 처리
    // ═══════════════════════════════════════════════════

    private IEnumerator BlinkHighlight(ToolRuntime rt)
    {
        while (true)
        {
            float t = (Mathf.Sin(Time.time * blinkSpeed) + 1f) / 2f;
            for (int i = 0; i < rt.renderers.Length; i++)
            {
                Material[] mats = rt.renderers[i].materials;
                for (int m = 0; m < mats.Length; m++)
                    mats[m].color = Color.Lerp(rt.originalColors[i][m], highlightColor, t * 0.9f);
            }
            yield return null;
        }
    }

    private IEnumerator FlashSelected(ToolRuntime rt)
    {
        // 0.3초 동안 하얗게 반짝
        float elapsed = 0f;
        while (elapsed < 0.3f)
        {
            float t = 1f - (elapsed / 0.3f);
            for (int i = 0; i < rt.renderers.Length; i++)
            {
                Material[] mats = rt.renderers[i].materials;
                for (int m = 0; m < mats.Length; m++)
                    mats[m].color = Color.Lerp(rt.originalColors[i][m], Color.white, t);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        RestoreColors(rt);
    }

    private void RestoreColors(ToolRuntime rt)
    {
        for (int i = 0; i < rt.renderers.Length; i++)
        {
            Material[] mats = rt.renderers[i].materials;
            for (int m = 0; m < mats.Length; m++)
                mats[m].color = rt.originalColors[i][m];
        }
    }

    private void ApplyFlatColor(ToolRuntime rt, Color color, float blend)
    {
        for (int i = 0; i < rt.renderers.Length; i++)
        {
            Material[] mats = rt.renderers[i].materials;
            for (int m = 0; m < mats.Length; m++)
                mats[m].color = Color.Lerp(rt.originalColors[i][m], color, blend);
        }
    }

    // ═══════════════════════════════════════════════════
    //  유틸리티
    // ═══════════════════════════════════════════════════

    private void EnsureCollider(GameObject obj)
    {
        if (obj.GetComponent<Collider>() == null && obj.GetComponentInChildren<Collider>() == null)
        {
            var col = obj.AddComponent<SphereCollider>();
            col.radius = 0.15f;
            Log($"⚠ {obj.name}에 Collider 없음 → SphereCollider 자동 추가 (Inspector에서 직접 추가 권장)");
        }
    }

    private bool IsTagRequired(string tag, string[] requiredTags)
    {
        foreach (string t in requiredTags)
            if (t == tag) return true;
        return false;
    }

    private ToolRuntime FindRuntime(string toolTag)
    {
        foreach (var rt in toolRuntimes)
            if (rt.entry.toolTag == toolTag) return rt;
        return null;
    }

    private void Log(string msg)
    {
        if (showDebugInfo)
            Debug.Log($"[TrunkInteractionController] {msg}");
    }
}

// ═══════════════════════════════════════════════════════
//  보조 데이터 클래스
// ═══════════════════════════════════════════════════════

[System.Serializable]
public class TrunkToolEntry
{
    [Tooltip("도구 3D 오브젝트")]
    public GameObject toolObject;

    [Tooltip("이 도구의 태그. StageFlowManager.toolRequirements의 태그와 정확히 일치해야 함.\n예: Sponge / Mop / Broom")]
    public string toolTag;
}
