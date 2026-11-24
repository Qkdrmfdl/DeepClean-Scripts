using UnityEngine;
using UnityEngine.XR;

public class FlapperCleaner : MonoBehaviour
{
    [Header("청소 설정")]
    [Tooltip("파리채가 인식하는 스윙 최소 속도 (m/s)")]
    public float swingVelocityThreshold = 1.5f;

    [Tooltip("같은 파리를 중복 처리 방지 쿨다운 (초)")]
    public float hitCooldown = 0.3f;

    [Header("잡기 설정")]
    [Tooltip("트리거를 얼마나 눌러야 '누른' 것으로 인정할지 (0~1)")]
    public float triggerThreshold = 0.7f;

    [Tooltip("그립을 얼마나 눌러야 '누른' 것으로 인정할지 (0~1)")]
    public float gripThreshold = 0.7f;

    [Header("ToolSwitcher 연동")]
    public ToolSwitcher toolSwitcher;

    [Header("효과음")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip swingSound;
    [SerializeField] private AudioClip hitSound;

    [Header("충돌 감지")]
    [Tooltip("파리채 헤드 중심에서 감지할 반지름 (m)")]
    public float hitRadius = 0.15f;

    [Header("디버그")]
    public bool showDebugInfo = true;

    private InputDevice rightDevice;
    private Vector3 cachedVelocity;
    private Vector3 prevPosition;
    private float cooldownTimer = 0f;
    private bool isGripping = false;
    private bool wasSwinging = false;

    void Start()
    {
        if (toolSwitcher == null)
            toolSwitcher = FindFirstObjectByType<ToolSwitcher>();

        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        prevPosition = transform.position;
        InitializeController();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    void InitializeController()
    {
        var chars = InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;
        var devices = new System.Collections.Generic.List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(chars, devices);
        if (devices.Count > 0)
            rightDevice = devices[0];
    }

    void Update()
    {
        if (!rightDevice.isValid)
            InitializeController();

        CheckGripInput();

        Vector3 deviceVel = Vector3.zero;
        if (rightDevice.isValid)
            rightDevice.TryGetFeatureValue(CommonUsages.deviceVelocity, out deviceVel);

        Vector3 positionVel = (transform.position - prevPosition) / Time.deltaTime;
        prevPosition = transform.position;

        cachedVelocity = deviceVel.magnitude >= positionVel.magnitude ? deviceVel : positionVel;

        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        // 스윙 소리: 속도가 임계값을 처음 넘는 순간 1회 재생
        bool isSwinging = isGripping && cachedVelocity.magnitude >= swingVelocityThreshold;
        if (isSwinging && !wasSwinging && audioSource != null && swingSound != null)
            audioSource.PlayOneShot(swingSound);
        wasSwinging = isSwinging;

        // OverlapSphere로 직접 파리 감지 (OnTriggerEnter tunneling 방지)
        if (isSwinging && cooldownTimer <= 0f)
            CheckFlyOverlap();
    }

    void CheckFlyOverlap()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, hitRadius);
        foreach (Collider col in hits)
        {
            if (col.CompareTag("Cleanable_Flapper"))
            {
                cooldownTimer = hitCooldown;
                KillFly(col.gameObject);
                return;
            }
        }
    }

    void CheckGripInput()
    {
        if (!rightDevice.isValid) return;
        if (!IsHoldingFlapper()) return;

        rightDevice.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue);
        rightDevice.TryGetFeatureValue(CommonUsages.grip, out float gripValue);

        bool wasGripping = isGripping;
        isGripping = triggerValue >= triggerThreshold && gripValue >= gripThreshold;

        if (isGripping && !wasGripping)
        {
            rightDevice.SendHapticImpulse(0, 0.3f, 0.1f);
            if (showDebugInfo)
                Debug.Log("[FlapperCleaner] 파리채 잡음!");
        }
        else if (!isGripping && wasGripping)
        {
            if (showDebugInfo)
                Debug.Log("[FlapperCleaner] 파리채 놓음!");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsHoldingFlapper()) return;
        if (!isGripping) return;
        if (cooldownTimer > 0f) return;
        if (!other.CompareTag("Cleanable_Flapper")) return;

        float speed = cachedVelocity.magnitude;

        if (showDebugInfo)
            Debug.Log($"[FlapperCleaner] 파리 접촉! 속도: {speed:F2} m/s (임계값: {swingVelocityThreshold})");

        if (speed < swingVelocityThreshold)
        {
            if (showDebugInfo)
                Debug.Log("[FlapperCleaner] 속도 부족 → 스윙이 너무 느림");
            return;
        }

        cooldownTimer = hitCooldown;
        KillFly(other.gameObject);
    }

    void KillFly(GameObject fly)
    {
        MoneyManager moneyManager = FindFirstObjectByType<MoneyManager>();
        if (moneyManager != null)
            moneyManager.OnFlyCleaned();

        if (audioSource != null && hitSound != null)
            audioSource.PlayOneShot(hitSound);

        rightDevice.SendHapticImpulse(0, 0.8f, 0.2f);

        if (showDebugInfo)
            Debug.Log($"[FlapperCleaner] 파리 제거 성공: {fly.name}");

        Destroy(fly);
    }

    bool IsHoldingFlapper()
    {
        return toolSwitcher != null && toolSwitcher.GetCurrentToolTag() == "Flapper";
    }
}
