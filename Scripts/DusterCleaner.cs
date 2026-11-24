using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

public class DusterCleaner : MonoBehaviour
{
    [Header("청소 설정")]
    [Tooltip("완전히 청소하는 데 필요한 털기 횟수")]
    public int totalDustingCount = 3;

    [Header("레이캐스트 설정")]
    [Tooltip("먼지털이가 닿을 수 있는 최대 거리")]
    public float reachDistance = 0.5f;

    [Header("털기 감지 설정")]
    [Tooltip("털기로 인정되는 최소 속도 (m/s)")]
    public float dustVelocityThreshold = 0.8f;

    [Tooltip("방향 전환 인식 기준 (dot product, 음수일수록 반대 방향 요구)")]
    public float reverseDirectionDot = -0.3f;

    [Tooltip("한 번 털기 후 쿨다운 (초)")]
    public float dustCooldown = 0.4f;

    [Header("잡기 설정")]
    [Tooltip("트리거를 얼마나 눌러야 '누른' 것으로 인정할지 (0~1)")]
    public float triggerThreshold = 0.7f;

    [Tooltip("그립을 얼마나 눌러야 '누른' 것으로 인정할지 (0~1)")]
    public float gripThreshold = 0.7f;

    [Header("파티클 설정")]
    [Tooltip("털 때 재생할 먼지 파티클 프리팹")]
    public ParticleSystem dustParticlePrefab;

    [Header("ToolSwitcher 연동")]
    public ToolSwitcher toolSwitcher;

    [Header("효과음")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("한 번 털 때마다 재생할 소리 (짧은 휙 소리 권장)")]
    [SerializeField] private AudioClip dustSound;

    [Header("디버그")]
    public bool showDebugInfo = true;

    private InputDevice rightDevice;
    private bool isGripping = false;
    private Vector3 prevVelocity = Vector3.zero;
    private Dictionary<GameObject, DusterData> dustProgress = new Dictionary<GameObject, DusterData>();
    private float cooldownTimer = 0f;
    private GameObject currentTarget = null;

    void Start()
    {
        if (toolSwitcher == null)
            toolSwitcher = FindFirstObjectByType<ToolSwitcher>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        InitializeController();
    }

    void InitializeController()
    {
        var chars = InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller;
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(chars, devices);
        if (devices.Count > 0)
            rightDevice = devices[0];
    }

    void Update()
    {
        if (!rightDevice.isValid)
            InitializeController();

        CheckGripInput();

        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;

        if (!IsHoldingDuster() || !isGripping)
        {
            prevVelocity = Vector3.zero;
            return;
        }

        Vector3 deviceVel = Vector3.zero;
        if (rightDevice.isValid)
            rightDevice.TryGetFeatureValue(CommonUsages.deviceVelocity, out deviceVel);

        FindAndCheckDuster(deviceVel);
    }

    void CheckGripInput()
    {
        if (!rightDevice.isValid) return;
        if (!IsHoldingDuster()) return;

        rightDevice.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue);
        rightDevice.TryGetFeatureValue(CommonUsages.grip, out float gripValue);

        bool wasGripping = isGripping;
        isGripping = triggerValue >= triggerThreshold && gripValue >= gripThreshold;

