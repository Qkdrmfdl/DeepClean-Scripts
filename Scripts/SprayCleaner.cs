using UnityEngine;
using UnityEngine.XR;

public class SprayCleaner : MonoBehaviour
{
    [Header("스프레이어 오브젝트")]
    public GameObject sprayObject;

    [Header("스프레이어 설정")]
    public float sprayDistance = 1.5f;
    public ParticleSystem sprayEffect;

    [Header("효과음")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("분사 중 반복 재생할 스프레이 소리")]
    [SerializeField] private AudioClip spraySound;

    [Header("스프레이어 위치/회전 오프셋")]
    public Vector3 positionOffset = Vector3.zero;
    public Vector3 rotationOffset = Vector3.zero;

    [Header("왼쪽 컨트롤러 설정")]
    public Transform leftHandController;
    public InputDeviceCharacteristics controllerCharacteristics =
        InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller;

    [Header("스프레이 적용 대상 태그")]
    [Tooltip("스프레이를 맞으면 '적은 횟수로 청소' 보너스가 적용될 얼룩 태그 목록.\n" +
             "스테이지마다 스펀지/손걸레 등 태그가 다를 수 있어 전부 등록해야 함 (예: Cleanable_Sponge, Cleanable_Rag)")]
    public string[] sprayableTags = { "Cleanable_Sponge", "Cleanable_Rag" };

    [Header("디버그")]
    public bool showDebugInfo = true;

    private InputDevice targetDevice;
    private bool isPurchased = false;
    private bool isActive = false; // 현재 스프레이어 표시 여부

    public bool IsPurchased => isPurchased;

    public static System.Collections.Generic.HashSet<GameObject> sprayedStains =
        new System.Collections.Generic.HashSet<GameObject>();

    void Start()
    {
        InitializeController();
        AttachToLeftHand();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // ShowSpray가 먼저 호출된 경우(isActive=true) 숨기지 않음
        if (!isActive && sprayObject != null)
            sprayObject.SetActive(false);
    }

    void InitializeController()
    {
        var devices = new System.Collections.Generic.List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(controllerCharacteristics, devices);

        if (devices.Count > 0)
        {
            targetDevice = devices[0];
            if (showDebugInfo)
                Debug.Log("왼쪽 컨트롤러 연결: " + targetDevice.name);
        }
    }

    void AttachToLeftHand()
    {
        if (leftHandController != null)
        {
            ApplyOffset();
            return;
        }

        GameObject xrOrigin = GameObject.Find("XR Origin");
        if (xrOrigin == null) { Debug.LogError("XR Origin 없음!"); return; }

        Transform cameraOffset = xrOrigin.transform.Find("Camera Offset");
        if (cameraOffset == null) { Debug.LogError("Camera Offset 없음!"); return; }

        string[] names = { "LeftHand Controller", "Left Hand Controller", "Left Controller" };
        foreach (string name in names)
        {
            Transform hand = cameraOffset.Find(name);
            if (hand != null)
            {
                leftHandController = hand;
                ApplyOffset();
                if (showDebugInfo)
                    Debug.Log("왼쪽 컨트롤러 발견: " + hand.name);
                return;
            }
        }

        Debug.LogError("왼쪽 컨트롤러를 찾지 못했습니다!");
        foreach (Transform child in cameraOffset)
            Debug.Log("Camera Offset 자식: " + child.name);
    }

    void ApplyOffset()
    {
        if (sprayObject == null || leftHandController == null) return;

        sprayObject.transform.SetParent(leftHandController);
        sprayObject.transform.localPosition = positionOffset;
        sprayObject.transform.localRotation = Quaternion.Euler(rotationOffset);

        if (showDebugInfo)
            Debug.Log($"오프셋 적용: pos={positionOffset}, rot={rotationOffset}");
    }

    void Update()
    {
        if (!targetDevice.isValid)
            InitializeController();

        if (isActive && sprayObject != null && !sprayObject.activeInHierarchy)
            Debug.LogWarning($"[SprayCleaner] isActive=true인데 비활성! activeSelf:{sprayObject.activeSelf} / 부모:{sprayObject.transform.parent?.name} / 부모활성:{sprayObject.transform.parent?.gameObject.activeInHierarchy}");

        if (!isActive) return;

        CheckTriggerInput();
    }

    void CheckTriggerInput()
    {
        if (!targetDevice.isValid) return;

        targetDevice.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue);

        if (triggerValue >= 0.7f)
            Spray();
        else
        {
            if (sprayEffect != null && sprayEffect.isPlaying)
                sprayEffect.Stop();

            SetSpraySound(false);
        }
    }

    void SetSpraySound(bool playing)
    {
        if (audioSource == null || spraySound == null) return;

        if (playing)
        {
            if (!audioSource.isPlaying)
            {
                audioSource.clip = spraySound;
                audioSource.loop = true;
                audioSource.Play();
            }
        }
        else if (audioSource.isPlaying && audioSource.clip == spraySound)
        {
            audioSource.Stop();
        }
    }

    void Spray()
    {
        if (sprayEffect != null && !sprayEffect.isPlaying)
            sprayEffect.Play();

        SetSpraySound(true);

        Transform origin = leftHandController != null ? leftHandController : transform;
        Ray ray = new Ray(origin.position, origin.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, sprayDistance);

        foreach (RaycastHit hit in hits)
        {
            if (IsSprayableTag(hit.collider.tag))
            {
                sprayedStains.Add(hit.collider.gameObject);
                if (showDebugInfo)
                    Debug.Log($"스프레이 적용: {hit.collider.gameObject.name}");
            }
        }

        if (targetDevice.isValid)
            targetDevice.SendHapticImpulse(0, 0.3f, 0.1f);
    }

    bool IsSprayableTag(string tag)
    {
        if (sprayableTags == null) return false;
        foreach (string t in sprayableTags)
            if (t == tag) return true;
        return false;
    }

    // 구매 완료 후 호출 - 스프레이어 준비만 (실제 표시는 나중)
    public void OnPurchased()
    {
        isPurchased = true;
        if (showDebugInfo)
            Debug.Log("스프레이어 구매 완료!");
    }

    // ToolSwitcher에서 Sponge 도구 선택 시 호출
    public void ShowSpray()
    {
        isActive = true;
        if (sprayObject != null)
        {
            sprayObject.SetActive(true);
            Debug.Log($"[SprayCleaner] ShowSpray 직후 - activeSelf:{sprayObject.activeSelf} / activeInHierarchy:{sprayObject.activeInHierarchy} / 부모:{sprayObject.transform.parent?.name}");
        }
    }

    // 다른 도구 선택 시 호출
    public void HideSpray()
    {
        isActive = false;
        if (sprayObject != null)
            sprayObject.SetActive(false);

        if (sprayEffect != null && sprayEffect.isPlaying)
            sprayEffect.Stop();

        SetSpraySound(false);

        if (showDebugInfo)
            Debug.Log("스프레이어 숨김");
    }

    // 오프셋이 실시간 반영 (Inspector에서 값 바꿀 때)
    void OnValidate()
    {
        if (sprayObject != null && leftHandController != null)
            ApplyOffset();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, transform.forward * sprayDistance);
    }
}
