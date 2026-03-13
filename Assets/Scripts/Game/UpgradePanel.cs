using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 웨이브 종료 후 표시되는 업그레이드/상점 패널 UI.
/// </summary>
public class UpgradePanel : MonoBehaviour
{
    [Header("카드 슬롯 (4개)")]
    [SerializeField] private UpgradeCard[] cards;

    [Header("버튼")]
    [SerializeField] private Button          rerollButton;
    [SerializeField] private Button          buyExpButton;
    [SerializeField] private Button          nextWaveButton;

    [Header("정보 텍스트")]
    [SerializeField] private TextMeshProUGUI playerGoldText;

    [Header("노티피케이션")]
    [SerializeField] private GameObject      notificationRoot;
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private float           notificationDuration = 2f;

    [Header("다음 웨이브 버튼 딜레이 텍스트")]
    [SerializeField] private TextMeshProUGUI autoStartTimerText;

    [Header("타이머 색상")]
    [SerializeField] private Color timerWarningColor = Color.red;

    [Header("다음 웨이브 시작 딜레이")]
    [SerializeField] private float nextWaveDelay = 1f;

    [Header("캐릭터 초상화")]
    [SerializeField] private Image  characterPortraitImage;
    [SerializeField] private Sprite characterPortraitSprite;

    [Header("장착 슬롯 (6개)")]
    [SerializeField] private EquipSlot[] equipSlots;

    [Header("인벤토리 슬롯 (10개)")]
    [SerializeField] private InvenSlot[] invenSlots;

    [Header("인벤토리 액션 (장착/팔기)")]
    [SerializeField] private GameObject      invenActionPanel;
    [SerializeField] private Button          equipActionButton;
    [SerializeField] private Button          sellActionButton;

    [Header("장착 슬롯 액션 (해제/판매)")]
    [SerializeField] private GameObject      equipSlotActionPanel;
    [SerializeField] private Button          unequipActionButton;
    [SerializeField] private Button          sellEquipActionButton;

    [Header("등급 확률 텍스트 (Normal/Rare/Elite/Epic/Legend)")]
    [SerializeField] private TextMeshProUGUI normalProbText;
    [SerializeField] private TextMeshProUGUI rareProbText;
    [SerializeField] private TextMeshProUGUI eliteProbText;
    [SerializeField] private TextMeshProUGUI epicProbText;
    [SerializeField] private TextMeshProUGUI legendProbText;

    [Header("코인 날아가기 이펙트")]
    [SerializeField] private Image         coinImageSource;  // 복사할 코인 Image 컴포넌트
    [SerializeField] private RectTransform goldTextRect;     // 골드 텍스트 위치 (목적지)
    [SerializeField] private Canvas        rootCanvas;

    // ── 내부 상태 ────────────────────────────────────────────────────
    private Coroutine notifCoroutine;
    private Coroutine autoStartCoroutine;
    private int       selectedInvenIndex  = -1;
    private int       selectedEquipIndex  = -1;
    private bool      isFirstWave         = false;

    // ────────────────────────────────────────────────────────────────
    // 초기화
    // ────────────────────────────────────────────────────────────────
    private void Awake()
    {
        rerollButton? .onClick.AddListener(() => UpgradeManager.Shared?.TryReroll());
        buyExpButton? .onClick.AddListener(() => UpgradeManager.Shared?.TryBuyExp());
        nextWaveButton?.onClick.AddListener(OnNextWaveClicked);

        if (equipActionButton)    equipActionButton  .onClick.AddListener(OnEquipActionClicked);
        if (sellActionButton)     sellActionButton   .onClick.AddListener(OnSellActionClicked);
        if (unequipActionButton)  unequipActionButton.onClick.AddListener(OnUnequipActionClicked);
        if (sellEquipActionButton) sellEquipActionButton.onClick.AddListener(OnSellEquipActionClicked);
        if (invenActionPanel)     invenActionPanel   .SetActive(false);
        if (equipSlotActionPanel) equipSlotActionPanel.SetActive(false);

        for (int i = 0; i < invenSlots.Length; i++)
        {
            invenSlots[i].SlotIndex = i;
            invenSlots[i].Init(this);
        }
        for (int i = 0; i < equipSlots.Length; i++)
        {
            equipSlots[i].SlotIndex = i;
            equipSlots[i].Init(this);
        }

        gameObject.SetActive(false);
    }

