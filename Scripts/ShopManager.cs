using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopManager : MonoBehaviour
{
    [Header("연동")]
    public MoneyManager moneyManager;
    public ToolSwitcher toolSwitcher;

    [Header("슬롯 목록")]
    public ToolSlotUI[] toolSlots;

    [Header("보유 금액 UI")]
    public TextMeshProUGUI currentMoneyText;

    [Header("알림 UI")]
    public TextMeshProUGUI noticeText;
    public float noticeDuration = 2f;

    [Header("사운드")]
    public AudioSource sfxSource;
    public AudioClip purchaseSuccessClip;

    private Coroutine noticeCoroutine;
    private bool sprayPurchased = false;
    private System.Collections.Generic.HashSet<string> unlockedTools = new();

    void Start()
    {
        if (moneyManager == null)
            moneyManager = FindFirstObjectByType<MoneyManager>();
        if (toolSwitcher == null)
            toolSwitcher = FindFirstObjectByType<ToolSwitcher>();
        if (sfxSource == null)
            sfxSource = GetComponent<AudioSource>();

        foreach (var slot in toolSlots)
        {
            if (slot == null) continue;

            // 이 슬롯의 어느 티어라도 purchasableTools에 등록돼 있으면 구매 슬롯으로 표시
            if (toolSwitcher != null && toolSwitcher.purchasableTools != null && slot.tiers != null)
            {
                foreach (var tier in slot.tiers)
                {
                    if (tier == null) continue;
                    if (System.Array.Exists(toolSwitcher.purchasableTools, pt => pt.toolTag == tier.toolTag))
                    {
                        slot.isPurchasable = true;
                        break;
                    }
                }
            }
            slot.Initialize(this);
        }

        RefreshToolSwitcherButtons();
    }

    void Update()
    {
        if (currentMoneyText != null && moneyManager != null)
            currentMoneyText.text = $"보유 금액: {moneyManager.GetCurrentMoney():N0}원";
    }

    public void TryUpgrade(ToolSlotUI slot)
    {
        int nextTier = slot.currentTier + 1;
        if (nextTier >= slot.tiers.Length) return;

        ToolTier nextTierData = slot.tiers[nextTier];

        if (moneyManager.GetCurrentMoney() < nextTierData.price)
        {
            ShowNotice("돈이 부족합니다!");
            return;
        }

        moneyManager.SpendMoney(nextTierData.price);
        slot.currentTier = nextTier;
        slot.Refresh();
        RefreshToolSwitcherButtons();

        // Spray 구매 시
        if (nextTierData.toolName == "Spray")
        {
            sprayPurchased = true;

            SprayCleaner sprayCleaner = null;
            SprayCleaner[] allSprayCleaners = Resources.FindObjectsOfTypeAll<SprayCleaner>();
            foreach (SprayCleaner sc in allSprayCleaners)
            {
                if (sc.gameObject.scene.isLoaded) { sprayCleaner = sc; break; }
            }

            if (sprayCleaner != null)
                sprayCleaner.OnPurchased();
            else
                Debug.LogError("SprayCleaner를 찾을 수 없습니다!");
        }

        // 구매 잠금 도구 해금 (Flapper 등 purchasableTools에 등록된 태그)
        if (!string.IsNullOrEmpty(nextTierData.toolTag) && toolSwitcher != null)
        {
            bool isPurchasable = System.Array.Exists(
                toolSwitcher.purchasableTools ?? new PurchasableTool[0],
                pt => pt.toolTag == nextTierData.toolTag
            );
            if (isPurchasable && !unlockedTools.Contains(nextTierData.toolTag))
            {
                unlockedTools.Add(nextTierData.toolTag);
                toolSwitcher.UnlockTool(nextTierData.toolTag);
            }
        }

        if (sfxSource != null && purchaseSuccessClip != null)
            sfxSource.PlayOneShot(purchaseSuccessClip);

        ShowNotice($"{nextTierData.toolName} 구매 완료!");
    }

    void RefreshToolSwitcherButtons()
    {
        if (toolSwitcher == null) return;

        foreach (var slot in toolSlots)
        {
            ToolTier current = slot.GetCurrentTier();
            if (string.IsNullOrEmpty(current.toolTag)) continue;

            string baseTag = slot.tiers[0].toolTag;

            for (int i = 0; i < toolSwitcher.toolTags.Length; i++)
            {
                if (toolSwitcher.toolTags[i] == baseTag || IsCurrentToolInSlot(toolSwitcher.toolTags[i], slot))
                {
                    toolSwitcher.toolTags[i] = current.toolTag;

                    // ✅ 수정: 기본 도구(Tier 0)이고, Tier 데이터의 오프셋이 0이라면 덮어쓰지 않음
                    // 또는 그냥 업그레이드 되었을 때만(slot.currentTier > 0) 덮어쓰도록 설정
                    if (slot.currentTier > 0)
                    {
                        toolSwitcher.toolPositionOffsets[i] = current.positionOffset;
                        toolSwitcher.toolRotationOffsets[i] = current.rotationOffset;
                    }

                    // 버튼 이미지 교체
                    var btn = toolSwitcher.toolButtons[i];
                    if (btn == null) continue;

                    var img = btn.GetComponent<Image>();
                    if (img != null)
                        img.sprite = current.toolSprite;

                    btn.interactable = true;
                    break;
                }
            }
        }

        toolSwitcher.RefreshToolCache();
    }

    bool IsCurrentToolInSlot(string tag, ToolSlotUI slot)
    {
        foreach (var tier in slot.tiers)
            if (tier.toolTag == tag) return true;
        return false;
    }

    void ShowNotice(string message)
    {
        if (noticeText == null) return;
        if (noticeCoroutine != null) StopCoroutine(noticeCoroutine);
        noticeCoroutine = StartCoroutine(ShowNoticeCoroutine(message));
    }

    System.Collections.IEnumerator ShowNoticeCoroutine(string message)
    {
        noticeText.text = message;
        noticeText.gameObject.SetActive(true);
        yield return new WaitForSeconds(noticeDuration);
        noticeText.gameObject.SetActive(false);
    }

    public bool IsSprayPurchased() => sprayPurchased;
    public bool IsToolUnlocked(string tag) => unlockedTools.Contains(tag);
    public bool IsFlapperPurchased() => unlockedTools.Contains("Flapper");
}