        if (isGripping && !wasGripping)
        {
            rightDevice.SendHapticImpulse(0, 0.3f, 0.1f);
            if (showDebugInfo)
                Debug.Log("[DusterCleaner] 먼지털이 잡음!");
        }
        else if (!isGripping && wasGripping)
        {
            prevVelocity = Vector3.zero;
            currentTarget = null;
            if (showDebugInfo)
                Debug.Log("[DusterCleaner] 먼지털이 놓음!");
        }
    }

    void FindAndCheckDuster(Vector3 currentVelocity)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, reachDistance);

        GameObject nearest = null;
        float nearestDist = float.MaxValue;

        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Cleanable_Duster"))
            {
                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = hit.gameObject;
                }
            }
        }

        if (nearest == null)
        {
            currentTarget = null;
            return;
        }

        if (currentTarget != nearest)
        {
            currentTarget = nearest;
            prevVelocity = currentVelocity;
            if (showDebugInfo)
                Debug.Log($"[DusterCleaner] 새 먼지 감지: {nearest.name}");
            return;
        }

        if (cooldownTimer > 0f) return;

        float currentSpeed = currentVelocity.magnitude;

        // 임계 속도 이상일 때만 방향을 기록 (매 프레임 덮어쓰지 않음)
        if (currentSpeed >= dustVelocityThreshold)
        {
            if (prevVelocity.magnitude >= dustVelocityThreshold)
            {
                float dot = Vector3.Dot(prevVelocity.normalized, currentVelocity.normalized);

                if (showDebugInfo && Time.frameCount % 10 == 0)
                    Debug.Log($"[DusterCleaner] dot={dot:F2}, speed={currentSpeed:F2}");

                if (dot < reverseDirectionDot)
                {
                    cooldownTimer = dustCooldown;
                    prevVelocity = Vector3.zero; // 다음 털기를 위해 리셋
                    OnDustShaken(nearest, nearest.transform.position);
                    return;
                }
            }

            prevVelocity = currentVelocity;
        }
    }

    void OnDustShaken(GameObject dust, Vector3 position)
    {
        if (!dustProgress.ContainsKey(dust))
            dustProgress[dust] = new DusterData(dust);

        DusterData data = dustProgress[dust];
        if (data.isFullyCleaned) return;

        data.shakingCount++;

        // Alpha 감소
        if (data.renderer != null)
        {
            // 첫 접촉 시 한 번만 투명 모드 설정
            if (!data.transparencySetup)
            {
                SetMaterialTransparent(data.renderer.material);
                data.transparencySetup = true;
            }

            float newAlpha = 1f - ((float)data.shakingCount / totalDustingCount);
            newAlpha = Mathf.Max(0f, newAlpha);
            ApplyAlpha(data.renderer.material, newAlpha);
        }

        // 파티클: renderer bounds 중심에서 재생
        Vector3 particlePos = Vector3.Lerp(dust.transform.position, transform.position, 0.8f);
        PlayDustParticle(particlePos);

        // 털기 효과음 (털 때마다 1회)
        if (audioSource != null && dustSound != null)
            audioSource.PlayOneShot(dustSound);

        // 털기 햅틱
        if (rightDevice.isValid)
            rightDevice.SendHapticImpulse(0, 0.3f, 0.15f);

        if (showDebugInfo)
            Debug.Log($"[DusterCleaner] 털기 {data.shakingCount}/{totalDustingCount}: {dust.name}");

        if (data.shakingCount >= totalDustingCount)
        {
            data.isFullyCleaned = true;

            MoneyManager moneyManager = FindFirstObjectByType<MoneyManager>();
            moneyManager?.OnDusterCleaned();

            // 완료 햅틱
            if (rightDevice.isValid)
                rightDevice.SendHapticImpulse(0, 0.8f, 0.3f);

            if (showDebugInfo)
                Debug.Log($"[DusterCleaner] 먼지 완전 제거: {dust.name}");

            dustProgress.Remove(dust);
            currentTarget = null;
            Destroy(dust);
        }
    }

    void PlayDustParticle(Vector3 position)
    {
        if (dustParticlePrefab == null) return;

        ParticleSystem ps = Instantiate(dustParticlePrefab, position, Quaternion.identity);
        ps.Play();
        Destroy(ps.gameObject, ps.main.duration + ps.main.startLifetime.constantMax);
    }

    void ApplyAlpha(Material material, float alpha)
    {
        if (material.HasProperty("_BaseColor"))
        {
            Color c = material.GetColor("_BaseColor");
            c.a = alpha;
            material.SetColor("_BaseColor", c);
        }

        if (material.HasProperty("_Color"))
        {
            Color c = material.GetColor("_Color");
            c.a = alpha;
            material.SetColor("_Color", c);
        }
    }

    void SetMaterialTransparent(Material material)
    {
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.renderQueue = 3000;

        if (material.shader.name.Contains("Standard"))
        {
            material.SetFloat("_Mode", 3);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        else if (material.shader.name.Contains("Universal Render Pipeline"))
        {
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
    }

    bool IsHoldingDuster()
    {
        return toolSwitcher != null && toolSwitcher.GetCurrentToolTag() == "Duster";
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, reachDistance);
    }
}

public class DusterData
{
    public GameObject dustObject;
    public Renderer renderer;
    public int shakingCount = 0;
    public bool isFullyCleaned = false;
    public bool transparencySetup = false;

    public DusterData(GameObject dust)
    {
        dustObject = dust;
        renderer = dust.GetComponent<Renderer>();
        if (renderer == null)
            Debug.LogWarning("먼지 오브젝트의 Renderer가 없습니다: " + dust.name);
    }
}
