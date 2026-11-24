using UnityEngine;
using UnityEngine.XR;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

[System.Serializable]
public class PurchasableTool
{
    public string toolTag;
    public Button button;
    public Vector3 positionOffset;
    public Vector3 rotationOffset;
}

public class ToolSwitcher : MonoBehaviour
{
    [Header("UI 설정")]
    [Tooltip("도구 선택 메뉴 Canvas")]
    public GameObject menuCanvas;

    [Tooltip("하이라이트 구매 패널 (메뉴 토글 시 비활성화)")]
    public GameObject highlightShopPanel;

    [Tooltip("X버튼으로 열고 닫을 Pad UI 오브젝트")]
    public GameObject padUIObject;

    [Tooltip("Pad UI를 제어하는 UIPanelToggle. CloseAllUI 호출 시 상태 동기화에 사용")]
    public UIPanelToggle padPanelToggle;

    [Tooltip("ToolSwitcher 메뉴나 Pad UI가 켜져 있는 동안 오른손 인터랙터를 Far(레이) 모드로 고정.\n" +
             "도구 콜라이더 때문에 Near 모드로 빠져 레이저가 사라지는 문제 방지.")]
    public bool forceFarRayWhileUIOpen = true;

    [Header("메뉴 위치 설정")]
    [Tooltip("카메라로부터의 거리")]
    public float menuDistance = 1.0f;

    [Tooltip("메뉴 수직 오프셋 (음수 = 아래)")]
    public float menuVerticalOffset = -0.3f;

    [Tooltip("메뉴 좌우 오프셋")]
    public float menuHorizontalOffset = 0f;

    [Header("도구 버튼들")]
    [Tooltip("각 버튼에 연결될 도구 태그 (버튼 순서와 동일하게)")]
    public string[] toolTags = new string[] { "Sponge", "Mop", "Broom" };

    [Header("도구 회전 오프셋")]
    [Tooltip("각 도구별 회전 오프셋 (X, Y, Z) - toolTags 순서와 동일")]
    public Vector3[] toolRotationOffsets = new Vector3[]
    {
        Vector3.zero,                 // Sponge
        new Vector3(0f, -106f, 60f),     // Mop (예시: X축 90도)
        new Vector3(0f, -106f, 60f)                  // Broom
    };

    [Header("도구 위치 오프셋 (선택사항)")]
    [Tooltip("각 도구별 위치 오프셋 (X, Y, Z) - 필요시 사용")]
    public Vector3[] toolPositionOffsets = new Vector3[]
    {
        Vector3.zero,   // Sponge
        Vector3.zero,   // Mop
        Vector3.zero    // Broom
    };

    [Header("도구 이름 UI")]
    public ToolNameUIFollow toolNameUI;

    [Tooltip("UI 버튼들 (Inspector에서 순서대로 할당)")]
    public Button[] toolButtons;

    [Tooltip("손 비우기 버튼 (Hand 버튼)")]
    public Button handButton;

    [Tooltip("잠금 시에도 항상 클릭 가능하게 유지할 버튼 (Pad UI 조작법 버튼 등)")]
    public Button[] alwaysInteractableButtons;

    [Header("Pad UI 버튼")]
    [Tooltip("Pad UI에 있는 모든 버튼. LockAllPadButtons / UnlockPadDescriptionOnly 제어에 사용됨")]
    public Button[] padButtons;

    [Tooltip("튜토리얼에서 버튼 안내 나레이션 후 별도로 활성화할 청소완료 버튼")]
    public Button cleaningCompleteButton;

    [Header("구매 잠금 도구들 (상점에서 구매 후 활성화)")]
    [Tooltip("처음엔 잠겨있다가 구매 시 해금되는 도구들")]
    public PurchasableTool[] purchasableTools;

    [Header("컨트롤러 설정")]
    [Tooltip("오른손 컨트롤러 Transform")]
    public Transform rightHandController;

    [Header("튜토리얼 잠금")]
    [Tooltip("true면 씬 시작 시 도구 버튼을 비활성화 상태로 시작 (메뉴는 열리지만 버튼 클릭 불가)")]
    public bool startWithButtonsLocked = false;

    // TutorialDemoController가 자동 제어 — Inspector에서 직접 수정 불필요
    [HideInInspector] public bool demoMode = false;

    [Header("디버그")]
    [Tooltip("콘솔에 디버그 정보 표시")]
    public bool showDebugInfo = false;

