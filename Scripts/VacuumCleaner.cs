using UnityEngine;
using UnityEngine.XR;

public class VacuumCleaner : MonoBehaviour
{
    [Header("청占쌀깍옙 占쏙옙占쏙옙占쏙옙트")]
    public GameObject vacuumObject;

    [Header("청占쏙옙 占쏙옙占쏙옙")]
    [Tooltip("占쏙옙 占쏙옙 占쏙옙占쏙옙 占쏙옙占쏙옙占쏙옙 占쏙옙占쏙옙占쏙옙占쏙옙占쏙옙 占쏙옙占쏙옙 (0~1)")]
    public float cleanPercentage = 1.0f;

    [Tooltip("占쏙옙占쏙옙占쏙옙 청占쏙옙占싹댐옙 占쏙옙 占십울옙占쏙옙 횟占쏙옙")]
    public int totalCleanCount = 1;

    [Header("占쏙옙占쏙옙캐占쏙옙트 占쏙옙占쏙옙")]
    [Tooltip("청占쌀기가 占쏙옙占쏙옙 占쏙옙 占쌍댐옙 占쌍댐옙 占신몌옙")]
    public float reachDistance = 1.2f;

    [Header("占쏙옙트占싼뤄옙 占쏙옙占쏙옙")]
    [Tooltip("占쏙옙占쏙옙占쏙옙 占쏙옙트占싼뤄옙 Transform")]
    public Transform rightHandController;
    public InputDeviceCharacteristics controllerCharacteristics =
        InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;

    [Header("占쏙옙占?占쏙옙占쏙옙")]
    [Tooltip("트占쏙옙占신몌옙 占쏢마놂옙 占쏙옙占쏙옙占쏙옙 '占쏙옙占쏙옙' 占쏙옙占쏙옙占쏙옙 占쏙옙占쏙옙占쏙옙占쏙옙 (0~1)")]
    public float triggerThreshold = 0.7f;

    [Tooltip("占쌓몌옙占쏙옙 占쏢마놂옙 占쏙옙占쏙옙占쏙옙 '占쏙옙占쏙옙' 占쏙옙占쏙옙占쏙옙 占쏙옙占쏙옙占쏙옙占쏙옙 (0~1)")]
    public float gripThreshold = 0.7f;

    [Header("占싻깍옙 占쏙옙占쏙옙 占쏙옙占쏙옙 (占쌌듸옙 占쏙옙占?")]
    [Tooltip("占쏙옙/占쌘뤄옙 占쏙옙占쏙옙占싱댐옙 占쌈듸옙 占쌈계값 (m/s)")]
    public float pushVelocityThreshold = 0.35f;

    [Tooltip("占싻깍옙 占쏙옙占쏙옙占쏙옙 占쌕뀐옙占?占쏙옙 占십울옙占쏙옙 占쌍쇽옙 占시곤옙 (占쏙옙)")]
    public float directionChangeDelay = 0.2f;

    [Header("ToolSwitcher 占쏙옙占쏙옙")]
    [Tooltip("ToolSwitcher 占쏙옙크占쏙옙트")]
    public ToolSwitcher toolSwitcher;

    [Header("디버그")]
    [Tooltip("콘솔에 청소 정보 표시")]
    public bool showDebugInfo = false;

    [Header("효과음")]
    [SerializeField] private AudioSource vacuumAudioSource;
    [SerializeField] private float vacuumStopDelay = 0.3f;

    private bool isVacuuming = false;
    private float vacuumStopTimer = 0f;

    private System.Collections.Generic.Dictionary<GameObject, VacuumDustData> dustProgress =
        new System.Collections.Generic.Dictionary<GameObject, VacuumDustData>();

    private InputDevice targetDevice;
    private bool isGripping = false;

    private enum PushDirection { None, Forward, Backward }
    private PushDirection currentDirection = PushDirection.None;
    private PushDirection lastDirection = PushDirection.None;
    private float directionChangeTimer = 0f;
    private GameObject currentDustBeingCleaned = null;
    private Vector3 lastVacuumVelocity = Vector3.zero;
    private MoneyManager moneyManager;

    void Start()
    {
        if (rightHandController == null)
            rightHandController = FindRightHandController();

        if (toolSwitcher == null)
            toolSwitcher = FindFirstObjectByType<ToolSwitcher>();

        moneyManager = FindFirstObjectByType<MoneyManager>();

        InitializeController();
    }

