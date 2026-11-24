using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

public class PlayerSeater : MonoBehaviour
{
    [Tooltip("운전석 위치 Transform (Car 자식으로 배치)")]
    public Transform seatPoint;

    [Tooltip("XR Origin 오브젝트")]
    public Transform playerTransform;

    [Tooltip("PlayerSeater가 붙어있는 Car 루트 오브젝트. 비워두면 이 컴포넌트의 오브젝트 사용")]
    public Transform carRoot;

    [Tooltip("이 씬 이름에서만 실행합니다. 비워두면 모든 씬에서 실행.")]
    public string onlyInScene = "";

    IEnumerator Start()
    {
        if (!string.IsNullOrEmpty(onlyInScene) &&
            SceneManager.GetActiveScene().name != onlyInScene)
            yield break;

        yield return null;

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) playerTransform = player.transform;
        }

        if (playerTransform == null || seatPoint == null)
        {
            Debug.LogWarning("[PlayerSeater] playerTransform 또는 seatPoint가 연결되지 않았습니다.");
            yield break;
        }

        // XRBodyTransformer 검색 (XR Origin 본인 → 자식 → 씬 전체 순서)
        var bodyTransformer = playerTransform.GetComponent<XRBodyTransformer>()
            ?? playerTransform.GetComponentInChildren<XRBodyTransformer>()
            ?? FindObjectOfType<XRBodyTransformer>();

        if (bodyTransformer != null) bodyTransformer.enabled = false;

        playerTransform.position = seatPoint.position;
        playerTransform.rotation = seatPoint.rotation;
    }
}