    private InputDevice rightController;
    private bool aButtonPressedLastFrame = false;
    private GameObject[] toolObjects;
    private GameObject currentActiveTool = null;
    private int currentToolIndex = -1; // 현재 도구 인덱스 저장
    private bool isMenuActive = false;
    private RectTransform canvasRect;
    private NearFarInteractor rightNearFarInteractor;
    public NearFarInteractor RightNearFarInteractor => rightNearFarInteractor;
    private XRInteractorLineVisual rightLineVisual;

    /// <summary>Hand 버튼을 눌러 빈 손 모드가 활성화된 경우 true</summary>
    public bool IsHandModeActive { get; private set; } = false;

    /// <summary>도구가 처음 선택될 때 발생. (toolIndex, toolTag) 전달</summary>
    public event System.Action<int, string> OnToolSelected;


    void Start()
    {
        InitializeControllers();

        if (rightHandController == null)
        {
            rightHandController = FindRightHandController();
        }

        if (rightHandController == null)
        {
            Debug.LogError("오른손 컨트롤러를 찾을 수 없습니다!");
            return;
        }

        if (menuCanvas != null)
        {
            canvasRect = menuCanvas.GetComponent<RectTransform>();

            Canvas canvas = menuCanvas.GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
            {
                Debug.LogWarning("Canvas의 Render Mode를 World Space로 변경하세요!");
            }

            // XRI 기반 UI Ray 입력은 TrackedDeviceGraphicRaycaster가 필요합니다.
            if (menuCanvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            {
                menuCanvas.AddComponent<TrackedDeviceGraphicRaycaster>();
                if (showDebugInfo)
                    Debug.Log("TrackedDeviceGraphicRaycaster 자동 추가됨");
            }

            if (menuCanvas.GetComponent<GraphicRaycaster>() == null)
            {
                menuCanvas.AddComponent<GraphicRaycaster>();
                if (showDebugInfo)
                    Debug.Log("GraphicRaycaster 자동 추가됨");
            }

            menuCanvas.SetActive(false);
        }
        else
        {
            Debug.LogError("Menu Canvas가 할당되지 않았습니다!");
        }

        EnsureEventSystem();
        EnsurePadUIRaycaster();
        FindAndCacheTools();
        SetupButtonListeners();
        ValidateXrUiSetup();
        rightNearFarInteractor = rightHandController.GetComponentInChildren<NearFarInteractor>(true);
        rightLineVisual = rightHandController.GetComponentInChildren<XRInteractorLineVisual>(true);

        if (showDebugInfo)
        {
            Debug.Log("도구 전환 시스템 초기화 완료!");
            Debug.Log($"도구 목록: {string.Join(", ", toolTags)}");
        }

        // 구매 잠금 도구 버튼 초기 비활성화
        if (purchasableTools != null)
            foreach (var pt in purchasableTools)
                if (pt.button != null) pt.button.gameObject.SetActive(false);

        // 초기 텍스트 설정
        if (toolNameUI != null)
        {
            toolNameUI.SetText("Current Tool: None");
        }

        // 배열 크기 검증
        ValidateArraySizes();

        if (startWithButtonsLocked)
            SetButtonsInteractable(false);
    }

    void ValidateXrUiSetup()
    {
        if (!showDebugInfo)
            return;

        var eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            Debug.LogError("EventSystem이 없습니다. UI 선택이 동작하지 않을 수 있습니다.", this);
            return;
        }

        var xrUi = eventSystem.GetComponent<XRUIInputModule>();
        if (xrUi == null)
            Debug.LogWarning("XRUIInputModule이 없습니다. 컨트롤러/손 UI 클릭이 안 될 수 있습니다.", eventSystem);

#if ENABLE_INPUT_SYSTEM
        var inputSystemUi = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputSystemUi == null)
            Debug.LogWarning("InputSystemUIInputModule이 없습니다. UI 입력이 안 될 수 있습니다.", eventSystem);
#endif

        if (menuCanvas != null)
        {
            if (menuCanvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                Debug.LogWarning("menuCanvas에 TrackedDeviceGraphicRaycaster가 없습니다. XR Ray UI 클릭이 안 될 수 있습니다.", menuCanvas);
        }
    }