    // ────────────────────────────────────────────────────────────────
    // OnEnable / OnDisable
    // ────────────────────────────────────────────────────────────────
    private void OnEnable()
    {
        foreach (var anim in GetComponentsInChildren<Animator>(true))
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;

        selectedInvenIndex  = -1;
        selectedEquipIndex  = -1;
        if (invenActionPanel)     invenActionPanel    .SetActive(false);
        if (equipSlotActionPanel) equipSlotActionPanel.SetActive(false);

        if (characterPortraitImage && characterPortraitSprite)
            characterPortraitImage.sprite = characterPortraitSprite;

        if (PlayerInventory.Shared != null)
            PlayerInventory.Shared.OnChanged += RefreshInventoryUI;

        RefreshInventoryUI();
    }

    private void OnDisable()
    {
        if (autoStartCoroutine != null) StopCoroutine(autoStartCoroutine);
        if (PlayerInventory.Shared != null)
            PlayerInventory.Shared.OnChanged -= RefreshInventoryUI;
    }

    // ────────────────────────────────────────────────────────────────
    // 카드 갱신 (UpgradeManager가 호출)
    // ────────────────────────────────────────────────────────────────
    public void Refresh(WeaponData[] drawnCards, int rerollPrice, int buyExpPrice,
                        int ownedCount, int maxOwned, int wave,
                        float pNormal = 0f, float pRare = 0f, float pElite = 0f,
                        float pEpic  = 0f, float pLegend = 0f)
    {
        isFirstWave = (wave == 0);

        for (int i = 0; i < cards.Length; i++)
        {
            if (i < drawnCards.Length && drawnCards[i] != null)
                cards[i].Setup(drawnCards[i]);
            else
                cards[i].SetEmpty();
        }

        // 첫 웨이브(wave=0)는 리롤·판매 금지
        if (rerollButton)         rerollButton.interactable        = !isFirstWave;
        if (sellActionButton)     sellActionButton.interactable    = !isFirstWave;
        if (sellEquipActionButton) sellEquipActionButton.interactable = !isFirstWave;

        // 등급 확률 텍스트
        if (normalProbText) normalProbText.text = $"{pNormal:0.#}%";
        if (rareProbText)   rareProbText.text   = $"{pRare:0.#}%";
        if (eliteProbText)  eliteProbText.text  = $"{pElite:0.#}%";
        if (epicProbText)   epicProbText.text   = $"{pEpic:0.#}%";
        if (legendProbText) legendProbText.text = $"{pLegend:0.#}%";

        RefreshGold();
        RefreshInventoryUI();
    }

    public void RefreshGold()
    {
        if (playerGoldText && PlayerStats.Shared != null)
            playerGoldText.text = $"{PlayerStats.Shared.Gold}";
    }

    // ────────────────────────────────────────────────────────────────
    // 인벤토리 / 장착 슬롯 UI 갱신
    // ────────────────────────────────────────────────────────────────
    public void RefreshInventoryUI()
    {
        var inv = PlayerInventory.Shared;
        if (inv == null) return;

        for (int i = 0; i < equipSlots.Length; i++)
            equipSlots[i].Refresh(i < inv.Equipped.Count ? inv.Equipped[i] : null, i == selectedEquipIndex);

        for (int i = 0; i < invenSlots.Length; i++)
            invenSlots[i].Refresh(
                i < inv.Inventory.Count ? inv.Inventory[i] : null,
                i == selectedInvenIndex);

        // 무기 미장착 시 웨이브 시작 불가
        bool hasEquipped = inv.Equipped.Count > 0;
        if (nextWaveButton) nextWaveButton.interactable = hasEquipped;

        RefreshGold();
    }

    // ────────────────────────────────────────────────────────────────
    // 인벤 슬롯 클릭 (InvenSlot이 호출)
    // ────────────────────────────────────────────────────────────────
    public void OnInvenSlotClicked(int slotIndex, Transform _)
    {
        var inv = PlayerInventory.Shared;
        if (inv == null) { Debug.LogWarning("[Panel] PlayerInventory.Shared is null"); return; }

        bool hasItem = slotIndex < inv.Inventory.Count;
        Debug.Log($"[Panel] InvenSlot clicked: slot={slotIndex}, hasItem={hasItem}, count={inv.Inventory.Count}");

        if (!hasItem || selectedInvenIndex == slotIndex)
        {
            selectedInvenIndex = -1;
            if (invenActionPanel) invenActionPanel.SetActive(false);
        }
        else
        {
            // 장착 슬롯 선택 해제
            selectedEquipIndex = -1;
            if (equipSlotActionPanel) equipSlotActionPanel.SetActive(false);

            selectedInvenIndex = slotIndex;
            if (invenActionPanel) invenActionPanel.SetActive(true);
        }

        RefreshInventoryUI();
    }