    Transform FindRightHandController()
    {
        GameObject xrOrigin = GameObject.Find("XR Origin");
        if (xrOrigin != null)
        {
            Transform cameraOffset = xrOrigin.transform.Find("Camera Offset");
            if (cameraOffset != null)
            {
                Transform rightHand = cameraOffset.Find("RightHand Controller");
                if (rightHand == null) rightHand = cameraOffset.Find("Right Hand Controller");
                if (rightHand == null) rightHand = cameraOffset.Find("Right Controller");

                if (rightHand != null)
                {
                    if (showDebugInfo)
                        Debug.Log("占쏙옙占쏙옙占쏙옙 占쏙옙트占싼뤄옙 占쌘듸옙 占쌩곤옙: " + rightHand.name);
                    return rightHand;
                }
            }
        }
        return null;
    }

    void InitializeController()
    {
        var devices = new System.Collections.Generic.List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(controllerCharacteristics, devices);

        if (devices.Count > 0)
        {
            targetDevice = devices[0];
            if (showDebugInfo)
                Debug.Log("占쏙옙트占싼뤄옙 占쏙옙占쏙옙占? " + targetDevice.name);
        }
    }

    void Update()
    {
        if (!targetDevice.isValid)
            InitializeController();

        if (vacuumStopTimer > 0f)
        {
            vacuumStopTimer -= Time.deltaTime;
            if (vacuumStopTimer <= 0f) { isVacuuming = false; vacuumAudioSource?.Stop(); }
        }

        bool isHoldingVacuum = IsHoldingVacuum();

        if (isHoldingVacuum)
        {
            CheckGripInput();

            if (isGripping)
            {
                CheckForDustContact();
            }
            else
            {
                currentDustBeingCleaned = null;
                currentDirection = PushDirection.None;
                lastDirection = PushDirection.None;
                lastVacuumVelocity = Vector3.zero;
                StopVacuumAudio();
            }
        }
        else
        {
            StopVacuumAudio();
        }

        if (directionChangeTimer > 0)
            directionChangeTimer -= Time.deltaTime;
    }

    bool IsHoldingVacuum()
    {
        if (toolSwitcher == null) return false;
        string currentTool = toolSwitcher.GetCurrentToolTag();
        return currentTool == "Vacuum";
    }

    void CheckGripInput()
    {
        if (!targetDevice.isValid) return;

        float triggerValue = 0f;
        targetDevice.TryGetFeatureValue(CommonUsages.trigger, out triggerValue);

        float gripValue = 0f;
        targetDevice.TryGetFeatureValue(CommonUsages.grip, out gripValue);

        bool wasPreviouslyGripping = isGripping;
        isGripping = (triggerValue >= triggerThreshold) && (gripValue >= gripThreshold);

        if (isGripping && !wasPreviouslyGripping)
        {
            if (targetDevice.isValid)
                targetDevice.SendHapticImpulse(0, 0.3f, 0.1f);

            if (showDebugInfo)
                Debug.Log("청占쌀깍옙 占쏙옙占쏙옙!");
        }
        else if (!isGripping && wasPreviouslyGripping)
        {
            if (showDebugInfo)
                Debug.Log("청占쌀깍옙 占쏙옙占쏙옙!");
        }
    }

