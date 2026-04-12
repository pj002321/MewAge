using System.Collections;
using System.Collections.Generic;
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

    [Header("정보 텍스트")]
    [SerializeField] private TextMeshProUGUI playerGoldText;

    [Header("노티피케이션")]
    [SerializeField] private GameObject      notificationRoot;
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private float           notificationDuration = 2f;

    [Header("EXP 정보")]
    [SerializeField] private Slider          expSlider;
    [SerializeField] private TextMeshProUGUI expText;
    [SerializeField] private TextMeshProUGUI levelText;

    [Header("EXP 구매 롱프레스")]
    [SerializeField] private float buyExpHoldDelay    = 0.5f;   // 처음 반복까지 대기 시간
    [SerializeField] private float buyExpRepeatInterval = 0.12f; // 반복 구매 간격

    [Header("다음 웨이브 시작 딜레이")]
    [SerializeField] private float nextWaveDelay = 1f;

    [Header("판매 드롭존")]
    [SerializeField] private RectTransform sellImg;
    [SerializeField] private float         sellZoneHoverScale = 1.2f;

    [Header("장착 슬롯 (6개)")]
    [SerializeField] private EquipSlot[] equipSlots;

    [Header("인벤토리 슬롯 (10개)")]
    [SerializeField] private InvenSlot[] invenSlots;

    [Header("시너지 슬롯 컨테이너 (챕터별 4개, 배너 프리팹 인스턴스 부모)")]
    [SerializeField] private RectTransform[] synergySlotRoots = new RectTransform[4];

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

    [Header("초상화 연출")]
    [SerializeField] private Animator      portraitAnimator;

    // ── 내부 상태 ────────────────────────────────────────────────────
    private Coroutine          notifCoroutine;
    private Coroutine          autoStartCoroutine;
    private Coroutine          buyExpHoldCoroutine;
    private int                selectedInvenIndex  = -1;
    private int                selectedEquipIndex  = -1;
    private bool               isFirstWave         = false;
    private WeaponSynergyTag[] synergySlotTags = new WeaponSynergyTag[4];

    // ────────────────────────────────────────────────────────────────
    // 초기화
    // ────────────────────────────────────────────────────────────────
    private void Awake()
    {
        rerollButton? .onClick.AddListener(OnRerollClicked);
        nextWaveButton?.onClick.AddListener(OnNextWaveClicked);

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

        synergySlotTags = new WeaponSynergyTag[synergySlotRoots.Length];
        for (int i = 0; i < synergySlotRoots.Length; i++)
        {
            int idx = i;
            var root = synergySlotRoots[idx];
            if (root == null) continue;
            var btn = root.GetComponent<Button>() ?? root.GetComponentInParent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => OnSynergySlotClicked(idx));
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

    private void OnRerollClicked()
    {
        SoundManager.Shared?.Play(SoundKey.UI_Reroll);
        UpgradeManager.Shared?.TryReroll();
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

        if (PlayerInventory.Shared != null)
            PlayerInventory.Shared.OnChanged += RefreshInventoryUI;

        PlayerInventory.OnBeforeMerge += OnBeforeMerge;

        RefreshInventoryUI();
        RefreshExpUI();

        portraitAnimator?.Play("enter_portrait");
    }

    private void OnDisable()
    {
        if (autoStartCoroutine != null) StopCoroutine(autoStartCoroutine);
        if (notifCoroutine != null) { StopCoroutine(notifCoroutine); notifCoroutine = null; }
        if (buyExpHoldCoroutine != null) { StopCoroutine(buyExpHoldCoroutine); buyExpHoldCoroutine = null; }
        if (notificationRoot) notificationRoot.SetActive(false);
        if (PlayerInventory.Shared != null)
            PlayerInventory.Shared.OnChanged -= RefreshInventoryUI;

        PlayerInventory.OnBeforeMerge -= OnBeforeMerge;
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
        RefreshExpUI();
    }

    private void RefreshSynergySlots(int wave)
    {
        int chapter   = UpgradeManager.Shared?.CurrentChapter ?? 1;
        var synergies = SynergyHelper.GetChapterSynergies(chapter);
        var cfg       = WeaponVisualConfig.Shared;

        for (int i = 0; i < synergySlotRoots.Length; i++)
        {
            var root = synergySlotRoots[i];
            if (root == null) continue;

            var img = root.GetComponent<Image>() ?? root.GetComponentInChildren<Image>();

            if (i < synergies.Count)
            {
                var entry  = synergies[i];
                synergySlotTags[i] = entry.Tag;

                var sprite = cfg?.GetSynergyBanner(entry.Tag);
                if (img != null)
                {
                    img.sprite  = sprite;
                    img.enabled = sprite != null;
                }
            }
            else
            {
                synergySlotTags[i] = WeaponSynergyTag.None;
                if (img != null) img.enabled = false;
            }
        }
    }

    public void RefreshGold()
    {
        if (playerGoldText && PlayerStats.Shared != null)
            playerGoldText.text = $"{PlayerStats.Shared.Gold}";
    }

    public void RefreshExpUI()
    {
        var ps = PlayerStats.Shared;
        if (ps == null) return;
        int exp = ps.Exp;
        int level = ps.Level;
        int expToNext = 20 + (level - 1) * 50;

        if (levelText) levelText.text = $"LV.{level}";
        if (expSlider) expSlider.value = (float)exp / expToNext;
        if (expText) expText.text = $"{exp}/{expToNext}";
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
        for (int i = 0; i < synergySlotRoots.Length; i++)
        {
            var root = synergySlotRoots[i];
            if (root == null) continue;

            int  count      = SynergyHelper.CountEquippedTag(synergySlotTags[i]);
            bool isAchieved = count >= 1;

            var img = root.GetComponent<Image>() ?? root.GetComponentInChildren<Image>();
            if (img != null && img.enabled)
                img.color = isAchieved ? SynergyHelper.GetTierColor(count) : Color.white;

            var animator = root.GetComponent<Animator>() ?? root.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                if (isAchieved)
                {
                    animator.enabled = true;
                    var ctrl = animator.runtimeAnimatorController;
                    animator.runtimeAnimatorController = null;
                    animator.runtimeAnimatorController = ctrl;
                }
                else
                {
                    animator.enabled = false;
                }
            }
        }

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
    /// <summary>스크린 좌표 기준으로 겹치는 EquipSlot 반환 (raycastTarget 무관)</summary>
    public EquipSlot GetEquipSlotAtScreenPos(Vector2 screenPos)
    {
        foreach (var slot in equipSlots)
        {
            if (slot == null) continue;
            var rt = slot.GetComponent<RectTransform>();
            if (rt == null) continue;
            var canvas = slot.GetComponentInParent<Canvas>();
            if (canvas != null) canvas = canvas.rootCanvas;
            Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? canvas.worldCamera : null;
            if (RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, cam))
                return slot;
        }
        return null;
    }

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
        portraitAnimator?.Play("exit_portrait");
        yield return new WaitForSecondsRealtime(nextWaveDelay);
        UpgradeManager.Shared?.ClosePanel();
    }

    // ────────────────────────────────────────────────────────────────
    // 합성 슈루룩 애니메이션
    // ────────────────────────────────────────────────────────────────
    private void OnBeforeMerge(PlayerInventory.MergeAnimInfo info)
    {
        if (!gameObject.activeInHierarchy) return;
        if (coinImageSource == null || rootCanvas == null) return;
        StartCoroutine(MergeAnimRoutine(info));
    }

    private IEnumerator MergeAnimRoutine(PlayerInventory.MergeAnimInfo info)
    {
        const float spreadDuration   = 0.18f;
        const float convergeDuration = 0.38f;
        const float staggerDelay     = 0.07f;

        Camera cam = rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera : null;
        var parentRt = (RectTransform)rootCanvas.transform;

        // 목적지 슬롯 위치
        RectTransform destRt = null;
        if (info.destEquipSlot >= 0 && info.destEquipSlot < equipSlots.Length)
            destRt = equipSlots[info.destEquipSlot].GetComponent<RectTransform>();
        else if (info.destInvenSlot >= 0 && info.destInvenSlot < invenSlots.Length)
            destRt = invenSlots[info.destInvenSlot].GetComponent<RectTransform>();
        else if (invenSlots.Length > 0)
            destRt = invenSlots[invenSlots.Length - 1].GetComponent<RectTransform>();
        if (destRt == null) yield break;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRt,
            RectTransformUtility.WorldToScreenPoint(cam, destRt.position),
            cam, out Vector2 destLocal);

        // 소스 슬롯 목록 수집 (위치 + 크기 + 무기 데이터)
        var sources  = new List<Vector2>();
        var srcSizes = new List<Vector2>();
        var consumed = info.consumed ?? new List<WeaponData>();

        foreach (int i in info.invenIndices)
            if (i < invenSlots.Length)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRt,
                    RectTransformUtility.WorldToScreenPoint(cam, invenSlots[i].transform.position),
                    cam, out Vector2 p);
                sources.Add(p);
                srcSizes.Add(((RectTransform)invenSlots[i].transform).rect.size);
            }
        foreach (int i in info.equipIndices)
            if (i < equipSlots.Length)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRt,
                    RectTransformUtility.WorldToScreenPoint(cam, equipSlots[i].transform.position),
                    cam, out Vector2 p);
                sources.Add(p);
                srcSizes.Add(((RectTransform)equipSlots[i].transform).rect.size);
            }

        // 소스별 3-아이콘 버스트 발사
        for (int s = 0; s < sources.Count; s++)
        {
            var weaponData = s < consumed.Count ? consumed[s] : null;
            var slotSize   = s < srcSizes.Count ? srcSizes[s] : new Vector2(80f, 80f);
            StartCoroutine(MergeIconBurstRoutine(
                sources[s], destLocal, weaponData, slotSize,
                spreadDuration, convergeDuration, s * staggerDelay));
        }

        yield return new WaitForSecondsRealtime(spreadDuration + convergeDuration + sources.Count * staggerDelay);
    }

    /// <summary>한 소스 슬롯의 weaponIcon·synergyIcon·typeIcon 3개가 바깥으로 벌어졌다가 dest로 빨려들어갑니다.</summary>
    private IEnumerator MergeIconBurstRoutine(
        Vector2 slotPos, Vector2 dest, WeaponData data, Vector2 slotSize,
        float spreadDuration, float convergeDuration, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        var cfg = WeaponVisualConfig.Shared;
        // (sprite, sizeDelta)
        var iconDefs = new (Sprite sprite, Vector2 size)[]
        {
            (data?.icon,                                                     slotSize),
            (data != null ? cfg?.GetSynergySprite(data.tag1)    : null,     slotSize * 0.45f),
            (data != null ? cfg?.GetTypeSprite(data.weaponType) : null,     slotSize * 0.45f),
        };

        // 3방향 벌어짐 각도 (120° 간격, 위쪽부터)
        var ghosts = new List<(RectTransform rt, CanvasGroup cg, Vector2 spreadPos)>();
        for (int i = 0; i < iconDefs.Length; i++)
        {
            var (sprite, size) = iconDefs[i];
            if (sprite == null) continue;

            float angle      = (90f + 120f * i) * Mathf.Deg2Rad;
            float spreadDist = slotSize.x * 0.75f;
            Vector2 spreadPos = slotPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spreadDist;

            var go = new GameObject("MergeGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(rootCanvas.transform, false);
            go.transform.SetAsLastSibling();

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.color = Color.white;

            var cg = go.GetComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.alpha = 1f;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = slotPos;
            rt.localScale = Vector3.one * 0.8f;

            ghosts.Add((rt, cg, spreadPos));
        }

        if (ghosts.Count == 0) yield break;

        // Phase 1: 바깥으로 벌어짐
        float elapsed = 0f;
        while (elapsed < spreadDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t    = Mathf.Clamp01(elapsed / spreadDuration);
            float ease = 1f - (1f - t) * (1f - t); // ease-out
            foreach (var (rt, cg, spreadPos) in ghosts)
            {
                rt.anchoredPosition = Vector2.LerpUnclamped(slotPos, spreadPos, ease);
                rt.localScale       = Vector3.one * Mathf.Lerp(0.8f, 1.1f, ease);
            }
            yield return null;
        }

        // Phase 2: 목적지로 빨려들어감 (거의 정 사이즈 유지)
        elapsed = 0f;
        var spreadPositions = ghosts.ConvertAll(g => g.spreadPos);
        while (elapsed < convergeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t    = Mathf.Clamp01(elapsed / convergeDuration);
            float ease = t * t * (3f - 2f * t); // smoothstep
            float alpha = t > 0.8f ? Mathf.Lerp(1f, 0f, (t - 0.8f) / 0.2f) : 1f;
            for (int i = 0; i < ghosts.Count; i++)
            {
                var (rt, cg, _) = ghosts[i];
                rt.anchoredPosition = Vector2.LerpUnclamped(spreadPositions[i], dest, ease);
                rt.localScale       = Vector3.one * Mathf.Lerp(1.1f, 0.95f, ease);
                cg.alpha            = alpha;
            }
            yield return null;
        }

        foreach (var (rt, _, _) in ghosts)
            if (rt != null) Destroy(rt.gameObject);
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
