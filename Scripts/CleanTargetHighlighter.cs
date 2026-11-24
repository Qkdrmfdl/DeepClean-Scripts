using UnityEngine;
using UnityEngine.XR;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class CleanTargetHighlighter : MonoBehaviour
{
    [Header("하이라이트 버튼 UI")]
    [Tooltip("하이라이트 활성화 버튼")]
    public Button highlightButton;

    [Tooltip("남은 무료 횟수 텍스트 (버튼 옆)")]
    public TextMeshProUGUI remainingUsesText;

    [Header("무료 사용 설정")]
    [Tooltip("무료로 사용할 수 있는 횟수")]
    public int freeUseCount = 3;

    [Tooltip("무료 소진 후 1회 사용 비용 (원)")]
    public int highlightCost = 500;

    [Header("구매 확인 UI")]
    [Tooltip("구매 확인 패널 (평소 비활성)")]
    public GameObject shopPanel;

    [Tooltip("구매 안내 텍스트 — 비용이 자동으로 표시됨")]
    public TextMeshProUGUI shopMessageText;

    [Tooltip("확인 버튼")]
    public Button confirmButton;

    [Tooltip("취소 버튼")]
    public Button cancelButton;

    [Tooltip("잔액 부족 안내 텍스트 (평소 비활성)")]
    public TextMeshProUGUI insufficientFundsText;

    [Header("하이라이트 설정")]
    [Tooltip("하이라이트 지속 시간 (초)")]
    public float highlightDuration = 3.0f;

    [Tooltip("하이라이트 강조색 (Renderer용). _BaseColor/_Color를 직접 이 색으로 틴트함")]
    public Color highlightColor = new Color(1f, 1f, 0f, 1f);

    [Tooltip("강조색 밝기 배율. _BaseColor는 알베도 텍스처에 곱해지는 값이라 텍스처가 어두우면 흐리게 보임 — 배율을 올려서(3~6 권장) 진하게 보정. 1이면 증폭 없음")]
    public float emissionIntensity = 4.0f;

    [Tooltip("깜빡임 효과 사용")]
    public bool useBlinking = true;

    [Tooltip("깜빡임 속도")]
    public float blinkSpeed = 2.0f;

    [Header("청소 대상 태그 목록 (스테이지별로 Inspector에서 설정)")]
    [Tooltip("태그가 붙은 오브젝트의 Renderer와 DecalProjector를 자동으로 탐색합니다.")]
    public string[] cleanableObjectTags = new string[]
    {
        "Cleanable_Mop",
        "Cleanable_Sponge",
        "Cleanable_Broom",
        "Cleanable_Hand",
        "Cleanable_Scraper",
        "Cleanable_Flapper",
        "Cleanable_ToiletBrush"
    };

    [Header("ToolSwitcher 연동")]
    public ToolSwitcher toolSwitcher;

    [Header("컨트롤러 설정")]
    public InputDeviceCharacteristics rightControllerCharacteristics =
        InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;

    [Header("디버그")]
    public bool showDebugInfo = true;

    // 내부 상태
    private InputDevice rightController;
    private List<HighlightableObject> highlightableObjects = new List<HighlightableObject>();
    private bool isHighlighting = false;
    private Coroutine highlightCoroutine;
    private int usedCount = 0;
    private MoneyManager moneyManager;

    // ===== Inner Class =====

    private class HighlightableObject
    {
        public GameObject gameObject;
        public string tag;

        // 파리처럼 몸통/날개/눈 등 여러 개의 Renderer로 나뉜 모델도 전부 하이라이트되도록 배열로 보관
        public Renderer[] renderers;
        public Material[][] originalMaterials;
        public Material[][] instanceMaterials;

        // ColorTint용 — 머티리얼별 원래 색 프로퍼티/색상 보존 (_EMISSION 키워드 없이 _BaseColor/_Color 자체를 틴트)
        public string[][] tintProps;
        public Color[][] tintOrigColors;

        public DecalProjector decalProjector;
        public float originalFadeFactor;
    }

    // ===== Unity Lifecycle =====

    void Start()
    {
        moneyManager = FindFirstObjectByType<MoneyManager>();

        if (toolSwitcher == null)
            toolSwitcher = FindFirstObjectByType<ToolSwitcher>();

        InitializeRightController();
        FindAllCleanableTargets();
        SetupButtonListeners();
        UpdateRemainingUsesText();

        if (shopPanel != null)
            shopPanel.SetActive(false);

        if (insufficientFundsText != null)
            insufficientFundsText.gameObject.SetActive(false);

        if (shopMessageText != null)
            shopMessageText.gameObject.SetActive(true);

        if (showDebugInfo)
            Debug.Log($"[CleanTargetHighlighter] 초기화 완료! 청소 대상: {highlightableObjects.Count}개");
    }

    void Update()
    {
        if (!rightController.isValid)
            InitializeRightController();
    }

    void OnDestroy()
    {
        if (isHighlighting)
            RestoreTargetMaterials(highlightableObjects);
    }

    // ===== 초기화 =====

    void InitializeRightController()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(rightControllerCharacteristics, devices);
        if (devices.Count > 0) rightController = devices[0];
    }

    void SetupButtonListeners()
    {
        if (highlightButton != null)
            highlightButton.onClick.AddListener(OnHighlightButtonClicked);
        else
            Debug.LogWarning("[CleanTargetHighlighter] Highlight Button이 할당되지 않았습니다!");

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmPurchase);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelPurchase);
    }

    // ===== 탐색 =====

    void FindAllCleanableTargets()
    {
        highlightableObjects.Clear();

        foreach (string tag in cleanableObjectTags)
        {
            GameObject[] found;
            try { found = GameObject.FindGameObjectsWithTag(tag); }
            catch
            {
                if (showDebugInfo)
                    Debug.LogWarning($"[CleanTargetHighlighter] 태그 '{tag}'가 프로젝트에 없습니다.");
                continue;
            }

            foreach (GameObject obj in found)
            {
                DecalProjector dp = obj.GetComponent<DecalProjector>();
                if (dp != null)
                {
                    highlightableObjects.Add(new HighlightableObject
                    {
                        gameObject = obj,
                        tag = tag,
                        decalProjector = dp,
                        originalFadeFactor = dp.fadeFactor
                    });
                    continue;
                }

                // 몸통/날개/눈처럼 자식마다 Renderer가 나뉜 모델도 있으므로 전부 수집한다.
                Renderer[] rends = obj.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    Material[][] originals = new Material[rends.Length][];
                    for (int i = 0; i < rends.Length; i++)
                        originals[i] = rends[i].sharedMaterials;

                    highlightableObjects.Add(new HighlightableObject
                    {
                        gameObject = obj,
                        tag = tag,
                        renderers = rends,
                        originalMaterials = originals
                    });
                }
            }
        }

        if (showDebugInfo)
            Debug.Log($"[CleanTargetHighlighter] 탐색 완료 — 총 {highlightableObjects.Count}개");
    }

    // ===== 버튼 클릭 — 무료/유료 분기 =====

    void OnHighlightButtonClicked()
    {
        if (usedCount < freeUseCount)
        {
            // 무료 사용
            usedCount++;
            UpdateRemainingUsesText();
            RunHighlight();

            if (showDebugInfo)
                Debug.Log($"[CleanTargetHighlighter] 무료 사용 {usedCount}/{freeUseCount}");
        }
        else
        {
            // 무료 소진 → 구매 확인 UI 표시
            ShowShopPanel();
        }
    }

    // ===== 구매 확인 UI =====

    void ShowShopPanel()
    {
        if (shopPanel == null) return;

        if (shopMessageText != null)
            shopMessageText.text = $"하이라이트를 구매하시겠습니까?\n({highlightCost:N0}원)";

        if (insufficientFundsText != null)
            insufficientFundsText.gameObject.SetActive(false);

        if (shopMessageText != null)
            shopMessageText.gameObject.SetActive(true);

        shopPanel.SetActive(true);

        if (showDebugInfo)
            Debug.Log("[CleanTargetHighlighter] 구매 확인 UI 표시");
    }

    void OnConfirmPurchase()
    {
        if (moneyManager == null)
        {
            Debug.LogWarning("[CleanTargetHighlighter] MoneyManager를 찾을 수 없습니다!");
            return;
        }

        if (moneyManager.SpendMoney(highlightCost))
        {
            // 결제 성공
            shopPanel.SetActive(false);
            RunHighlight();

            if (showDebugInfo)
                Debug.Log($"[CleanTargetHighlighter] 구매 완료! -{highlightCost}원");
        }
        else
        {
            // 잔액 부족
            if (shopMessageText != null)
                shopMessageText.gameObject.SetActive(false);

            if (insufficientFundsText != null)
                insufficientFundsText.gameObject.SetActive(true);

            if (showDebugInfo)
                Debug.Log("[CleanTargetHighlighter] 잔액 부족!");
        }
    }

    void OnCancelPurchase()
    {
        if (shopPanel != null)
            shopPanel.SetActive(false);

        if (insufficientFundsText != null)
            insufficientFundsText.gameObject.SetActive(false);

        if (shopMessageText != null)
            shopMessageText.gameObject.SetActive(true);

        if (showDebugInfo)
            Debug.Log("[CleanTargetHighlighter] 구매 취소");
    }

    // ===== 남은 횟수 텍스트 갱신 =====

    void UpdateRemainingUsesText()
    {
        if (remainingUsesText == null) return;

        int remaining = Mathf.Max(0, freeUseCount - usedCount);
        remainingUsesText.text = remaining > 0
            ? $"무료 {remaining}회"
            : "유료";
    }

    // ===== 현재 도구 → 청소 태그 매핑 =====

    string[] GetCurrentToolTags()
    {
        if (toolSwitcher == null) return cleanableObjectTags;

        // 아무 도구도 들고 있지 않은 초기 상태 → 전체 하이라이트
        string tool = toolSwitcher.GetCurrentToolTag();
        if (tool == "none" || string.IsNullOrEmpty(tool)) return cleanableObjectTags;

        if (toolSwitcher.IsHandModeActive) return new[] { "Cleanable_Hand" };

        // 상점에서 유료 구매/업그레이드하면 도구 태그에 접미사가 붙는다("Mop1"→"Mop2", "Sponge"→"Spray" 등).
        // 정확히 일치(==)로 비교하면 업그레이드된 도구가 매핑에서 빠져 하이라이트 대상이 0개가 되므로 접두사(StartsWith)로 비교한다.
        if (tool.StartsWith("Sponge") || tool.StartsWith("Spray")) return new[] { "Cleanable_Sponge" };
        if (tool.StartsWith("Rag"))       return new[] { "Cleanable_Rag" };
        if (tool.StartsWith("TileBrush")) return new[] { "Cleanable_TileBrush" };
        if (tool.StartsWith("Mop"))       return new[] { "Cleanable_Mop" };
        if (tool.StartsWith("Broom") || tool.StartsWith("Vacuum")) return new[] { "Cleanable_Broom" };
        if (tool.StartsWith("Scraper"))   return new[] { "Cleanable_Scraper" };
        if (tool.StartsWith("Flapper"))   return new[] { "Cleanable_Flapper" };
        if (tool.StartsWith("ToiletBrush")) return new[] { "Cleanable_ToiletBrush" };
        if (tool.StartsWith("Duster"))    return new[] { "Cleanable_Duster" };

        return cleanableObjectTags;
    }

    // ===== 하이라이트 실행 =====

    void RunHighlight()
    {
        if (isHighlighting && highlightCoroutine != null)
        {
            StopCoroutine(highlightCoroutine);
            RestoreTargetMaterials(highlightableObjects);
        }

        highlightCoroutine = StartCoroutine(HighlightTargetsCoroutine());

        if (rightController.isValid)
            rightController.SendHapticImpulse(0, 0.4f, 0.15f);
    }

    IEnumerator HighlightTargetsCoroutine()
    {
        isHighlighting = true;

        // 현재 도구에 해당하는 대상만 필터링
        var currentTags = new HashSet<string>(GetCurrentToolTags());
        var targets = highlightableObjects.FindAll(h => h.gameObject != null && currentTags.Contains(h.tag));

        if (targets.Count == 0)
        {
            if (showDebugInfo)
                Debug.LogWarning("[CleanTargetHighlighter] 현재 도구에 맞는 청소 대상이 없습니다!");
            isHighlighting = false;
            yield break;
        }

        ApplyHighlightToTargets(targets);

        if (showDebugInfo)
            Debug.Log($"[CleanTargetHighlighter] {targets.Count}개 하이라이트 시작!");

        float elapsed = 0f;
        while (elapsed < highlightDuration)
        {
            if (useBlinking)
                UpdateBlinkIntensity(targets, Mathf.PingPong(Time.time * blinkSpeed, 1.0f));

            elapsed += Time.deltaTime;
            yield return null;
        }

        RestoreTargetMaterials(targets);
        isHighlighting = false;

        if (showDebugInfo)
            Debug.Log("[CleanTargetHighlighter] 하이라이트 종료");
    }

    // ===== 하이라이트 적용/복원 =====

    void ApplyHighlightToTargets(List<HighlightableObject> targets)
    {
        foreach (var h in targets)
        {
            if (h.renderers != null && h.renderers.Length > 0)
            {
                h.instanceMaterials = new Material[h.renderers.Length][];
                h.tintProps = new string[h.renderers.Length][];
                h.tintOrigColors = new Color[h.renderers.Length][];

                for (int r = 0; r < h.renderers.Length; r++)
                {
                    if (h.renderers[r] == null) continue;

                    Material[] originals = h.originalMaterials[r];
                    Material[] instances = new Material[originals.Length];
                    string[] props = new string[originals.Length];
                    Color[] origColors = new Color[originals.Length];

                    for (int i = 0; i < originals.Length; i++)
                    {
                        if (originals[i] == null) continue;
                        instances[i] = new Material(originals[i]);

                        string prop = instances[i].HasProperty("_BaseColor") ? "_BaseColor"
                                    : instances[i].HasProperty("_Color")     ? "_Color"
                                    : "";
                        props[i] = prop;
                        if (!string.IsNullOrEmpty(prop))
                        {
                            origColors[i] = instances[i].GetColor(prop);
                            Color tinted = highlightColor * emissionIntensity;
                            tinted.a = origColors[i].a; // 알파는 원래 값 유지 — 색상만 하이라이트
                            instances[i].SetColor(prop, tinted);
                        }

                        // 발자국처럼 텍스처 자체가 검정에 가까우면 _BaseColor 곱셈만으론 절대 밝아지지 않는다
                        // (검정 × 색 = 검정). 지원되는 머티리얼은 _EmissionColor(더하기 방식)도 같이 켠다.
                        if (instances[i].HasProperty("_EmissionColor"))
                        {
                            instances[i].EnableKeyword("_EMISSION");
                            instances[i].globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                        }
                    }
                    h.instanceMaterials[r] = instances;
                    h.tintProps[r] = props;
                    h.tintOrigColors[r] = origColors;
                    h.renderers[r].materials = instances;
                }
            }
            else if (h.decalProjector != null)
            {
                h.decalProjector.fadeFactor = 1.0f;
            }
        }
    }

    void UpdateBlinkIntensity(List<HighlightableObject> targets, float t)
    {
        foreach (var h in targets)
        {
            if (h.instanceMaterials != null)
            {
                Color boosted = highlightColor * emissionIntensity;

                for (int r = 0; r < h.instanceMaterials.Length; r++)
                {
                    var mats = h.instanceMaterials[r];
                    if (mats == null) continue;

                    for (int i = 0; i < mats.Length; i++)
                    {
                        var mat = mats[i];
                        if (mat == null) continue;

                        string prop = h.tintProps[r][i];
                        if (!string.IsNullOrEmpty(prop))
                        {
                            Color orig = h.tintOrigColors[r][i];
                            Color tinted = Color.Lerp(orig, boosted, t);
                            tinted.a = orig.a; // 알파는 원래 값 유지 — 색상만 하이라이트
                            mat.SetColor(prop, tinted);
                        }

                        if (mat.HasProperty("_EmissionColor"))
                            mat.SetColor("_EmissionColor", boosted * t);
                    }
                }
            }
            else if (h.decalProjector != null)
            {
                h.decalProjector.fadeFactor = Mathf.Lerp(0.4f, 1.0f, t);
            }
        }
    }

    void RestoreTargetMaterials(List<HighlightableObject> targets)
    {
        foreach (var h in targets)
        {
            if (h.renderers != null && h.renderers.Length > 0)
            {
                for (int r = 0; r < h.renderers.Length; r++)
                {
                    if (h.renderers[r] != null)
                        h.renderers[r].materials = h.originalMaterials[r];
                }

                if (h.instanceMaterials != null)
                {
                    foreach (var mats in h.instanceMaterials)
                    {
                        if (mats == null) continue;
                        foreach (var mat in mats)
                            if (mat != null) Destroy(mat);
                    }
                    h.instanceMaterials = null;
                }
            }
            else if (h.decalProjector != null)
            {
                h.decalProjector.fadeFactor = h.originalFadeFactor;
            }
        }
    }

    // ===== 외부 API =====

    public void RemoveCleanedTarget(GameObject cleanedObject)
    {
        int removed = highlightableObjects.RemoveAll(h => h.gameObject == cleanedObject);
        if (showDebugInfo && removed > 0)
            Debug.Log($"[CleanTargetHighlighter] 제거: {cleanedObject.name} (남은 대상: {highlightableObjects.Count}개)");
    }

    public void RefreshCleanableTargets()
    {
        if (isHighlighting)
        {
            StopCoroutine(highlightCoroutine);
            RestoreTargetMaterials(highlightableObjects);
            isHighlighting = false;
        }
        FindAllCleanableTargets();
    }

    public int GetRemainingTargetsCount() => highlightableObjects.Count;
}
