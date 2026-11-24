using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// HandCleaner와 동일한 방식으로 트렁크 도구를 자석처럼 잡아 양동이에 넣는 컴포넌트.
/// TrunkInteractionController가 트렁크를 열 때 활성화하고, 모두 담기면 자동 비활성화.
/// Physics.OverlapSphere → MoveTowards(당김) → Lerp(따라오기) → 양동이 근처 → 담기
/// </summary>
public class TrunkToolGrabber : MonoBehaviour
{
    [Header("컨트롤러 (비우면 ToolSwitcher에서 자동 탐색)")]
    public Transform rightControllerTransform;

    [Header("감지 범위")]
    [Tooltip("자석 당김이 시작되는 반경 (m)")]
    public float nearDetectRadius = 0.6f;
    [Tooltip("레이캐스트로 도구를 감지할 최대 거리 (m)")]
    public float rayDetectDistance = 5f;
    [Tooltip("이 거리 이하면 도구가 컨트롤러에 부착됨")]
    public float attachThreshold = 0.08f;
    [Tooltip("양동이에 이 거리 이하로 들어오면 담기 완료")]
    public float bucketRadius = 0.5f;

    [Header("이동 속도")]
    public float magnetSpeed = 4f;
    public float followSpeed = 20f;
    public float returnSpeed = 3f;

    [Header("부착 오프셋 (컨트롤러 로컬)")]
    public Vector3 holdOffset = new Vector3(0f, -0.05f, 0.05f);

    [Header("햅틱")]
    [Tooltip("도구 당기기 시작 시 진동 세기")]
    public float grabStartAmplitude = 0.6f;
    [Tooltip("도구 당기기 시작 시 진동 길이 (초)")]
    public float grabStartDuration = 0.12f;
    [Tooltip("도구가 손에 붙었을 때 진동 세기")]
    public float attachAmplitude = 0.9f;
    [Tooltip("도구가 손에 붙었을 때 진동 길이 (초)")]
    public float attachDuration = 0.2f;
    [Tooltip("양동이에 넣었을 때 진동 세기")]
    public float bucketAmplitude = 0.7f;
    [Tooltip("양동이에 넣었을 때 진동 길이 (초)")]
    public float bucketDuration = 0.15f;

    // ── 상태 머신 ──────────────────────────────────────────────────────
    enum State { Idle, Pulling, Holding, Returning }
    State state = State.Idle;

    private TrunkToolMarker heldMarker;
    private Vector3 originalPos;
    private InputDevice rightDevice;

    // ── 초기화 ─────────────────────────────────────────────────────────
    void OnEnable()
    {
        CacheInputDevice();
        ResolveRightController();
    }

    void ResolveRightController()
    {
        if (rightControllerTransform != null) return;

        // 1순위: ToolSwitcher — NearFarInteractor 또는 rightHandController
        var switcher = FindFirstObjectByType<ToolSwitcher>();
        if (switcher != null)
        {
            if (switcher.RightNearFarInteractor != null)
                rightControllerTransform = switcher.RightNearFarInteractor.transform;
            else if (switcher.rightHandController != null)
                rightControllerTransform = switcher.rightHandController;

            if (rightControllerTransform != null)
            {
                Debug.Log("[TrunkToolGrabber] 컨트롤러 발견 (ToolSwitcher)");
                return;
            }
        }

        // 2순위: XR Origin 직접 탐색
        var xrOrigin = GameObject.Find("XR Origin");
        if (xrOrigin != null)
        {
            var t = xrOrigin.transform.Find("Camera Offset/RightHand Controller")
                 ?? xrOrigin.transform.Find("Camera Offset/Right Hand Controller")
                 ?? xrOrigin.transform.Find("Camera Offset/Right Controller");
            if (t != null)
            {
                rightControllerTransform = t;
                Debug.Log("[TrunkToolGrabber] 컨트롤러 발견 (XR Origin)");
                return;
            }
        }

        Debug.LogWarning("[TrunkToolGrabber] 오른쪽 컨트롤러를 찾지 못했습니다.");
    }

    // ── 메인 루프 ──────────────────────────────────────────────────────
    void Update()
    {
        if (!rightDevice.isValid) CacheInputDevice();

        switch (state)
        {
            case State.Idle:      UpdateIdle();      break;
            case State.Pulling:   UpdatePulling();   break;
            case State.Holding:   UpdateHolding();   break;
            case State.Returning: UpdateReturning(); break;
        }
    }

    // Idle: 근접 또는 레이캐스트로 도구 감지, 트리거 입력 시 당기기 시작
    void UpdateIdle()
    {
        if (rightControllerTransform == null) ResolveRightController();

        if (!IsGrabHeld()) return;

        // 1순위: 물리적 근접 감지
        var nearest = FindNearestTool(RightController().position, nearDetectRadius);
        if (nearest != null) { StartPulling(nearest); return; }

        // 2순위: 레이캐스트 감지 (트렁크 도구가 멀리 있을 때)
        var pointed = FindToolByRaycast();
        if (pointed != null)
            StartPulling(pointed);
    }