    void CheckForDustContact()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, reachDistance);

        GameObject nearestDust = null;
        float nearestDistance = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Cleanable_Broom"))
            {
                float distance = Vector3.Distance(transform.position, hit.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestDust = hit.gameObject;
                }
            }
        }

        if (nearestDust != null)
        {
            if (currentDustBeingCleaned != nearestDust)
            {
                currentDustBeingCleaned = nearestDust;
                currentDirection = PushDirection.None;
                lastDirection = PushDirection.None;

                if (showDebugInfo)
                    Debug.Log($"占쏙옙 占쏙옙占쏙옙 占쏙옙占쏙옙: {nearestDust.name}");
            }

            DetectPushMotion(nearestDust);
        }
        else
        {
            currentDustBeingCleaned = null;
            currentDirection = PushDirection.None;
            lastDirection = PushDirection.None;
            lastVacuumVelocity = Vector3.zero;
            StopVacuumAudio();
        }
    }

    void DetectPushMotion(GameObject dust)
    {
        if (!targetDevice.isValid) return;

        Vector3 velocity = Vector3.zero;
        targetDevice.TryGetFeatureValue(CommonUsages.deviceVelocity, out velocity);

        // 월드 공간 수평(XZ) 속도만 사용 - 컨트롤러 방향 무관, 곡선 동작 허용
        Vector3 horizontalVel = new Vector3(velocity.x, 0f, velocity.z);
        float speed = horizontalVel.magnitude;

        if (showDebugInfo && Time.frameCount % 30 == 0)
            Debug.Log($"진공청소기 속도: {speed:F3} m/s");

        if (speed > pushVelocityThreshold)
            PlayVacuumAudio();
        else
            StopVacuumAudio();

        if (speed >= pushVelocityThreshold)
        {
            if (lastVacuumVelocity.magnitude >= pushVelocityThreshold)
            {
                float dot = Vector3.Dot(lastVacuumVelocity.normalized, horizontalVel.normalized);

                // 이전 방향과 현재 방향이 충분히 반대이면 왕복 1세트 완료
                if (directionChangeTimer <= 0 && dot < -0.3f)
                {
                    lastVacuumVelocity = Vector3.zero;
                    directionChangeTimer = directionChangeDelay;
                    OnPushCycleComplete(dust);
                    return;
                }
            }
            lastVacuumVelocity = horizontalVel;
        }
    }

    void OnPushCycleComplete(GameObject dust)
    {
        if (showDebugInfo)
            Debug.Log("占싻깍옙 占쏙옙 占쏙옙트 占싹뤄옙! (占쌌∽옙占?");

        CleanDust(dust);
    }

    void CleanDust(GameObject dust)
    {
        if (!dustProgress.ContainsKey(dust))
            dustProgress[dust] = new VacuumDustData(dust);

        VacuumDustData data = dustProgress[dust];

        if (data.isFullyCleaned) return;

        if (data.renderer == null)
        {
            Debug.LogWarning($"占쏙옙占쏙옙 占쏙옙占쏙옙占쏙옙트 '{dust.name}'占쏙옙 Renderer占쏙옙 占쏙옙占쏙옙占싹댐옙!");
            return;
        }

        data.cleanCount++;

        float newAlpha = 1f - (cleanPercentage * data.cleanCount);
        newAlpha = Mathf.Max(0f, newAlpha);

        Color color = data.renderer.material.color;
        color.a = newAlpha;
        data.renderer.material.color = color;

        SetMaterialTransparent(data.renderer.material);

        if (targetDevice.isValid)
            targetDevice.SendHapticImpulse(0, 0.4f, 0.15f);

        if (showDebugInfo)
            Debug.Log($"占쏙옙占쏙옙 청占쏙옙 占쏙옙... ({data.cleanCount}/{totalCleanCount}) - 占쏙옙占쏙옙占쏙옙: {newAlpha:F2}");

        if (data.cleanCount >= totalCleanCount)
        {
            data.isFullyCleaned = true;

            if (moneyManager != null)
                moneyManager.OnDustCleaned();

            if (showDebugInfo)
                Debug.Log("占쏙옙占쏙옙 占쏙옙占쏙옙 占쏙옙占쏙옙! (청占쌀깍옙占?占쏙옙 占쏙옙占쏙옙!)");

            if (targetDevice.isValid)
                targetDevice.SendHapticImpulse(0, 0.9f, 0.35f);

            Destroy(dust);
            dustProgress.Remove(dust);
            currentDustBeingCleaned = null;
        }
    }

    void SetMaterialTransparent(Material material)
    {
        if (material.shader.name.Contains("Standard"))
        {
            material.SetFloat("_Mode", 3);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
        }
        else if (material.shader.name.Contains("Universal Render Pipeline/Lit"))
        {
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.renderQueue = 3000;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        }
    }

    void PlayVacuumAudio()
    {
        if (vacuumAudioSource == null) return;
        vacuumStopTimer = 0f;
        if (!isVacuuming)
        {
            isVacuuming = true;
            vacuumAudioSource.loop = true;
            vacuumAudioSource.Play();
        }
    }

    void StopVacuumAudio()
    {
        if (vacuumAudioSource == null || !isVacuuming) return;
        if (vacuumStopTimer <= 0f)
            vacuumStopTimer = vacuumStopDelay;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, reachDistance);
    }
}

public class VacuumDustData
{
    public GameObject stainObject;
    public Renderer renderer;
    public int cleanCount = 0;
    public bool isFullyCleaned = false;

    public VacuumDustData(GameObject dust)
    {
        stainObject = dust;
        renderer = dust.GetComponent<Renderer>();
    }
}