    void ValidateArraySizes()
    {
        // 회전 오프셋 배열 크기 확인
        if (toolRotationOffsets.Length != toolTags.Length)
        {
            Debug.LogWarning($"toolRotationOffsets 배열 크기가 toolTags와 다릅니다! ({toolRotationOffsets.Length} vs {toolTags.Length})");
            System.Array.Resize(ref toolRotationOffsets, toolTags.Length);
        }

        // 위치 오프셋 배열 크기 확인
        if (toolPositionOffsets.Length != toolTags.Length)
        {
            Debug.LogWarning($"toolPositionOffsets 배열 크기가 toolTags와 다릅니다! ({toolPositionOffsets.Length} vs {toolTags.Length})");
            System.Array.Resize(ref toolPositionOffsets, toolTags.Length);
        }
    }

    void EnsurePadUIRaycaster()
    {
        if (padUIObject == null)
        {
            Debug.LogWarning("[ToolSwitcher] padUIObject가 null — Inspector에서 Pad UI Object 필드를 할당해야 합니다!");
            return;
        }

        Canvas canvas = padUIObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = padUIObject.GetComponentInChildren<Canvas>(true);
        if (canvas == null)
            canvas = padUIObject.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[ToolSwitcher] padUIObject에서 Canvas를 찾을 수 없습니다! Inspector에서 padUIObject를 Canvas가 붙어 있는 오브젝트로 직접 지정하거나, TrackedDeviceGraphicRaycaster를 Canvas에 수동으로 추가하세요.");
            return;
        }

        bool hasRaycaster = canvas.gameObject.GetComponent<TrackedDeviceGraphicRaycaster>() != null;
        if (!hasRaycaster)
        {
            canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            if (showDebugInfo)
                Debug.Log("[ToolSwitcher] PadUI에 TrackedDeviceGraphicRaycaster 자동 추가됨");
        }
        else
        {
            if (showDebugInfo)
                Debug.Log("[ToolSwitcher] PadUI TrackedDeviceGraphicRaycaster 이미 있음 — OK");
        }

        // menuCanvas도 동일하게 확인 및 자동 추가
        if (menuCanvas != null)
        {
            if (menuCanvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
            {
                menuCanvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
                if (showDebugInfo)
                    Debug.Log("[ToolSwitcher] menuCanvas에 TrackedDeviceGraphicRaycaster 자동 추가됨");
            }
        }

        // EventSystem 확인
        var es = Object.FindFirstObjectByType<EventSystem>();
        bool hasXRUI = es != null && es.GetComponent<XRUIInputModule>() != null;
        if (showDebugInfo)
            Debug.Log($"[ToolSwitcher] EventSystem={es?.name ?? "없음"} | XRUIInputModule={hasXRUI}");
    }

    void EnsureEventSystem()
    {
        var eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            var eventSystemObj = new GameObject("EventSystem");
            eventSystem = eventSystemObj.AddComponent<EventSystem>();

            // XRI 프로젝트에서는 StandaloneInputModule이 아니라 XRUIInputModule(그리고 Input System 기반 UI 모듈)이 필요합니다.
            if (eventSystemObj.GetComponent<XRUIInputModule>() == null)
                eventSystemObj.AddComponent<XRUIInputModule>();

#if ENABLE_INPUT_SYSTEM
            if (eventSystemObj.GetComponent<InputSystemUIInputModule>() == null)
                eventSystemObj.AddComponent<InputSystemUIInputModule>();
#endif

            if (showDebugInfo)
                Debug.Log("EventSystem 자동 생성됨 (XRUIInputModule)");

            return;
        }

        // 기존 EventSystem에 StandaloneInputModule이 남아있으면 XR UI 클릭을 방해할 수 있습니다.
        var standalone = eventSystem.GetComponent<StandaloneInputModule>();
        if (standalone != null && standalone.enabled)
        {
            standalone.enabled = false;
            if (showDebugInfo)
                Debug.LogWarning("StandaloneInputModule 비활성화 (XR UI 입력을 위해).");
        }

        if (eventSystem.GetComponent<XRUIInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<XRUIInputModule>();
            if (showDebugInfo)
                Debug.Log("XRUIInputModule 자동 추가됨");
        }

#if ENABLE_INPUT_SYSTEM
        if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            if (showDebugInfo)
                Debug.Log("InputSystemUIInputModule 자동 추가됨");
        }
