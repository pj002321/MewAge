using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    [SerializeField] private Button          statPanelButton;  // 능력치 보기 팝업 버튼

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

    [Header("EXP 구매 롱프레스")]
    [SerializeField] private float buyExpHoldDelay    = 0.5f;   // 처음 반복까지 대기 시간
    [SerializeField] private float buyExpRepeatInterval = 0.12f; // 반복 구매 간격

    [Header("다음 웨이브 시작 딜레이")]
    [SerializeField] private float nextWaveDelay = 1f;

    [Header("캐릭터 초상화")]
    [SerializeField] private Image  characterPortraitImage;
    [SerializeField] private Sprite characterPortraitSprite;

    [Header("판매 드롭존")]
    [SerializeField] private RectTransform sellImg;
    [SerializeField] private float         sellZoneHoverScale = 1.2f;

    [Header("장착 슬롯 (6개)")]
    [SerializeField] private EquipSlot[] equipSlots;

    [Header("인벤토리 슬롯 (10개)")]
    [SerializeField] private InvenSlot[] invenSlots;

    [Header("시너지 슬롯 (챕터별 4개)")]
    [SerializeField] private Image[]             synergySlotImages   = new Image[4];
    [SerializeField] private TextMeshProUGUI[]   synergySlotNames    = new TextMeshProUGUI[4];
    [SerializeField] private TextMeshProUGUI[]   synergySlotDescs    = new TextMeshProUGUI[4];

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
    private Coroutine          notifCoroutine;
    private Coroutine          autoStartCoroutine;
    private Coroutine          buyExpHoldCoroutine;
    private int                selectedInvenIndex  = -1;
    private int                selectedEquipIndex  = -1;
    private bool               isFirstWave         = false;
    // 챕터 시너지 슬롯에 매핑된 태그 (Refresh 시 캐싱)
    private WeaponSynergyTag[] synergySlotTags = new WeaponSynergyTag[4];

    // ────────────────────────────────────────────────────────────────
    // 초기화
    // ────────────────────────────────────────────────────────────────
    private void Awake()
    {
        rerollButton? .onClick.AddListener(() => UpgradeManager.Shared?.TryReroll());
        nextWaveButton?.onClick.AddListener(OnNextWaveClicked);
        statPanelButton?.onClick.AddListener(() => StatPanelUI.Shared?.Show());

        // EXP 구매 버튼: 롱프레스 지원 (EventTrigger로 PointerDown/Up 처리)
        if (buyExpButton != null)
        {
            var trigger = buyExpButton.GetComponent<EventTrigger>()
                       ?? buyExpButton.gameObject.AddComponent<EventTrigger>();
            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ => OnBuyExpPointerDown());
            trigger.triggers.Add(down);
            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ => OnBuyExpPointerUp());
            trigger.triggers.Add(up);
        }

        for (int i = 0; i < synergySlotImages.Length; i++)
        {
            int idx = i;
            var img = synergySlotImages[idx];
            if (img == null) continue;
            // 같은 오브젝트 또는 부모에서 Button 자동 검색
            var btn = img.GetComponent<Button>() ?? img.GetComponentInParent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => OnSynergySlotClicked(idx));
            else
                Debug.LogWarning($"[UpgradePanel] synergySlotImages[{idx}] 에서 Button을 찾을 수 없습니다.");
        }

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
        WeaponInfoPanel.Shared?.Hide();
        if (sellImg) { sellImg.localScale = Vector3.one; sellImg.gameObject.SetActive(false); }

        if (characterPortraitImage)
        {
            var playerSr  = PlayerController.Instance?.GetComponentInChildren<SpriteRenderer>();
            var spr       = playerSr?.sprite ?? characterPortraitSprite;
            if (spr != null) characterPortraitImage.sprite = spr;
        }

        if (PlayerInventory.Shared != null)
            PlayerInventory.Shared.OnChanged += RefreshInventoryUI;

        RefreshInventoryUI();
    }

    private void OnDisable()
    {
        if (autoStartCoroutine != null) StopCoroutine(autoStartCoroutine);
        if (notifCoroutine != null) { StopCoroutine(notifCoroutine); notifCoroutine = null; }
        if (buyExpHoldCoroutine != null) { StopCoroutine(buyExpHoldCoroutine); buyExpHoldCoroutine = null; }
        if (notificationRoot) notificationRoot.SetActive(false);
        if (PlayerInventory.Shared != null)
            PlayerInventory.Shared.OnChanged -= RefreshInventoryUI;
    }

    // ────────────────────────────────────────────────────────────────
    // 카드 갱신 (UpgradeManager가 호출)
    // ────────────────────────────────────────────────────────────────
    public void Refresh(WeaponData[] drawnCards, int rerollPrice, int buyExpPrice,
                        int ownedCount, int maxOwned, int wave,
                        float pNormal = 0f, float pRare = 0f, float pElite = 0f,
                        float pEpic  = 0f, float pLegend = 0f, WeaponData adCardData = null)
    {
        isFirstWave = (wave == 0);

        for (int i = 0; i < cards.Length; i++)
        {
            bool isAdSlot = (i == cards.Length - 1);
            if (isAdSlot)
            {
                if (adCardData != null)
                    cards[i].SetupAdCard(adCardData, () => UpgradeManager.Shared?.TryClaimAdCard());
                else
                    cards[i].SetEmpty();
            }
            else if (i < drawnCards.Length && drawnCards[i] != null)
                cards[i].Setup(drawnCards[i]);
            else
                cards[i].SetEmpty();
        }

        // 첫 웨이브(wave=0)는 리롤 금지
        if (rerollButton) rerollButton.interactable = !isFirstWave;

        // 등급 확률 텍스트
        if (normalProbText) normalProbText.text = $"{pNormal:0.#}%";
        if (rareProbText)   rareProbText.text   = $"{pRare:0.#}%";
        if (eliteProbText)  { eliteProbText.text = $"{pElite:0.#}%"; eliteProbText.color = new Color(0.1f, 0.75f, 1f); }
        if (epicProbText)   epicProbText.text   = $"{pEpic:0.#}%";
        if (legendProbText) legendProbText.text = $"{pLegend:0.#}%";

        RefreshGold();
        RefreshSynergySlots(wave);
        RefreshInventoryUI();
    }

    private void RefreshSynergySlots(int wave)
    {
        int chapter = UpgradeManager.Shared?.CurrentChapter ?? 1;
        var synergies = SynergyHelper.GetChapterSynergies(chapter);

        for (int i = 0; i < synergySlotImages.Length; i++)
        {
            var img = synergySlotImages[i];
            if (img == null) continue;

            if (i < synergies.Count)
            {
                var entry = synergies[i];
                synergySlotTags[i] = entry.Tag;
                img.sprite  = entry.Icon;
                img.enabled = entry.Icon != null;
                if (i < synergySlotNames.Length && synergySlotNames[i])
                    synergySlotNames[i].text = entry.Name;
                if (i < synergySlotDescs.Length && synergySlotDescs[i])
                    synergySlotDescs[i].text = entry.Description;
            }
            else
            {
                synergySlotTags[i] = WeaponSynergyTag.None;
                img.enabled = false;
                if (i < synergySlotNames.Length && synergySlotNames[i])
                    synergySlotNames[i].text = string.Empty;
                if (i < synergySlotDescs.Length && synergySlotDescs[i])
                    synergySlotDescs[i].text = string.Empty;
            }
        }
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

        RefreshSynergyColors();
        RefreshGold();
    }

    private void RefreshSynergyColors()
    {
        for (int i = 0; i < synergySlotImages.Length; i++)
        {
            var img = synergySlotImages[i];
            if (img == null) continue;
            int count = SynergyHelper.CountEquippedTag(synergySlotTags[i]);
            img.color = SynergyHelper.GetTierColor(count);
        }

        // 시너지 정보 패널이 열려있으면 장착 수 반영
        SynergyInfoPanel.Shared?.Refresh();
    }

    private void OnSynergySlotClicked(int index)
    {
        int chapter = UpgradeManager.Shared?.CurrentChapter ?? 1;
        Debug.Log($"[Synergy] slot {index} clicked, chapter={chapter}, panel={SynergyInfoPanel.Shared}");
        SynergyInfoPanel.Shared?.Show(chapter);
    }

    // ────────────────────────────────────────────────────────────────
    // 인벤 슬롯 클릭 (InvenSlot이 호출)
    // ────────────────────────────────────────────────────────────────
    public void OnInvenSlotClicked(int slotIndex, Transform _)
    {
        var inv = PlayerInventory.Shared;
        if (inv == null) return;

        bool hasItem = slotIndex < inv.Inventory.Count;

        if (!hasItem || selectedInvenIndex == slotIndex)
        {
            selectedInvenIndex = -1;
            WeaponInfoPanel.Shared?.Hide();
        }
        else
        {
            selectedEquipIndex = -1;
            selectedInvenIndex = slotIndex;

            var item = inv.Inventory[slotIndex];
            WeaponInfoPanel.Shared?.Show(item,
                onEquip: () =>
                {
                    PlayerInventory.Shared?.EquipFromInventory(slotIndex);
                    SoundManager.Shared?.Play(SoundKey.UI_Equip);
                    selectedInvenIndex = -1;
                    RefreshInventoryUI();
                },
                onSell: isFirstWave ? null : (System.Action)(() =>
                {
                    DoSellInven(slotIndex);
                    selectedInvenIndex = -1;
                    RefreshInventoryUI();
                })
            );
        }

        RefreshInventoryUI();
    }

    // ────────────────────────────────────────────────────────────────
    // 장착 슬롯 클릭 (EquipSlot이 호출)
    // ────────────────────────────────────────────────────────────────
    public void OnEquipSlotClicked(int slotIndex)
    {
        var inv = PlayerInventory.Shared;
        if (inv == null) return;

        bool hasItem = slotIndex < inv.Equipped.Count && inv.Equipped[slotIndex] != null;

        if (!hasItem || selectedEquipIndex == slotIndex)
        {
            selectedEquipIndex = -1;
            WeaponInfoPanel.Shared?.Hide();
        }
        else
        {
            selectedInvenIndex = -1;
            selectedEquipIndex = slotIndex;

            WeaponInfoPanel.Shared?.Show(inv.Equipped[slotIndex],
                onUnequip: () =>
                {
                    var i = PlayerInventory.Shared;
                    if (i != null && !i.UnequipToInventory(slotIndex))
                    {
                        ShowNotification("인벤토리가 가득 찼습니다.");
                    }
                    else
                    {
                        SoundManager.Shared?.Play(SoundKey.UI_Unequip);
                        UpgradeManager.Shared?.TryAutoMergeAll();
                    }
                    selectedEquipIndex = -1;
                    RefreshInventoryUI();
                },
                onSell: isFirstWave ? null : (System.Action)(() =>
                {
                    DoSellEquip(slotIndex);
                    selectedEquipIndex = -1;
                    RefreshInventoryUI();
                })
            );
        }

        RefreshInventoryUI();
    }

    // ────────────────────────────────────────────────────────────────
    // 슬롯 드래그 콜백 (InvenSlot / EquipSlot이 호출)
    // ────────────────────────────────────────────────────────────────
    public void NotifyDragBegin()
    {
        if (nextWaveButton) nextWaveButton.interactable = false;
        if (sellImg) sellImg.gameObject.SetActive(true);
    }

    public void NotifyDragHover(Vector2 screenPos)
    {
        if (sellImg == null || !sellImg.gameObject.activeSelf) return;
        Camera cam = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera : null;
        bool over = RectTransformUtility.RectangleContainsScreenPoint(sellImg, screenPos, cam);
        sellImg.localScale = Vector3.one * (over ? sellZoneHoverScale : 1f);
    }

    /// <summary>드래그 종료. sellImg 위면 true 반환 (판매 실행은 슬롯 쪽에서 호출).</summary>
    public bool NotifyDragEnd(Vector2 screenPos)
    {
        bool overSell = false;
        if (sellImg != null && sellImg.gameObject.activeSelf)
        {
            Camera cam = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera : null;
            overSell = RectTransformUtility.RectangleContainsScreenPoint(sellImg, screenPos, cam);
            sellImg.localScale = Vector3.one;
            sellImg.gameObject.SetActive(false);
        }
        // 무기 장착 여부에 따라 다음 웨이브 버튼 복원
        bool hasEquipped = PlayerInventory.Shared?.Equipped.Count > 0;
        if (nextWaveButton) nextWaveButton.interactable = hasEquipped;
        return overSell;
    }

    public void SellDraggedInven(int slotIndex)
    {
        DoSellInven(slotIndex);
        RefreshInventoryUI();
    }

    public void SellDraggedEquip(int slotIndex)
    {
        DoSellEquip(slotIndex);
        RefreshInventoryUI();
    }

    // ────────────────────────────────────────────────────────────────
    // 판매 헬퍼 (코인 이펙트 포함)
    // ────────────────────────────────────────────────────────────────
    private void DoSellInven(int slotIndex)
    {
        var inv = PlayerInventory.Shared;
        if (inv == null || slotIndex >= inv.Inventory.Count) return;

        if (coinImageSource != null && rootCanvas != null && goldTextRect != null)
        {
            Camera cam      = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            Vector2 startSP = RectTransformUtility.WorldToScreenPoint(cam, invenSlots[slotIndex].transform.position);
            Vector2 endSP   = RectTransformUtility.WorldToScreenPoint(cam, goldTextRect.position);
            StartCoroutine(CoinFlyRoutine(startSP, endSP, cam));
        }

        inv.SellFromInventory(slotIndex);
        SoundManager.Shared?.Play(SoundKey.UI_Sell);
    }

    private void DoSellEquip(int slotIndex)
    {
        var inv = PlayerInventory.Shared;
        if (inv == null || slotIndex >= inv.Equipped.Count) return;

        if (coinImageSource != null && rootCanvas != null && goldTextRect != null)
        {
            Camera cam      = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            Vector2 startSP = RectTransformUtility.WorldToScreenPoint(cam, equipSlots[slotIndex].transform.position);
            Vector2 endSP   = RectTransformUtility.WorldToScreenPoint(cam, goldTextRect.position);
            StartCoroutine(CoinFlyRoutine(startSP, endSP, cam));
        }

        inv.SellFromEquipped(slotIndex);
        SoundManager.Shared?.Play(SoundKey.UI_Sell);
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
    // EXP 구매 롱프레스
    // ────────────────────────────────────────────────────────────────
    private void OnBuyExpPointerDown()
    {
        UpgradeManager.Shared?.TryBuyExp();
        if (buyExpHoldCoroutine != null) StopCoroutine(buyExpHoldCoroutine);
        buyExpHoldCoroutine = StartCoroutine(BuyExpHoldRoutine());
    }

    private void OnBuyExpPointerUp()
    {
        if (buyExpHoldCoroutine != null) { StopCoroutine(buyExpHoldCoroutine); buyExpHoldCoroutine = null; }
    }

    private IEnumerator BuyExpHoldRoutine()
    {
        yield return new WaitForSecondsRealtime(buyExpHoldDelay);
        while (true)
        {
            UpgradeManager.Shared?.TryBuyExp();
            yield return new WaitForSecondsRealtime(buyExpRepeatInterval);
        }
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