    // Pulling: MoveTowards로 컨트롤러 쪽으로 자석처럼 당김
    void UpdatePulling()
    {
        if (heldMarker == null) { state = State.Idle; return; }

        if (!IsGrabHeld()) { StartReturning(); return; }

        Vector3 target = RightController().TransformPoint(holdOffset);
        heldMarker.transform.position = Vector3.MoveTowards(
            heldMarker.transform.position, target, magnetSpeed * Time.deltaTime);

        if (Vector3.Distance(heldMarker.transform.position, target) < attachThreshold)
        {
            state = State.Holding;
            SendHaptic(attachAmplitude, attachDuration); // 손에 붙는 순간
        }
    }

    // Holding: Lerp로 컨트롤러에 붙어 따라오고, 양동이 근접 시 담기
    void UpdateHolding()
    {
        if (heldMarker == null) { state = State.Idle; return; }

        if (!IsGrabHeld()) { StartReturning(); return; }

        Vector3 target = RightController().TransformPoint(holdOffset);
        heldMarker.transform.position = Vector3.Lerp(
            heldMarker.transform.position, target, followSpeed * Time.deltaTime);

        var bucket = BucketAttacher.Instance?.bucket;
        if (bucket != null)
        {
            float dist = Vector3.Distance(heldMarker.transform.position, bucket.transform.position);
            if (dist < bucketRadius)
                PlaceInBucket();
        }
    }

    // Returning: 놓았을 때 원래 위치로 복귀
    void UpdateReturning()
    {
        if (heldMarker == null) { state = State.Idle; return; }

        heldMarker.transform.position = Vector3.MoveTowards(
            heldMarker.transform.position, originalPos, returnSpeed * Time.deltaTime);

        if (Vector3.Distance(heldMarker.transform.position, originalPos) < 0.01f)
        {
            heldMarker.transform.position = originalPos;
            heldMarker = null;
            state = State.Idle;
        }
    }

    // ── 동작 ──────────────────────────────────────────────────────────
    void StartPulling(TrunkToolMarker marker)
    {
        heldMarker = marker;
        originalPos = marker.transform.position;
        state = State.Pulling;
        SendHaptic(grabStartAmplitude, grabStartDuration); // 잡기 시작
    }

    void StartReturning()
    {
        state = State.Returning;
    }

    void PlaceInBucket()
    {
        if (heldMarker == null) return;
        SendHaptic(bucketAmplitude, bucketDuration); // 양동이에 넣기
        heldMarker.controller?.OnToolSelected(heldMarker.toolTag);
        heldMarker = null;
        state = State.Idle;
    }

    // ── 유틸 ──────────────────────────────────────────────────────────

    // 컨트롤러 forward 방향 레이캐스트로 TrunkToolMarker 탐색
    TrunkToolMarker FindToolByRaycast()
    {
        var origin = RightController();
        var ray = new Ray(origin.position, origin.forward);
        var hits = Physics.RaycastAll(ray, rayDetectDistance);
        TrunkToolMarker best = null;
        float minDist = float.MaxValue;

        foreach (var hit in hits)
        {
            var marker = hit.collider.GetComponent<TrunkToolMarker>()
                      ?? hit.collider.GetComponentInParent<TrunkToolMarker>();
            if (marker == null || marker.controller == null) continue;
            if (hit.distance < minDist) { minDist = hit.distance; best = marker; }
        }
        return best;
    }

    TrunkToolMarker FindNearestTool(Vector3 center, float radius)
    {
        Collider[] cols = Physics.OverlapSphere(center, radius);
        TrunkToolMarker nearest = null;
        float minDist = float.MaxValue;

        foreach (var col in cols)
        {
            var marker = col.GetComponent<TrunkToolMarker>()
                      ?? col.GetComponentInParent<TrunkToolMarker>();
            if (marker == null || marker.controller == null) continue;

            float dist = Vector3.Distance(center, marker.transform.position);
            if (dist < minDist) { minDist = dist; nearest = marker; }
        }

        return nearest;
    }

    bool IsGrabHeld()
    {
        if (!rightDevice.isValid) return false;
        rightDevice.TryGetFeatureValue(CommonUsages.triggerButton, out bool trigger);
        rightDevice.TryGetFeatureValue(CommonUsages.gripButton, out bool grip);
        return trigger || grip;
    }

    Transform RightController() =>
        rightControllerTransform != null ? rightControllerTransform : transform;

    void CacheInputDevice()
    {
        var list = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, list);
        if (list.Count > 0) rightDevice = list[0];
    }

    void SendHaptic(float amplitude, float duration)
    {
        if (rightDevice.isValid)
            rightDevice.SendHapticImpulse(0, amplitude, duration);
    }

    // ── Gizmos ────────────────────────────────────────────────────────
    void OnDrawGizmos()
    {
        // 도구 감지 범위 (파랑)
        Transform origin = rightControllerTransform != null ? rightControllerTransform : transform;
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.12f);
        Gizmos.DrawSphere(origin.position, nearDetectRadius);
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.7f);
        Gizmos.DrawWireSphere(origin.position, nearDetectRadius);

        // 양동이 담기 구역 (초록) — HandCleaner trashBin과 동일한 스타일
        var bucket = BucketAttacher.Instance?.bucket;
        if (bucket != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
            Gizmos.DrawSphere(bucket.transform.position, bucketRadius);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(bucket.transform.position, bucketRadius);
        }
    }
}