#endif
    }

    void InitializeControllers()
    {
        var rightChars = InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;
        var devices = new System.Collections.Generic.List<InputDevice>();

        InputDevices.GetDevicesWithCharacteristics(rightChars, devices);
        if (devices.Count > 0)
        {
            rightController = devices[0];
            if (showDebugInfo)
                Debug.Log("오른손 컨트롤러 입력 연결됨: " + rightController.name);
        }
    }

    Transform FindRightHandController()
    {
        GameObject xrOrigin = GameObject.Find("XR Origin");
        if (xrOrigin != null)
        {
            Transform cameraOffset = xrOrigin.transform.Find("Camera Offset");
            if (cameraOffset != null)
            {
                Transform rightHand = cameraOffset.Find("RightHand Controller") ??
                                      cameraOffset.Find("Right Hand Controller") ??
                                      cameraOffset.Find("Right Controller");
                if (rightHand != null)
                {
                    if (showDebugInfo)
                        Debug.Log("오른손 컨트롤러 자동 발견: " + rightHand.name);
                    return rightHand;
                }
            }
        }
        return null;
    }

    void FindAndCacheTools()
    {
        toolObjects = new GameObject[toolTags.Length];

        for (int i = 0; i < toolTags.Length; i++)
        {
            // 비활성화된 오브젝트도 찾기
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                if (obj.CompareTag(toolTags[i]) && obj.scene.isLoaded)
                {
                    toolObjects[i] = obj;
                    toolObjects[i].SetActive(false);

                    if (showDebugInfo)
                        Debug.Log($"도구 발견: {toolTags[i]} → {obj.name}");
                    break;
                }
            }

            if (toolObjects[i] == null)
                Debug.LogWarning($"'{toolTags[i]}' 태그를 가진 오브젝트를 찾을 수 없습니다!");
        }
    }

    void SetupButtonListeners()
    {
        if (toolButtons != null)
        {
            for (int i = 0; i < toolButtons.Length; i++)
            {
                if (toolButtons[i] != null && i < toolTags.Length)
                {
                    int index = i;
                    toolButtons[i].onClick.AddListener(() => OnToolButtonClicked(index));

                    if (showDebugInfo)
                        Debug.Log($"버튼 이벤트 연결: {toolButtons[i].name} → {toolTags[index]}");
                }
            }
        }
        else
        {
            Debug.LogWarning("Tool Buttons 배열이 비어있습니다!");
        }

        if (handButton != null)
        {
            handButton.onClick.AddListener(OnHandButtonClicked);
            if (showDebugInfo)
                Debug.Log("Hand 버튼 이벤트 연결됨");
        }
        else
        {
            Debug.LogWarning("Hand Button이 할당되지 않았습니다!");
        }
    }

    void Update()
    {
        if (demoMode) return;

        if (!rightController.isValid) InitializeControllers();
        CheckButtonInput();

        if (isMenuActive && menuCanvas != null) UpdateMenuPosition();
    }

    void LateUpdate()
    {
        if (rightLineVisual != null)
            rightLineVisual.enabled = true;
        if (rightNearFarInteractor != null)
            rightNearFarInteractor.enabled = true;

        UpdateFarRayForUI();

        LiveApplyToolOffsets();
    }

    // ToolSwitcher 메뉴 / Pad UI가 켜져 있는 동안 Near 캐스팅을 꺼서 Far 레이가 항상 나가게 한다.
    // (도구 콜라이더가 인터랙터 근접 영역에 들어가면 NearFarInteractor가 Near 모드로 전환돼 레이저가 사라지는 문제)
    private bool nearCastingSuppressed = false;

    void UpdateFarRayForUI()
    {
        if (rightNearFarInteractor == null) return;

        bool wantForceFar = forceFarRayWhileUIOpen && IsPointerUIOpen();

        if (wantForceFar && !nearCastingSuppressed)
        {
            rightNearFarInteractor.enableNearCasting = false;
            nearCastingSuppressed = true;
        }
        else if (!wantForceFar && nearCastingSuppressed)
        {
            rightNearFarInteractor.enableNearCasting = true;
            nearCastingSuppressed = false;
        }
    }

    // 오른손 레이로 가리키는 UI(도구 메뉴 / Pad UI)가 하나라도 열려 있는지
    bool IsPointerUIOpen()
    {
        if (isMenuActive) return true;
        if (padPanelToggle != null) return padPanelToggle.IsOpen;
        if (padUIObject != null) return padUIObject.activeInHierarchy;
        return false;
    }

    void OnDisable()
    {
        // 메뉴가 열린 채 ToolSwitcher가 비활성화되는 경우(예: 랭크 UI 표시) Near 캐스팅을 되돌려 놓는다.
        if (nearCastingSuppressed && rightNearFarInteractor != null)
        {
            rightNearFarInteractor.enableNearCasting = true;
            nearCastingSuppressed = false;
        }
    }

    void LiveApplyToolOffsets()
    {
        if (currentActiveTool == null || currentToolIndex < 0) return;

        // 데모 중이거나 도구가 컨트롤러에서 분리된 경우(데모 앵커에 붙어있는 경우) 적용 안 함
        if (demoMode) return;
        if (currentActiveTool.transform.parent != rightHandController) return;

        // Purchasable 도구면 PurchasableTool의 현재 Inspector 값으로 배열 동기화
        if (purchasableTools != null && currentToolIndex < toolTags.Length)
        {
            string currentTag = toolTags[currentToolIndex];
            foreach (var pt in purchasableTools)
            {
                if (pt.toolTag == currentTag)
                {
                    toolPositionOffsets[currentToolIndex] = pt.positionOffset;
                    toolRotationOffsets[currentToolIndex] = pt.rotationOffset;
                    break;
                }
            }
        }

        if (currentToolIndex < toolPositionOffsets.Length)
            currentActiveTool.transform.localPosition = toolPositionOffsets[currentToolIndex];
        if (currentToolIndex < toolRotationOffsets.Length)
            currentActiveTool.transform.localRotation = Quaternion.Euler(toolRotationOffsets[currentToolIndex]);
    }

    void CheckButtonInput()
    {
        // 오른쪽 A 버튼 → 도구 선택 메뉴 토글
        if (rightController.isValid &&
            rightController.TryGetFeatureValue(CommonUsages.primaryButton, out bool aPressed))
        {
            if (aPressed && !aButtonPressedLastFrame) ToggleMenu();
            aButtonPressedLastFrame = aPressed;
        }
    }

    public void UnlockButtons()
    {
        SetButtonsInteractable(true);
        if (showDebugInfo) Debug.Log("[ToolSwitcher] 도구 버튼 잠금 해제됨");
    }

    public void ForceShowRay()
    {
        if (rightLineVisual != null)
            rightLineVisual.enabled = true;
        if (rightNearFarInteractor != null)
            rightNearFarInteractor.enabled = true;
    }

    void SetButtonsInteractable(bool interactable)
    {
        if (toolButtons != null)
            foreach (var btn in toolButtons)
                if (btn != null && !IsAlwaysInteractable(btn))
                    btn.interactable = interactable;
        if (handButton != null && !IsAlwaysInteractable(handButton))
            handButton.interactable = interactable;
    }

    bool IsAlwaysInteractable(Button btn)
    {
        if (alwaysInteractableButtons == null) return false;
        foreach (var b in alwaysInteractableButtons)
            if (b == btn) return true;
        return false;
    }

    // 데모 시작 시 열려 있는 UI를 모두 닫는다
    public void CloseAllUI()
    {
        if (menuCanvas != null && menuCanvas.activeSelf)
        {
            menuCanvas.SetActive(false);
            isMenuActive = false;
        }
        // ClosePanel()을 통해 닫아야 UIPanelToggle의 isPanelOpen 상태가 동기화됨
        padPanelToggle?.ClosePanel();
    }

    // 패드 UI의 모든 버튼을 비활성화 (클릭 불가)
    public void LockAllPadButtons()
    {
        if (padButtons == null) return;
        foreach (var btn in padButtons)
            if (btn != null) btn.interactable = false;
    }

    // 청소완료 버튼만 활성화 (튜토리얼 버튼 안내 나레이션 후 호출)
    public void UnlockCleaningCompleteButton()
    {
        if (cleaningCompleteButton == null)
        {
            Debug.LogWarning("[ToolSwitcher] cleaningCompleteButton이 연결되지 않았습니다!");
            return;
        }
        cleaningCompleteButton.gameObject.SetActive(true);
        cleaningCompleteButton.interactable = true;
        if (showDebugInfo)
            Debug.Log("[ToolSwitcher] 청소완료 버튼 활성화됨");
    }

    // 패드 UI에서 alwaysInteractableButtons만 활성화 (조작법/설명 버튼만 누를 수 있음)
    public void UnlockPadDescriptionOnly()
    {
        if (padButtons == null) return;
        foreach (var btn in padButtons)
            if (btn != null) btn.interactable = IsAlwaysInteractable(btn);
    }

    [Header("따라하기 도구 제한 표시")]
    [Tooltip("따라하기 중 선택 불가한 도구 버튼의 disabled 색상 (회색끼)")]
    public Color lockedToolButtonColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);

    private readonly Dictionary<Button, ColorBlock> _origToolButtonColors = new Dictionary<Button, ColorBlock>();

    /// <summary>
    /// allowedTag의 도구 버튼만 선택 가능하게 하고 나머지는 비활성(+회색) 처리.
    /// allowedTag가 null/빈 문자열이면 제한을 해제하고 원래 상태로 되돌린다.
    /// </summary>
    public void RestrictToolSelection(string allowedTag)
    {
        if (toolButtons == null) return;

        bool restrict = !string.IsNullOrEmpty(allowedTag);

        for (int i = 0; i < toolButtons.Length; i++)
        {
            var btn = toolButtons[i];
            if (btn == null || i >= toolTags.Length) continue;
            if (IsAlwaysInteractable(btn)) continue;

            if (restrict)
            {
                // 태그에 번호가 붙는 경우 대응 (예: "Mop" → "Mop1"/"Mop2")
                bool allowed = toolTags[i] == allowedTag
                            || toolTags[i].StartsWith(allowedTag);
                if (!allowed)
                {
                    if (!_origToolButtonColors.ContainsKey(btn))
                        _origToolButtonColors[btn] = btn.colors;

                    ColorBlock cb = btn.colors;
                    cb.disabledColor = lockedToolButtonColor;
                    btn.colors = cb;
                }
                btn.interactable = allowed;
            }
            else
            {
                if (_origToolButtonColors.TryGetValue(btn, out ColorBlock orig))
                    btn.colors = orig;
                btn.interactable = true;
            }
        }

        if (!restrict)
            _origToolButtonColors.Clear();

        if (showDebugInfo)
            Debug.Log($"[ToolSwitcher] 도구 선택 제한: {(restrict ? allowedTag : "해제")}");
    }

    void ToggleMenu()
    {
        if (menuCanvas == null) return;

        isMenuActive = !isMenuActive;
        menuCanvas.SetActive(isMenuActive);

        if (highlightShopPanel != null)
            highlightShopPanel.SetActive(false);

        if (isMenuActive)
            UpdateMenuPosition();

        if (rightController.isValid)
            rightController.SendHapticImpulse(0, 0.3f, 0.1f);

        if (showDebugInfo)
            Debug.Log(isMenuActive ? "메뉴 열림" : "메뉴 닫힘");
    }

    void UpdateMenuPosition()
    {
        if (menuCanvas == null) return;
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        Vector3 menuPosition = mainCamera.transform.position
            + mainCamera.transform.forward * menuDistance
            + mainCamera.transform.up * menuVerticalOffset
            + mainCamera.transform.right * menuHorizontalOffset;

        menuCanvas.transform.position = menuPosition;

        Vector3 directionToCamera = mainCamera.transform.position - menuCanvas.transform.position;
        Quaternion baseRotation = Quaternion.LookRotation(directionToCamera);
        menuCanvas.transform.rotation = baseRotation * Quaternion.Euler(0f, 180f, 0f);
    }

    void OnToolButtonClicked(int toolIndex)
    {
        if (toolIndex < 0 || toolIndex >= toolObjects.Length) return;

        if (showDebugInfo)
            Debug.Log($"[ToolSwitcher] 버튼 클릭: index={toolIndex}, tag={toolTags[toolIndex]}", this);

        CloseMenu();
        HideCurrentTool();
        IsHandModeActive = false;
        ShowAndAttachTool(toolIndex);

        // 스프레이 연동
        SprayCleaner sprayCleaner = null;
        SprayCleaner[] all = Resources.FindObjectsOfTypeAll<SprayCleaner>();
        foreach (SprayCleaner sc in all)
        {
            if (sc.gameObject.scene.isLoaded) { sprayCleaner = sc; break; }
        }

        if (sprayCleaner != null)
        {
            // Sponge 버튼 클릭 시 (인덱스 0)
            if (toolIndex == 0)
            {
                ShopManager shopManager = FindFirstObjectByType<ShopManager>();
                bool purchased = shopManager != null && shopManager.IsSprayPurchased();
                if (showDebugInfo)
                    Debug.Log($"[ToolSwitcher] SprayCleaner 발견: {sprayCleaner.gameObject.name} | shopManager: {(shopManager != null ? "있음" : "없음")} | IsSprayPurchased: {purchased}");
                if (purchased)
                    sprayCleaner.ShowSpray();
                else
                    sprayCleaner.HideSpray();
            }
            else
                sprayCleaner.HideSpray();
        }
        else
        {
            Debug.LogWarning("[ToolSwitcher] SprayCleaner를 찾지 못했습니다!");
        }

        if (rightController.isValid)
            rightController.SendHapticImpulse(0, 0.5f, 0.2f);

        if (toolNameUI != null)
            toolNameUI.SetText("Current Tool: " + toolTags[toolIndex]);
    }

    void OnHandButtonClicked()
    {
        CloseMenu();
        HideCurrentTool();
        IsHandModeActive = true;

        SprayCleaner sprayCleaner = null;
        SprayCleaner[] all = Resources.FindObjectsOfTypeAll<SprayCleaner>();
        foreach (SprayCleaner sc in all)
        {
            if (sc.gameObject.scene.isLoaded) { sprayCleaner = sc; break; }
        }
        if (sprayCleaner != null)
            sprayCleaner.HideSpray();

        if (rightController.isValid)
            rightController.SendHapticImpulse(0, 0.3f, 0.15f);

        if (toolNameUI != null)
            toolNameUI.SetText("Current Tool: Hand");
    }

    void CloseMenu()
    {
        if (menuCanvas != null)
        {
            menuCanvas.SetActive(false);
            isMenuActive = false;

            if (showDebugInfo)
                Debug.Log("메뉴 닫힘 (버튼 클릭)");
        }
    }

    void HideCurrentTool()
    {
        if (currentActiveTool != null)
        {
            // 도구 장착 해제 시 인터랙터블·콜라이더 재활성화
            var xrInteractable = currentActiveTool.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
            if (xrInteractable != null)
                xrInteractable.enabled = true;
            foreach (var col in currentActiveTool.GetComponentsInChildren<Collider>())
                col.enabled = true;

            currentActiveTool.SetActive(false);

            if (showDebugInfo)
                Debug.Log($"도구 숨김: {currentActiveTool.name}");

            currentActiveTool = null;
            currentToolIndex = -1;
        }
    }

    void ShowAndAttachTool(int toolIndex)
    {
        if (toolIndex < 0 || toolIndex >= toolObjects.Length) return;

        GameObject toolToShow = toolObjects[toolIndex];
        if (toolToShow == null)
        {
            Debug.LogWarning($"도구 오브젝트가 없습니다: {toolTags[toolIndex]}");
            return;
        }

        toolToShow.SetActive(true);
        currentActiveTool = toolToShow;
        currentToolIndex = toolIndex;

        // 장착된 도구를 XRI가 인터랙터블/콜라이더로 인식하지 않도록 비활성화
        // (콜라이더가 살아있으면 레이가 도구에 맞아 빨간색으로 표시됨)
        var xrInteractable = toolToShow.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        if (xrInteractable != null)
            xrInteractable.enabled = false;
        foreach (var col in toolToShow.GetComponentsInChildren<Collider>())
            col.enabled = false;

        // 도구를 활성화한 직후, 딱 한 번만 자식으로 만들고 오프셋을 적용합니다.
        AttachToolToController();

        Rigidbody rb = toolToShow.GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = true;

        if (showDebugInfo)
            Debug.Log($"도구 장착: {toolTags[toolIndex]} ({toolToShow.name}) - 회전: {toolRotationOffsets[toolIndex]}");

        string tag = toolIndex < toolTags.Length ? toolTags[toolIndex] : "";
        OnToolSelected?.Invoke(toolIndex, tag);
    }

    // FirstUseDemoTrigger의 첫 사용 데모가 끝난 뒤 장착 중이던 도구를 다시 손에 붙이기 위해 공개.
    public void AttachToolToController()
    {
        if (currentActiveTool == null || rightHandController == null) return;

        if (currentActiveTool.transform.parent != rightHandController)
        {
            // SetParent 기본값(worldPositionStays=true)은 부모 스케일/회전 영향으로 로컬 스케일이 재계산되어
            // "스케일 값이 날아간 것처럼" 보일 수 있습니다. 로컬 트랜스폼 유지(false)로 붙입니다.
            currentActiveTool.transform.SetParent(rightHandController, false);
        }

        // 위치 오프셋 적용
        if (currentToolIndex >= 0 && currentToolIndex < toolPositionOffsets.Length)
        {
            currentActiveTool.transform.localPosition = toolPositionOffsets[currentToolIndex];
        }
        else
        {
            currentActiveTool.transform.localPosition = Vector3.zero;
        }

        // 회전 오프셋 적용 (Quaternion.identity 대신)
        if (currentToolIndex >= 0 && currentToolIndex < toolRotationOffsets.Length)
        {
            currentActiveTool.transform.localRotation = Quaternion.Euler(toolRotationOffsets[currentToolIndex]);
        }
        else
        {
            currentActiveTool.transform.localRotation = Quaternion.identity;
        }
    }

    string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
        return path;
    }

    /// <summary>
    /// allowedTags에 없는 도구 버튼을 숨깁니다.
    /// allowedTags가 비어있으면 모든 버튼을 그대로 유지합니다.
    /// </summary>
    public void ShowOnlyTools(System.Collections.Generic.IEnumerable<string> allowedTags)
    {
        var allowed = new System.Collections.Generic.HashSet<string>(allowedTags);
        if (allowed.Count == 0) return;

        for (int i = 0; i < toolButtons.Length; i++)
        {
            if (toolButtons[i] == null || i >= toolTags.Length) continue;
            toolButtons[i].gameObject.SetActive(allowed.Contains(toolTags[i]));
        }

        if (showDebugInfo)
            Debug.Log($"[ToolSwitcher] 허용 도구: [{string.Join(", ", allowed)}]");
    }

    public void UnlockTool(string tag)
    {
        if (purchasableTools == null) return;

        PurchasableTool pt = System.Array.Find(purchasableTools, t => t.toolTag == tag);
        if (pt == null)
        {
            Debug.LogWarning($"[ToolSwitcher] '{tag}' 은 purchasableTools에 없습니다!");
            return;
        }
        if (pt.button == null || pt.button.gameObject.activeSelf) return;

        pt.button.gameObject.SetActive(true);

        int newIndex = toolTags.Length;
        System.Array.Resize(ref toolTags, newIndex + 1);
        toolTags[newIndex] = pt.toolTag;

        System.Array.Resize(ref toolButtons, newIndex + 1);
        toolButtons[newIndex] = pt.button;

        System.Array.Resize(ref toolRotationOffsets, newIndex + 1);
        toolRotationOffsets[newIndex] = pt.rotationOffset;

        System.Array.Resize(ref toolPositionOffsets, newIndex + 1);
        toolPositionOffsets[newIndex] = pt.positionOffset;

        int index = newIndex;
        pt.button.onClick.AddListener(() => OnToolButtonClicked(index));

        RefreshToolCache();

        if (showDebugInfo)
            Debug.Log($"[ToolSwitcher] '{pt.toolTag}' 잠금 해제됨!");
    }

    // 하위 호환성 유지
    public void UnlockFlapper() => UnlockTool("Flapper");

    public GameObject GetCurrentToolObject() => currentActiveTool;

    public string GetCurrentToolTag()
    {
        if (currentActiveTool == null) return "none";

        for (int i = 0; i < toolObjects.Length; i++)
            if (toolObjects[i] == currentActiveTool && i < toolTags.Length)
                return toolTags[i];

        return "none";
    }

    public int GetCurrentToolIndex() => currentToolIndex;

    public void RefreshToolCache()
    {
        toolObjects = new GameObject[toolTags.Length];

        for (int i = 0; i < toolTags.Length; i++)
        {
            // 비활성화된 오브젝트도 찾기
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                if (obj.CompareTag(toolTags[i]) && obj.scene.isLoaded)
                {
                    toolObjects[i] = obj;
                    toolObjects[i].SetActive(false);

                    if (showDebugInfo)
                        Debug.Log($"도구 재캐싱: {toolTags[i]} → {obj.name}");
                    break;
                }
            }

            if (toolObjects[i] == null)
                Debug.LogWarning($"'{toolTags[i]}' 태그 오브젝트 없음!");
        }
    }
}
