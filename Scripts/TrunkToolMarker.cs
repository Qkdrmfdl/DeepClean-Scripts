using UnityEngine;

/// <summary>
/// 트렁크 도구 오브젝트에 런타임으로 추가되는 마커.
/// BucketDropZone이 어떤 도구인지, 어느 컨트롤러에 알릴지 파악하기 위해 사용.
/// </summary>
public class TrunkToolMarker : MonoBehaviour
{
    public string toolTag;
    public TrunkInteractionController controller;
}
