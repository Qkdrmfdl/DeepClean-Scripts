using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FurnitureUIButton : MonoBehaviour
{
    [Header("연결")]
    public FurnitureData furnitureData; // 이 버튼이 나타내는 가구
    public FurniturePlacementManager placementManager;

    [Header("UI 요소")]
    public Image iconImage; // 가구 이미지
    public TextMeshProUGUI nameText; // 가구 이름
    public TextMeshProUGUI priceText; // 가격

    private Button button;

    void Start()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnButtonClick);

        // 가구 정보 표시
        UpdateUI();
    }

    void UpdateUI()
    {
        if (furnitureData == null) return;

        // 아이콘 설정
        if (iconImage != null && furnitureData.icon != null)
        {
            iconImage.sprite = furnitureData.icon;
        }

        // 이름 설정
        if (nameText != null)
        {
            nameText.text = furnitureData.furnitureName;
        }

        // 가격 설정
        if (priceText != null)
        {
            priceText.text = $"${furnitureData.price}";
        }
    }

    void OnButtonClick()
    {
        if (placementManager != null && furnitureData != null)
        {
            placementManager.StartFurniturePlacement(furnitureData);
        }
        else
        {
            Debug.LogError("PlacementManager 또는 FurnitureData가 연결되지 않았습니다!");
        }
    }
}