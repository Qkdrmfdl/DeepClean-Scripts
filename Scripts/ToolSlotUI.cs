using UnityEngine;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class ToolTier
{
    public string toolName;
    public string toolTag;        // ToolSwitcher의 toolTags와 동일
    public Sprite toolSprite;
    public string description;
    public int price;             // 0이면 기본 제공
    public Vector3 positionOffset;
    public Vector3 rotationOffset;
}

public class ToolSlotUI : MonoBehaviour
{
    [Header("UI 요소")]
    public Image toolImage;
    public TextMeshProUGUI toolNameText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI priceText;
    public Button upgradeButton;
    public TextMeshProUGUI upgradeButtonText;

    [Header("단계 설정")]
    public ToolTier[] tiers;      // 0단계, 1단계, 2단계...

    [HideInInspector] public int currentTier = 0;
    [HideInInspector] public bool isPurchasable = false;
    private ShopManager shopManager;

    public void Initialize(ShopManager manager)
    {
        shopManager = manager;

        // 0단계는 기본 제공
        if (tiers.Length > 0 && tiers[0].price == 0)
            currentTier = 0;

        Refresh();
        upgradeButton.onClick.AddListener(OnUpgradeButtonClicked);
    }

    public void Refresh()
    {
        if (tiers.Length == 0) return;

        // 현재 단계 정보 표시
        ToolTier current = tiers[currentTier];
        toolImage.sprite = current.toolSprite;
        toolNameText.text = current.toolName;
        descriptionText.text = current.description;

        // 다음 단계 존재 여부
        bool hasNextTier = currentTier + 1 < tiers.Length;

        if (!hasNextTier)
        {
            // 최고 단계
            priceText.text = "최고 등급";
            upgradeButton.interactable = false;
            upgradeButtonText.text = "MAX";
        }
        else
        {
            ToolTier next = tiers[currentTier + 1];
            string label = isPurchasable ? "구매" : "업그레이드";
            priceText.text = $"{label}: {next.price:N0}원";
            upgradeButton.interactable = true;
            upgradeButtonText.text = label;
        }
    }

    public ToolTier GetCurrentTier() => tiers[currentTier];

    void OnUpgradeButtonClicked()
    {
        shopManager.TryUpgrade(this);
    }
}