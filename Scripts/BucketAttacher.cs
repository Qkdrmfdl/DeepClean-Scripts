using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

[System.Serializable]
public class BucketToolEntry
{
    [Tooltip("도구 태그 (TrunkToolEntry.toolTag와 정확히 일치해야 함)")]
    public string toolTag;

    [Tooltip("양동이 안에 미리 배치해둔 도구 오브젝트 (처음엔 비활성화)")]
    public GameObject bucketToolObject;
}

public class BucketAttacher : MonoBehaviour
{
    public static BucketAttacher Instance { get; private set; }

    [Header("양동이")]
    public GameObject bucket;

    [Header("양동이 안 도구 (미리 배치)")]
    public BucketToolEntry[] bucketTools;

    [Header("왼손 컨트롤러")]
    [Tooltip("XR Origin 하위 LeftHand Controller Transform. 비워두면 자동 탐색.")]
    public Transform leftHandController;

    [Header("손 부착 위치 (왼손 컨트롤러 기준 로컬 좌표)")]
    public Vector3 attachLocalPosition = new Vector3(0f, -0.2f, 0f);
    public Vector3 attachLocalRotation = Vector3.zero;

    [Header("효과음")]
    public AudioClip clankSound;
    [Range(0f, 1f)] public float clankVolume = 1f;

    [Header("주목 유도 연출 (양동이 부착 시)")]
    [Tooltip("부착 순간 왼손 컨트롤러에 줄 햅틱 세기 (0~1)")]
    public float attachHapticAmplitude = 0.9f;
    [Tooltip("햅틱 지속 시간 (초)")]
    public float attachHapticDuration = 0.6f;

    private AudioSource audioSource;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        foreach (var entry in bucketTools)
            if (entry.bucketToolObject != null)
                entry.bucketToolObject.SetActive(false);

        if (bucket != null)
            bucket.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>트렁크 열릴 때 호출</summary>
    public void AttachToLeftHand(string[] requiredTags)
    {
        if (bucket == null) return;

        Transform leftHand = leftHandController != null ? leftHandController : FindLeftHandController();
        if (leftHand == null)
        {
            Debug.LogWarning("[BucketAttacher] 왼손 컨트롤러를 찾지 못했습니다.");
            return;
        }

        // 1. 버킷 이동 전에 각 도구의 버킷 기준 상대 위치/회전 저장 (scale 무시)
        var relatives = new List<(GameObject tool, Vector3 localOffset, Quaternion localRot)>();
        foreach (var entry in bucketTools)
        {
            if (entry.bucketToolObject == null) continue;
            Vector3 worldOffset = entry.bucketToolObject.transform.position - bucket.transform.position;
            Vector3 localOffset = Quaternion.Inverse(bucket.transform.rotation) * worldOffset;
            Quaternion localRot  = Quaternion.Inverse(bucket.transform.rotation) * entry.bucketToolObject.transform.rotation;
            relatives.Add((entry.bucketToolObject, localOffset, localRot));
        }

        // 2. 버킷을 손에 부착 (기존 방식 그대로)
        bucket.transform.SetParent(leftHand);
        bucket.transform.localPosition = attachLocalPosition;
        bucket.transform.localRotation = Quaternion.Euler(attachLocalRotation);
        bucket.SetActive(true);

        // 3. 각 도구를 leftHand 직접 자식으로 붙임 — 버킷과의 상대 위치 유지
        foreach (var (tool, localOffset, localRot) in relatives)
        {
            Vector3    worldPos = bucket.transform.position + bucket.transform.rotation * localOffset;
            Quaternion worldRot = bucket.transform.rotation * localRot;

            tool.transform.SetParent(leftHand, worldPositionStays: true);
            tool.transform.position = worldPos;
            tool.transform.rotation = worldRot;
        }

        SetupDropZone();
        PlayAttachAttentionCue();
        Debug.Log("[BucketAttacher] 양동이 왼손에 부착 완료");
    }

    // 왼손에 양동이가 달렸다는 걸 놓치기 쉬워서(구두 설명 없이는 존재를 모르는 경우가 많음),
    // 부착 순간 햅틱으로 주목을 유도한다.
    private void PlayAttachAttentionCue()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, devices);
        if (devices.Count > 0)
            devices[0].SendHapticImpulse(0, attachHapticAmplitude, attachHapticDuration);
    }

    private void SetupDropZone()
    {
        if (bucket == null || bucket.GetComponentInChildren<BucketDropZone>() != null) return;

        var dropZoneGO = new GameObject("BucketDropZone");
        dropZoneGO.transform.SetParent(bucket.transform, false);
        dropZoneGO.transform.localPosition = Vector3.zero;

        var col = dropZoneGO.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.25f;

        dropZoneGO.AddComponent<BucketDropZone>();
        Debug.Log("[BucketAttacher] BucketDropZone 자동 생성 완료 (반경 0.25m)");
    }

    /// <summary>도구 선택 시 호출 — 트렁크 도구 숨김 + 양동이 안 도구 활성화</summary>
    public void PlaceToolInBucket(GameObject trunkTool, string toolTag)
    {
        if (trunkTool != null)
            trunkTool.SetActive(false);

        bool found = false;
        foreach (var entry in bucketTools)
        {
            if (entry.toolTag == toolTag && entry.bucketToolObject != null)
            {
                entry.bucketToolObject.SetActive(true);
                found = true;
                Debug.Log($"[BucketAttacher] {toolTag} 양동이에 담김");
                break;
            }
        }

        if (!found)
            Debug.LogWarning($"[BucketAttacher] '{toolTag}'에 매핑된 양동이 도구가 없습니다.");

        if (clankSound != null)
            audioSource.PlayOneShot(clankSound, clankVolume);
    }

    private Transform FindLeftHandController()
    {
        var xrOrigin = GameObject.Find("XR Origin");
        if (xrOrigin == null) return null;
        var cameraOffset = xrOrigin.transform.Find("Camera Offset");
        if (cameraOffset == null) return null;
        return cameraOffset.Find("LeftHand Controller")
            ?? cameraOffset.Find("Left Hand Controller")
            ?? cameraOffset.Find("Left Controller");
    }
}