    // ────────────────────────────────────────────────────────────────
    // 장착 슬롯 클릭 (EquipSlot이 호출)
    // ────────────────────────────────────────────────────────────────
    public void OnEquipSlotClicked(int slotIndex)
    {
        var inv = PlayerInventory.Shared;
        if (inv == null) { Debug.LogWarning("[Panel] PlayerInventory.Shared is null"); return; }

        bool hasItem = slotIndex < inv.Equipped.Count && inv.Equipped[slotIndex] != null;
        Debug.Log($"[Panel] EquipSlot clicked: slot={slotIndex}, hasItem={hasItem}, equippedCount={inv.Equipped.Count}, equipSlotActionPanel={(equipSlotActionPanel != null ? "OK" : "NULL")}");

        if (!hasItem || selectedEquipIndex == slotIndex)
        {
            selectedEquipIndex = -1;
            if (equipSlotActionPanel) equipSlotActionPanel.SetActive(false);
        }
        else
        {
            // 인벤 선택 해제
            selectedInvenIndex = -1;
            if (invenActionPanel) invenActionPanel.SetActive(false);

            selectedEquipIndex = slotIndex;
            if (equipSlotActionPanel)
            {
                equipSlotActionPanel.SetActive(true);
                Debug.Log($"[Panel] equipSlotActionPanel SetActive(true) → activeSelf={equipSlotActionPanel.activeSelf}, activeInHierarchy={equipSlotActionPanel.activeInHierarchy}");
            }
            else
                Debug.LogWarning("[Panel] equipSlotActionPanel 레퍼런스 NULL!");
        }

        RefreshInventoryUI();
    }

    // ────────────────────────────────────────────────────────────────
    // 액션 버튼
    // ────────────────────────────────────────────────────────────────
    private void OnEquipActionClicked()
    {
        if (selectedInvenIndex < 0) return;
        PlayerInventory.Shared?.EquipFromInventory(selectedInvenIndex);
        SoundManager.Shared?.Play(SoundKey.UI_Equip);
        Deselect();
    }

    private void OnSellActionClicked()
    {
        if (selectedInvenIndex < 0) return;
        var inv = PlayerInventory.Shared;
        if (inv == null || selectedInvenIndex >= inv.Inventory.Count) return;

        // 코인 이펙트
        if (coinImageSource != null && rootCanvas != null && goldTextRect != null)
        {
            Camera cam      = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            Vector2 startSP = RectTransformUtility.WorldToScreenPoint(cam, invenSlots[selectedInvenIndex].transform.position);
            Vector2 endSP   = RectTransformUtility.WorldToScreenPoint(cam, goldTextRect.position);
            StartCoroutine(CoinFlyRoutine(startSP, endSP, cam));
        }

        inv.SellFromInventory(selectedInvenIndex);
        SoundManager.Shared?.Play(SoundKey.UI_Sell);
        Deselect();
    }

    private void OnUnequipActionClicked()
    {
        if (selectedEquipIndex < 0) return;
        var inv = PlayerInventory.Shared;
        if (inv == null) return;

        if (!inv.UnequipToInventory(selectedEquipIndex))
            ShowNotification("인벤토리가 가득 찼습니다.");
        else
            SoundManager.Shared?.Play(SoundKey.UI_Unequip);

        DeselectEquip();
    }

    private void OnSellEquipActionClicked()
    {
        if (selectedEquipIndex < 0) return;
        var inv = PlayerInventory.Shared;
        if (inv == null || selectedEquipIndex >= inv.Equipped.Count) return;

        // 코인 이펙트
        if (coinImageSource != null && rootCanvas != null && goldTextRect != null)
        {
            Camera cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            Vector2 startSP = RectTransformUtility.WorldToScreenPoint(cam, equipSlots[selectedEquipIndex].transform.position);
            Vector2 endSP   = RectTransformUtility.WorldToScreenPoint(cam, goldTextRect.position);
            StartCoroutine(CoinFlyRoutine(startSP, endSP, cam));
        }

        inv.SellFromEquipped(selectedEquipIndex);
        SoundManager.Shared?.Play(SoundKey.UI_Sell);
        DeselectEquip();
    }

