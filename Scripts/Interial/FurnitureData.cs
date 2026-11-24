using UnityEngine;

[CreateAssetMenu(fileName = "NewFurniture", menuName = "Game/Furniture Data")]
public class FurnitureData : ScriptableObject
{
    [Header("기본 정보")]
    public string furnitureName = "가구 이름";
    public GameObject prefab; // 가구 3D 모델
    public int price = 100;
    public Sprite icon; // UI에 표시될 이미지

    [Header("배치 설정")]
    public bool useGridSnap = true; // 그리드 스냅 사용 여부
    public float gridSize = 0.5f; // 그리드 크기 (0.5m)
}