    private void Deselect()
    {
        selectedInvenIndex = -1;
        if (invenActionPanel) invenActionPanel.SetActive(false);
    }

    private void DeselectEquip()
    {
        selectedEquipIndex = -1;
        if (equipSlotActionPanel) equipSlotActionPanel.SetActive(false);
    }

    // ────────────────────────────────────────────────────────────────
    // 코인 날아가기 코루틴 (coinImageSource 복사 사용)
    // ────────────────────────────────────────────────────────────────
    private IEnumerator CoinFlyRoutine(Vector2 startSP, Vector2 endSP, Camera cam)
    {
        const float flyDuration   = 0.55f;
        const float arcHeight     = 60f;
        const int   ghostCount    = 4;
        const float ghostInterval = 0.07f;

        var parentRt = (RectTransform)rootCanvas.transform;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, startSP, cam, out Vector2 start);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, endSP,   cam, out Vector2 end);

        // 잔상 생성
        for (int i = 1; i <= ghostCount; i++)
            StartCoroutine(CoinGhostRoutine(start, end, flyDuration, arcHeight, i * ghostInterval));

        // 메인 코인 (coinImageSource 복사)
        var coinGO  = Instantiate(coinImageSource.gameObject, rootCanvas.transform);
        coinGO.transform.SetAsLastSibling();
        var coinRt  = coinGO.GetComponent<RectTransform>();
        var coinImg = coinGO.GetComponent<Image>();
        if (coinImg) coinImg.raycastTarget = false;

        float elapsed = 0f;
        while (elapsed < flyDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t   = Mathf.Clamp01(elapsed / flyDuration);
            float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;
            coinRt.anchoredPosition = Vector2.Lerp(start, end, t) + Vector2.up * arc;
            yield return null;
        }
        Destroy(coinGO);
    }

    private IEnumerator CoinGhostRoutine(Vector2 start, Vector2 end, float flyDuration, float arcHeight, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        var ghostGO = Instantiate(coinImageSource.gameObject, rootCanvas.transform);
        ghostGO.transform.SetAsLastSibling();
        var ghostRt  = ghostGO.GetComponent<RectTransform>();
        var ghostCg  = ghostGO.GetComponent<CanvasGroup>();
        if (ghostCg == null) ghostCg = ghostGO.AddComponent<CanvasGroup>();
        var ghostImg = ghostGO.GetComponent<Image>();
        if (ghostImg) ghostImg.raycastTarget = false;

        float duration = flyDuration * 0.55f;
        float elapsed  = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t   = Mathf.Clamp01(elapsed / duration);
            float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;
            ghostRt.anchoredPosition = Vector2.Lerp(start, end, t) + Vector2.up * arc;
            ghostCg.alpha = Mathf.Lerp(0.45f, 0f, t);
            yield return null;
        }
        Destroy(ghostGO);
    }

    // ────────────────────────────────────────────────────────────────
    // 다음 웨이브 버튼
    // ────────────────────────────────────────────────────────────────
    private void OnNextWaveClicked()
    {
        if (autoStartCoroutine != null) StopCoroutine(autoStartCoroutine);
        nextWaveButton.interactable = false;
        autoStartCoroutine = StartCoroutine(NextWaveDelayRoutine());
    }

    private IEnumerator NextWaveDelayRoutine()
    {
        float remaining = nextWaveDelay;
        while (remaining > 0f)
        {
            if (autoStartTimerText)
            {
                autoStartTimerText.text  = Mathf.CeilToInt(remaining).ToString();
                autoStartTimerText.color = timerWarningColor;
            }
            yield return new WaitForSecondsRealtime(0.1f);
            remaining -= 0.1f;
        }
        UpgradeManager.Shared?.ClosePanel();
    }

    // ────────────────────────────────────────────────────────────────
    // 노티피케이션
    // ────────────────────────────────────────────────────────────────
    public void ShowNotification(string message)
    {
        if (notificationRoot == null) return;
        if (notifCoroutine != null) StopCoroutine(notifCoroutine);
        notifCoroutine = StartCoroutine(NotifRoutine(message));
    }

    private IEnumerator NotifRoutine(string message)
    {
        if (notificationText) notificationText.text = message;
        notificationRoot.SetActive(true);
        yield return new WaitForSecondsRealtime(notificationDuration);
        notificationRoot.SetActive(false);
    }
}
