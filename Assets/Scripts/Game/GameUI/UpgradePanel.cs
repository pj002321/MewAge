using System.Collections;
using System.Collections.Generic;
using Coffee.UIExtensions;
using Mewage.DB;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
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
    [SerializeField] private Button          infoButton;
    [SerializeField] private Button          eraWeaponInfoButton;

    [Header("시대별 무기 정보 팝업")]
    [SerializeField] private EraWeaponInfoPopup eraWeaponInfoPopup;

    [Header("정보 텍스트")]
    [SerializeField] private TextMeshProUGUI playerGoldText;
    [SerializeField] private TextMeshProUGUI playerAtkText;
    [SerializeField] private TextMeshProUGUI playerCrtText;

    [Header("노티피케이션")]
    [SerializeField] private GameObject      notificationRoot;
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private float           notificationDuration = 2f;

    [Header("EXP 정보")]
    [SerializeField] private Slider          expSlider;
    [SerializeField] private TextMeshProUGUI expText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI expGainText;  // 한 번에 얻는 EXP량 (+30)

    [Header("EXP 구매 롱프레스")]
    [SerializeField] private float buyExpHoldDelay    = 0.5f;   // 처음 반복까지 대기 시간
    [SerializeField] private float buyExpRepeatInterval = 0.12f; // 반복 구매 간격

    [Header("다음 웨이브 시작 딜레이")]
    [SerializeField] private float nextWaveDelay = 1f;

    [Header("판매 드롭존")]
    [SerializeField] private RectTransform    sellImg;
    [SerializeField] private TextMeshProUGUI  sellPriceText;
    [SerializeField] private float            sellZoneHoverScale = 1.2f;

    [Header("장착 슬롯 (WeaponManager 앵커 순서와 동일: 우상단·우하단·좌상단·좌하단·우·좌)")]
    [SerializeField] private EquipSlot equipSlotTopRight;
    [SerializeField] private EquipSlot equipSlotBottomRight;
    [SerializeField] private EquipSlot equipSlotTopLeft;
    [SerializeField] private EquipSlot equipSlotBottomLeft;
    [SerializeField] private EquipSlot equipSlotRight;
    [SerializeField] private EquipSlot equipSlotLeft;

    private EquipSlot[] equipSlots;

    [Header("인벤토리 슬롯 (10개)")]
    [SerializeField] private InvenSlot[] invenSlots;

    [Header("등급 확률 텍스트 (일반/고급/희귀/전설/신화)")]
    [SerializeField] private TextMeshProUGUI normalProbText;
    [SerializeField] private TextMeshProUGUI rareProbText;
    [FormerlySerializedAs("eliteProbText")]
    [SerializeField] private TextMeshProUGUI epicProbText;
    [FormerlySerializedAs("epicProbText")]
    [SerializeField] private TextMeshProUGUI legendaryProbText;
    [FormerlySerializedAs("legendProbText")]
    [SerializeField] private TextMeshProUGUI mythicProbText;

    [Header("그랜드 머지 연출")]
    [SerializeField] private RectTransform grandMergeCenterAnchor; // 인스펙터에서 화면 중앙 RectTransform 연결

    [Header("코인 날아가기 이펙트")]
    [SerializeField] private Image         coinImageSource;  // 복사할 코인 Image 컴포넌트
    [SerializeField] private RectTransform goldTextRect;     // 골드 텍스트 위치 (목적지)
    [SerializeField] private Canvas        rootCanvas;


    [Header("시너지 노티피케이션")]
    [SerializeField] private Image infoButtonRedDot;
    [SerializeField] private NewSynergyNoti newSynergyNoti; // 신규시너지 달성 시에만 활성화 애니메이션 시간 끝나면 비활성화. 처음에는 비활성화
    [SerializeField] private Transform synergyCompleteContetns; // completeSlot이 자식으로 들어갈 곳.
    [SerializeField] private GameObject synergyCompleteSlot;    // 자식에 있는 이미지를 이용하여 현재 획득한 시너지 목록을 슬롯으로 표기

    // ── 내부 상태 ────────────────────────────────────────────────────
    private UIShiny            _infoButtonShiny;
    private Coroutine          notifCoroutine;
    private Coroutine          autoStartCoroutine;
    private Coroutine          buyExpHoldCoroutine;
    private Coroutine          atkTextCoroutine;
    private Coroutine          crtTextCoroutine;
    private Vector2            _atkTextOriginPos;
    private Vector2            _crtTextOriginPos;
    private int                selectedInvenIndex  = -1;
    private int                selectedEquipIndex  = -1;
    private bool               isFirstWave         = false;
    private float              _lastDisplayedAtk   = -1f;
    private float              _lastDisplayedCrt   = -1f;
    private float              _atkBeforeMerge     = -1f;
    private float              _mergeDestAtk       = -1f; // 합성 목적지 슬롯의 합성 전 ATK
    private int                _mergeDestEquipSlot = -1;
    private int                _mergeDestInvenSlot = -1;
    private bool               _refreshing         = false;
    private bool               _mergeAnimPlaying   = false;
    private readonly Queue<PlayerInventory.MergeAnimInfo> _mergeQueue = new Queue<PlayerInventory.MergeAnimInfo>();

    // ────────────────────────────────────────────────────────────────
    // 초기화
    // ────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (playerAtkText != null) _atkTextOriginPos = ((RectTransform)playerAtkText.transform).anchoredPosition;
        if (playerCrtText != null) _crtTextOriginPos = ((RectTransform)playerCrtText.transform).anchoredPosition;

        equipSlots = new[]
        {
            equipSlotTopRight, equipSlotBottomRight,
            equipSlotTopLeft,  equipSlotBottomLeft,
            equipSlotRight,    equipSlotLeft,
        };

        rerollButton? .onClick.AddListener(OnRerollClicked);
        nextWaveButton?.onClick.AddListener(OnNextWaveClicked);
        infoButton?   .onClick.AddListener(OnInfoClicked);
        _infoButtonShiny = infoButton?.GetComponent<UIShiny>();
        if (_infoButtonShiny != null) _infoButtonShiny.enabled = false;
        if (infoButtonRedDot != null) infoButtonRedDot.gameObject.SetActive(false);
        eraWeaponInfoButton?.onClick.AddListener(() =>
            eraWeaponInfoPopup?.Open(UpgradeManager.Shared?.CurrentChapter ?? 1));

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
        // 씬에 SynergyEffectManager가 없으면 자동 생성
        if (SynergyEffectManager.Shared == null)
            SynergyEffectManager.Create("SynergyEffectManager");

        SynergyEffectManager.OnNewSynergyAchieved += OnNewSynergyAchieved;
        SynergyEffectManager.OnAchievedChanged    += OnAchievedChanged;
        foreach (var anim in GetComponentsInChildren<Animator>(true))
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;

        selectedInvenIndex  = -1;
        selectedEquipIndex  = -1;
        WeaponInfoPanel.Shared?.Hide();
        if (sellImg) { sellImg.localScale = Vector3.one; sellImg.gameObject.SetActive(false); }

        if (PlayerInventory.Shared != null)
            PlayerInventory.Shared.OnChanged += RefreshInventoryUI;

        PlayerInventory.OnBeforeMerge += OnBeforeMerge;

        GameEvents.OnGoldChanged += HandleGoldChanged;
        GameEvents.OnExpChanged  += HandleExpChanged;

        RefreshInventoryUI();
        RefreshSynergyCompleteSlots();
        RefreshExpUI();

        InGameCharacterDisplay.Shared?.DropIn();
        _lastDisplayedAtk = -1f;
        _lastDisplayedCrt = -1f;
        _atkBeforeMerge   = -1f;
        RefreshAtkUI(animate: false);
        RefreshCrtUI(animate: false);

        // 상점 열릴 때 기존 합성 가능한 쌍이 있으면 자동 합성 (구독 완료 후 실행해야 애니 이벤트 수신 가능)
        UpgradeManager.Shared?.TryAutoMergeAll();
    }

    private void OnDisable()
    {
        SynergyEffectManager.OnNewSynergyAchieved -= OnNewSynergyAchieved;
        SynergyEffectManager.OnAchievedChanged    -= OnAchievedChanged;
        if (autoStartCoroutine != null) StopCoroutine(autoStartCoroutine);
        if (notifCoroutine != null) { StopCoroutine(notifCoroutine); notifCoroutine = null; }
        if (buyExpHoldCoroutine != null) { StopCoroutine(buyExpHoldCoroutine); buyExpHoldCoroutine = null; }
        if (atkTextCoroutine != null) { StopCoroutine(atkTextCoroutine); atkTextCoroutine = null; ResetTextState(playerAtkText); }
        if (crtTextCoroutine != null) { StopCoroutine(crtTextCoroutine); crtTextCoroutine = null; ResetTextState(playerCrtText); }
        if (notificationRoot) notificationRoot.SetActive(false);
        if (PlayerInventory.Shared != null)
            PlayerInventory.Shared.OnChanged -= RefreshInventoryUI;

        PlayerInventory.OnBeforeMerge -= OnBeforeMerge;
        _mergeQueue.Clear();
        _mergeAnimPlaying = false;

        GameEvents.OnGoldChanged -= HandleGoldChanged;
        GameEvents.OnExpChanged  -= HandleExpChanged;
    }

    // ────────────────────────────────────────────────────────────────
    // 카드 갱신 (UpgradeManager가 호출)
    // ────────────────────────────────────────────────────────────────
    public void Refresh(WeaponData[] drawnCards, int rerollPrice, int buyExpPrice,
                        int ownedCount, int maxOwned, int wave,
                        float pNormal = 0f, float pRare = 0f, float pEpic = 0f,
                        float pLegendary = 0f, float pMythic = 0f, WeaponData adCardData = null,
                        bool[] isSold = null, bool isAdSold = false)
    {
        isFirstWave = (wave == 0);

        for (int i = 0; i < cards.Length; i++)
        {
            bool isAdSlot = (i == cards.Length - 1);
            if (isAdSlot)
            {
                if (adCardData != null)
                    cards[i].SetupAdCard(adCardData, i, () => UpgradeManager.Shared?.TryClaimAdCard(), isAdSold);
                else
                    cards[i].SetEmpty();
            }
            else if (i < drawnCards.Length && drawnCards[i] != null)
            {
                bool sold = (isSold != null && i < isSold.Length) ? isSold[i] : false;
                cards[i].Setup(drawnCards[i], i, sold);
            }
            else
            {
                cards[i].SetEmpty();
            }
        }

        // 첫 웨이브(wave=0)는 리롤 금지
        if (rerollButton) rerollButton.interactable = !isFirstWave;

        // 합성 애니 중에는 Setup()이 buyButton.interactable=true로 덮어쓰므로 재잠금
        if (_mergeAnimPlaying) SetCardsInteractable(false);

        // 등급 확률 텍스트
        if (normalProbText)    normalProbText.text    = $"{pNormal:0.#}%";
        if (rareProbText)      rareProbText.text      = $"{pRare:0.#}%";
        if (epicProbText)    { epicProbText.text      = $"{pEpic:0.#}%";      epicProbText.color    = WeaponGradeColors.Get(WeaponGrade.Epic); }
        if (legendaryProbText) { legendaryProbText.text = $"{pLegendary:0.#}%"; legendaryProbText.color = WeaponGradeColors.Get(WeaponGrade.Legendary); }
        if (mythicProbText)  { mythicProbText.text   = $"{pMythic:0.#}%";    mythicProbText.color  = WeaponGradeColors.Get(WeaponGrade.Mythic); }

        RefreshGold();
        RefreshInventoryUI();
        RefreshExpUI();
    }

    public void RefreshGold()
    {
        if (playerGoldText && PlayerStats.Shared != null)
            playerGoldText.text = $"{PlayerStats.Shared.Gold}";
    }

    private void HandleGoldChanged(int _) => RefreshGold();
    private void HandleExpChanged(int _, int __, int ___) => RefreshExpUI();

    public void RefreshAtkUI(bool animate = true)
    {
        if (playerAtkText == null) return;
        float atk = CalcTotalAtk();

        if (!animate || _lastDisplayedAtk < 0f)
        {
            _lastDisplayedAtk   = atk;
            _atkBeforeMerge     = -1f;
            _mergeDestAtk       = -1f;
            _mergeDestEquipSlot = -1;
            _mergeDestInvenSlot = -1;
            playerAtkText.gameObject.SetActive(false);
            return;
        }

        float from, to;
        if (_mergeDestAtk >= 0f)
        {
            // 합성 모드: 목적지 슬롯의 전/후 ATK를 비교
            var inv = PlayerInventory.Shared;
            WeaponData destWeapon =
                _mergeDestEquipSlot >= 0 && inv?.Equipped.Count > _mergeDestEquipSlot
                    ? inv.Equipped[_mergeDestEquipSlot]
                    : (_mergeDestInvenSlot >= 0 && inv?.Inventory.Count > _mergeDestInvenSlot
                        ? inv.Inventory[_mergeDestInvenSlot]
                        : null);
            from = _mergeDestAtk;
            to   = destWeapon != null ? CalcSingleWeaponAtk(destWeapon) : atk;
            _mergeDestAtk       = -1f;
            _mergeDestEquipSlot = -1;
            _mergeDestInvenSlot = -1;
        }
        else
        {
            from = (_atkBeforeMerge >= 0f) ? _atkBeforeMerge : _lastDisplayedAtk;
            to   = atk;
        }
        _atkBeforeMerge = -1f;

        if (Mathf.Approximately(to, from) && Mathf.Approximately(atk, _lastDisplayedAtk)) return;

        if (atkTextCoroutine != null) StopCoroutine(atkTextCoroutine);
        atkTextCoroutine = StartCoroutine(AtkRollRoutine(from, to));
        _lastDisplayedAtk = atk;
    }

    private float CalcTotalAtk()
    {
        var pc = PlayerController.Instance;
        float playerAtk = pc?.Attack ?? PlayerStatManager.Shared?.GetFinal(StatType.ATK) ?? 0f;

        var equipped = PlayerInventory.Shared?.Equipped;
        if (equipped == null || equipped.Count == 0) return playerAtk;

        float total = 0f;
        foreach (var w in equipped)
        {
            if (w == null) continue;
            float dmg = w.attackDamage + playerAtk;
            if (pc != null)
            {
                bool isMelee = w.weaponType == WeaponType.Melee || w.weaponType == WeaponType.Spin;
                dmg += isMelee ? pc.MeleeAttackBonus : pc.RangedAttackBonus;
            }
            total += dmg;
        }
        return total;
    }

    private float CalcSingleWeaponAtk(WeaponData w)
    {
        if (w == null) return 0f;
        var pc = PlayerController.Instance;
        float playerAtk = pc?.Attack ?? PlayerStatManager.Shared?.GetFinal(StatType.ATK) ?? 0f;
        float dmg = w.attackDamage + playerAtk;
        if (pc != null)
        {
            bool isMelee = w.weaponType == WeaponType.Melee || w.weaponType == WeaponType.Spin;
            dmg += isMelee ? pc.MeleeAttackBonus : pc.RangedAttackBonus;
        }
        return dmg;
    }

    private IEnumerator AtkRollRoutine(float from, float to)
    {
        bool increase = to > from;
        playerAtkText.color = increase ? Color.green : Color.red;
        playerAtkText.gameObject.SetActive(true);

        var cg = playerAtkText.GetComponent<CanvasGroup>()
              ?? playerAtkText.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        var rt = (RectTransform)playerAtkText.transform;
        rt.anchoredPosition = _atkTextOriginPos;

        const float rollDuration = 0.4f;
        float elapsed = 0f;
        while (elapsed < rollDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            playerAtkText.text = $"{Random.Range(Mathf.Min(from, to), Mathf.Max(from, to)):0}";
            yield return null;
        }
        playerAtkText.text = $"{to:0}";

        const float floatDuration = 0.8f;
        const float risePixels    = 55f;
        elapsed = 0f;
        while (elapsed < floatDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / floatDuration);
            rt.anchoredPosition = _atkTextOriginPos + Vector2.up * (risePixels * t);
            cg.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }

        cg.alpha = 1f;
        rt.anchoredPosition = _atkTextOriginPos;
        playerAtkText.gameObject.SetActive(false);
        atkTextCoroutine = null;
    }

    public void RefreshCrtUI(bool animate = true)
    {
        if (playerCrtText == null) return;
        float crt = CalcTotalCrit();

        if (!animate || _lastDisplayedCrt < 0f)
        {
            _lastDisplayedCrt = crt;
            playerCrtText.gameObject.SetActive(false);
            return;
        }

        float delta = crt - _lastDisplayedCrt;
        if (Mathf.Abs(delta) < 0.01f) return;

        if (crtTextCoroutine != null) StopCoroutine(crtTextCoroutine);
        crtTextCoroutine = StartCoroutine(CrtFloatRoutine(delta));
        _lastDisplayedCrt = crt;
    }

    private float CalcTotalCrit()
    {
        float total = 0f;
        var equipped = PlayerInventory.Shared?.Equipped;
        if (equipped != null)
            foreach (var w in equipped)
                if (w != null) total += (w.criticalDamage - w.baseCriticalDamage) * 100f;
        return total;
    }

    private IEnumerator CrtFloatRoutine(float delta)
    {
        bool increase = delta > 0f;
        string sign = increase ? "+" : "";
        playerCrtText.text  = $"{sign}{delta:0.#}%";
        playerCrtText.color = increase ? Color.green : Color.red;
        playerCrtText.gameObject.SetActive(true);

        var cg = playerCrtText.GetComponent<CanvasGroup>()
              ?? playerCrtText.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 1f;

        var rt = (RectTransform)playerCrtText.transform;
        rt.anchoredPosition = _crtTextOriginPos;

        const float floatDuration = 0.8f;
        const float risePixels    = 55f;
        float elapsed = 0f;
        while (elapsed < floatDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / floatDuration);
            rt.anchoredPosition = _crtTextOriginPos + Vector2.up * (risePixels * t);
            cg.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }

        cg.alpha = 1f;
        rt.anchoredPosition = _crtTextOriginPos;
        playerCrtText.gameObject.SetActive(false);
        crtTextCoroutine = null;
    }

    private static void ResetTextState(TextMeshProUGUI text)
    {
        if (text == null) return;
        var cg = text.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;
        text.gameObject.SetActive(false);
    }

    public void RefreshExpUI()
    {
        var ps = PlayerStats.Shared;
        if (ps == null) return;
        int exp = ps.Exp;
        int level = ps.Level;
        int expToNext = PlayerStats.Shared?.ExpToNext ?? (20 + (level - 1) * 50);

        bool isMaxLevel = level >= (PlayerStats.Shared?.MaxLevel ?? int.MaxValue);
        if (levelText) levelText.text = isMaxLevel ? "MAX" : $"LV.{level}";
        if (expSlider) expSlider.value = isMaxLevel ? 1f : (float)exp / expToNext;
        if (expText) expText.text = isMaxLevel ? "MAX" : $"{exp}/{expToNext}";
        if (buyExpButton) buyExpButton.interactable = !isMaxLevel;

        if (expGainText)
        {
            int buyExpAmount = UpgradeManager.Shared?.BuyExpAmount ?? 30;
            expGainText.text = $"경험치 +{buyExpAmount}";
        }

        // 열린 서브패널 확률 텍스트 레벨 동기화
        if (SynergyInfoPanel.Shared != null && SynergyInfoPanel.Shared.gameObject.activeSelf)
            SynergyInfoPanel.Shared.RefreshProbTexts();
        if (eraWeaponInfoPopup != null && eraWeaponInfoPopup.gameObject.activeSelf)
            eraWeaponInfoPopup.RefreshProbTexts();
    }

    // ────────────────────────────────────────────────────────────────
    // 인벤토리 / 장착 슬롯 UI 갱신
    // ────────────────────────────────────────────────────────────────
    public void RefreshInventoryUI()
    {
        if (_refreshing) return;
        if (_mergeAnimPlaying) return;
        _refreshing = true;
        try { RefreshInventoryUIInternal(); }
        finally { _refreshing = false; }
    }

    private void RefreshInventoryUIInternal()
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
        RefreshAtkUI();
        RefreshCrtUI();
        SynergyEffectManager.Shared?.RecalculateSynergies();
    }

    private void OnInfoClicked()
    {
        if (_infoButtonShiny != null) _infoButtonShiny.enabled = false;
        if (infoButtonRedDot != null) infoButtonRedDot.gameObject.SetActive(false);
        int chapter = UpgradeManager.Shared?.CurrentChapter ?? 1;
        SynergyInfoPanel.Shared?.Show(chapter);
    }

    private void OnNewSynergyAchieved(IReadOnlyList<string> newIds)
    {
        if (_infoButtonShiny != null) _infoButtonShiny.enabled = true;
        if (infoButtonRedDot != null) infoButtonRedDot.gameObject.SetActive(true);

        if (newSynergyNoti == null || newIds == null || newIds.Count == 0) return;

        string id = newIds[0];
        DB_시너지 row = null;
        DB_시너지.ForEachEntity(r => { if (r?.f_시너지ID == id) row = r; });
        if (row == null) return;

        var icon = SynergyVisualConfig.Shared?.GetIcon(id)
                ?? Resources.Load<Sprite>($"Synergy/{id.ToLower()}");
        newSynergyNoti.Show(icon, row.f_이름);
    }

    private void OnAchievedChanged(HashSet<string> _) => RefreshSynergyCompleteSlots();

    private void RefreshSynergyCompleteSlots()
    {
        if (synergyCompleteContetns == null || synergyCompleteSlot == null) return;

        for (int i = synergyCompleteContetns.childCount - 1; i >= 0; i--)
            Destroy(synergyCompleteContetns.GetChild(i).gameObject);

        var achieved = SynergyEffectManager.AchievedSynergies;
        var contentsParent = synergyCompleteContetns.parent?.gameObject;
        if (achieved == null || achieved.Count == 0)
        {
            contentsParent?.SetActive(false);
            return;
        }

        contentsParent?.SetActive(true);

        var cfg = SynergyVisualConfig.Shared;
        foreach (var id in achieved)
        {
            var slot = Instantiate(synergyCompleteSlot, synergyCompleteContetns);
            var img  = slot.GetComponentInChildren<Image>();
            if (img == null) continue;
            var icon = cfg?.GetIcon(id) ?? Resources.Load<Sprite>($"Synergy/{id.ToLower()}");
            img.sprite  = icon;
            img.enabled = icon != null;
        }
    }

    // ────────────────────────────────────────────────────────────────
    // 인벤 슬롯 클릭 (InvenSlot이 호출)
    // ────────────────────────────────────────────────────────────────
    public void OnInvenSlotClicked(int slotIndex, Transform _)
    {
        var inv = PlayerInventory.Shared;
        if (inv == null) return;

        bool hasItem = slotIndex < inv.Inventory.Count && inv.Inventory[slotIndex] != null;

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
                    _atkBeforeMerge = CalcTotalAtk(); // 장착 전 ATK 스냅샷
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
                }),
                onClose: () =>
                {
                    selectedInvenIndex = -1;
                    RefreshInventoryUI();
                }
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
                    _atkBeforeMerge = CalcTotalAtk(); // 해제 전 ATK 스냅샷
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
                }),
                onClose: () =>
                {
                    selectedEquipIndex = -1;
                    RefreshInventoryUI();
                }
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

    public void NotifyDragBegin(WeaponData draggedWeapon = null)
    {
        selectedInvenIndex = -1;
        selectedEquipIndex = -1;
        WeaponInfoPanel.Shared?.Hide();
        if (nextWaveButton) nextWaveButton.interactable = false;
        if (sellImg) sellImg.gameObject.SetActive(true);
        if (sellPriceText)
            sellPriceText.text = draggedWeapon != null ? $"{Mathf.Max(1, draggedWeapon.price / 2)}" : "";
        RefreshInventoryUI();

        // 3성 무기 드래그 시 같은 이름의 3성 슬롯 하이라이트
        if (draggedWeapon != null && draggedWeapon.star >= 3)
        {
            string name = draggedWeapon.weaponName;
            var inv = PlayerInventory.Shared;
            foreach (var slot in equipSlots)
            {
                var w = (inv != null && slot.SlotIndex < inv.Equipped.Count)
                    ? inv.Equipped[slot.SlotIndex] : null;
                if (w != null && w.star >= 3 && w.weaponName == name)
                    slot.SetMergeHighlight(true);
            }
            foreach (var slot in invenSlots)
            {
                var w = (inv != null && slot.SlotIndex < inv.Inventory.Count)
                    ? inv.Inventory[slot.SlotIndex] : null;
                if (w != null && w.star >= 3 && w.weaponName == name)
                    slot.SetMergeHighlight(true);
            }
        }
    }

    public void NotifyDragHover(Vector2 screenPos)
    {
        if (sellImg == null || !sellImg.gameObject.activeSelf) return;
        Camera cam = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? rootCanvas.worldCamera : null;
        bool over = RectTransformUtility.RectangleContainsScreenPoint(sellImg, screenPos, cam);
        sellImg.localScale = Vector3.one * (over ? sellZoneHoverScale : 1f);
    }

    private void ClearMergeHighlights()
    {
        foreach (var slot in equipSlots) slot?.SetMergeHighlight(false);
        foreach (var slot in invenSlots) slot?.SetMergeHighlight(false);
    }

    /// <summary>드래그 종료. sellImg 위면 true 반환 (판매 실행은 슬롯 쪽에서 호출).</summary>
    public bool NotifyDragEnd(Vector2 screenPos)
    {
        ClearMergeHighlights();
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
    private bool HasOnlyOneWeapon()
    {
        var inv = PlayerInventory.Shared;
        if (inv == null) return false;
        return inv.Equipped.Count + inv.Inventory.Count <= 1;
    }

    private void DoSellInven(int slotIndex)
    {
        var inv = PlayerInventory.Shared;
        if (inv == null || slotIndex >= inv.Inventory.Count) return;

        if (HasOnlyOneWeapon())
        {
            ShowNotification("마지막 무기는 판매할 수 없습니다.");
            return;
        }

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

        if (HasOnlyOneWeapon())
        {
            ShowNotification("마지막 무기는 판매할 수 없습니다.");
            return;
        }

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
        InGameCharacterDisplay.Shared?.DropOut();
        yield return new WaitForSecondsRealtime(nextWaveDelay);
        UpgradeManager.Shared?.ClosePanel();
    }

    // ────────────────────────────────────────────────────────────────
    // 합성 슬램 애니메이션
    // ────────────────────────────────────────────────────────────────
    private void SetCardsInteractable(bool interactable)
    {
        foreach (var c in cards)
            if (c != null) c.SetBuyInteractable(interactable);
    }

    private void OnBeforeMerge(PlayerInventory.MergeAnimInfo info)
    {
        if (!gameObject.activeInHierarchy) return;
        if (rootCanvas == null) return;
        if (_mergeAnimPlaying)
        {
            _mergeQueue.Enqueue(info); // 재생 중이면 큐에 적재
            return;
        }
        _atkBeforeMerge     = CalcTotalAtk();
        _mergeDestEquipSlot = info.destEquipSlot;
        _mergeDestInvenSlot = info.destInvenSlot;
        var invSnap = PlayerInventory.Shared;
        _mergeDestAtk = info.destEquipSlot >= 0 && invSnap?.Equipped.Count > info.destEquipSlot
            ? CalcSingleWeaponAtk(invSnap.Equipped[info.destEquipSlot])
            : (info.destInvenSlot >= 0 && invSnap?.Inventory.Count > info.destInvenSlot
                ? CalcSingleWeaponAtk(invSnap.Inventory[info.destInvenSlot])
                : -1f);
        _mergeAnimPlaying = true;
        // 합성 애니 중 카드 구매 및 웨이브 시작 차단
        SetCardsInteractable(false);
        if (nextWaveButton) nextWaveButton.interactable = false;
        StartCoroutine(MergeAnimRoutine(info));
    }

    private IEnumerator MergeAnimRoutine(PlayerInventory.MergeAnimInfo info)
    {
        const float flyDuration   = 0.32f;
        const float punchDuration = 0.18f;
        const float stagger       = 0.06f;

        Camera cam      = rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
        var    parentRt = (RectTransform)rootCanvas.transform;

        // ── 연쇄 그랜드 머지를 ghost 발사 전에 먼저 감지 ────────────────
        // 목적지가 확정돼야 ghost들이 올바른 슬롯으로 날아간다.
        bool isChainGrand = false;
        PlayerInventory.MergeAnimInfo chainInfo = default;
        if (!info.isGrandMerge && _mergeQueue.Count > 0)
        {
            var peeked = _mergeQueue.Peek();
            bool destIsChainSource = info.destEquipSlot >= 0
                ? peeked.isGrandMerge && peeked.equipIndices.Contains(info.destEquipSlot)
                : peeked.isGrandMerge && peeked.invenIndices.Contains(info.destInvenSlot);
            if (destIsChainSource)
            {
                isChainGrand = true;
                chainInfo    = _mergeQueue.Dequeue();
            }
        }

        // 최종 목적지: 체인이면 체인의 결과 슬롯, 아니면 현재 합성의 결과 슬롯
        var reveal = isChainGrand ? chainInfo : info;
        RectTransform destRt = null;
        if (reveal.destEquipSlot >= 0 && reveal.destEquipSlot < equipSlots.Length)
            destRt = equipSlots[reveal.destEquipSlot].GetComponent<RectTransform>();
        else if (reveal.destInvenSlot >= 0 && reveal.destInvenSlot < invenSlots.Length)
            destRt = invenSlots[reveal.destInvenSlot].GetComponent<RectTransform>();

        if (destRt == null)
        {
            FinishMergeAnim();
            yield break;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRt, RectTransformUtility.WorldToScreenPoint(cam, destRt.position),
            cam, out Vector2 destLocal);

        // ── 소스 ghost 발사 (모두 최종 목적지로) ─────────────────────────
        var consumedIcons  = info.consumedIcons  ?? new List<Sprite>();
        var consumedGrades = info.consumedGrades ?? new List<WeaponGrade>();
        int dataIdx  = 0;
        int srcCount = 0;

        foreach (int i in info.invenIndices)
        {
            Sprite      icon  = dataIdx < consumedIcons.Count  ? consumedIcons[dataIdx]  : null;
            WeaponGrade grade = dataIdx < consumedGrades.Count ? consumedGrades[dataIdx] : WeaponGrade.Common;
            dataIdx++;
            if (i >= invenSlots.Length) continue;
            invenSlots[i].HideContentImages();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRt, RectTransformUtility.WorldToScreenPoint(cam, invenSlots[i].transform.position),
                cam, out Vector2 p);
            var sz = ((RectTransform)invenSlots[i].transform).rect.size;
            StartCoroutine(SlamRoutine(p, sz, destLocal, grade, icon, flyDuration, punchDuration, stagger * srcCount++));
        }
        foreach (int i in info.equipIndices)
        {
            Sprite      icon  = dataIdx < consumedIcons.Count  ? consumedIcons[dataIdx]  : null;
            WeaponGrade grade = dataIdx < consumedGrades.Count ? consumedGrades[dataIdx] : WeaponGrade.Common;
            dataIdx++;
            if (i >= equipSlots.Length) continue;
            equipSlots[i].HideContentImages();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRt, RectTransformUtility.WorldToScreenPoint(cam, equipSlots[i].transform.position),
                cam, out Vector2 p);
            var sz = ((RectTransform)equipSlots[i].transform).rect.size;
            StartCoroutine(SlamRoutine(p, sz, destLocal, grade, icon, flyDuration, punchDuration, stagger * srcCount++));
        }

        // ── 체인 그랜드 머지: 추가 소스 ghost ────────────────────────────
        if (isChainGrand)
        {
            int chainIdx = 0;
            foreach (int ci in chainInfo.invenIndices)
            {
                var cIcon  = chainIdx < chainInfo.consumedIcons.Count  ? chainInfo.consumedIcons[chainIdx]  : null;
                var cGrade = chainIdx < chainInfo.consumedGrades.Count ? chainInfo.consumedGrades[chainIdx] : WeaponGrade.Common;
                chainIdx++;
                if (ci == info.destInvenSlot) continue; // merge1 dest → 이미 위에서 처리됨
                if (ci >= invenSlots.Length) continue;
                invenSlots[ci].HideContentImages();
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRt, RectTransformUtility.WorldToScreenPoint(cam, invenSlots[ci].transform.position),
                    cam, out Vector2 cp);
                var csz = ((RectTransform)invenSlots[ci].transform).rect.size;
                StartCoroutine(SlamRoutine(cp, csz, destLocal, cGrade, cIcon, flyDuration, punchDuration, stagger * srcCount++));
            }
            foreach (int ci in chainInfo.equipIndices)
            {
                var cIcon  = chainIdx < chainInfo.consumedIcons.Count  ? chainInfo.consumedIcons[chainIdx]  : null;
                var cGrade = chainIdx < chainInfo.consumedGrades.Count ? chainInfo.consumedGrades[chainIdx] : WeaponGrade.Common;
                chainIdx++;
                if (info.destEquipSlot >= 0 && ci == info.destEquipSlot) continue;
                if (ci >= equipSlots.Length) continue;
                equipSlots[ci].HideContentImages();
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRt, RectTransformUtility.WorldToScreenPoint(cam, equipSlots[ci].transform.position),
                    cam, out Vector2 cp);
                var csz = ((RectTransform)equipSlots[ci].transform).rect.size;
                StartCoroutine(SlamRoutine(cp, csz, destLocal, cGrade, cIcon, flyDuration, punchDuration, stagger * srcCount++));
            }
        }

        if (srcCount == 0)
        {
            FinishMergeAnim();
            yield break;
        }

        // 마지막 ghost가 착지 후 펀치까지 완전히 끝날 때까지 대기
        yield return new WaitForSecondsRealtime(flyDuration + punchDuration + stagger * (srcCount - 1));

        // ── 그랜드 머지: 룰렛 + 화면 중앙 이동 연출 ──────────────────────
        bool isGrand = info.isGrandMerge || isChainGrand;
        if (isGrand && grandMergeCenterAnchor != null)
        {
            yield return StartCoroutine(GrandMergeRevealRoutine(destRt, destLocal, reveal));
        }

        // 결과 표시
        if (_mergeQueue.Count > 0)
            ShowSlotIntermediate(reveal.destEquipSlot, reveal.destInvenSlot, reveal.resultIcon, reveal.resultGrade, reveal.resultStar);
        else
            RefreshInventoryUI();
        StartCoroutine(PulseSlotRoutine(destRt));

        if (!isGrand)
            SoundManager.Shared?.Play(SoundKey.UI_Merge);

        if (reveal.destEquipSlot >= 0 && reveal.destEquipSlot < equipSlots.Length)
            equipSlots[reveal.destEquipSlot].PlayMergeShiny();
        else if (reveal.destInvenSlot >= 0 && reveal.destInvenSlot < invenSlots.Length)
            invenSlots[reveal.destInvenSlot].PlayMergeShiny();

        // 큐에 다음 합성이 있으면 — 그랜드 머지 결과가 보이고 나서 실행
        if (_mergeQueue.Count > 0)
        {
            if (isGrand)
                yield return new WaitForSecondsRealtime(0.6f);
            var next = _mergeQueue.Dequeue();
            _atkBeforeMerge = CalcTotalAtk();
            StartCoroutine(MergeAnimRoutine(next));
            yield break;
        }

        _mergeAnimPlaying = false;
        SetCardsInteractable(true);
        if (nextWaveButton) nextWaveButton.interactable = true;
        RefreshInventoryUI();
    }

    /// <summary>모든 yield break 경로에서 공통으로 호출해 _mergeAnimPlaying을 리셋합니다.</summary>
    private void FinishMergeAnim()
    {
        if (_mergeQueue.Count > 0)
        {
            var next = _mergeQueue.Dequeue();
            _atkBeforeMerge = CalcTotalAtk();
            StartCoroutine(MergeAnimRoutine(next));
            return;
        }
        _mergeAnimPlaying = false;
        SetCardsInteractable(true);
        if (nextWaveButton) nextWaveButton.interactable = true;
        RefreshInventoryUI();
    }

    /// <summary>연속 합성 중 목적지 슬롯 하나만 중간 결과로 업데이트합니다.
    /// resultWeapon이 후속 합성에 의해 Destroy됐을 수 있으므로 사전 캡처된 값을 사용합니다.</summary>
    private void ShowSlotIntermediate(int destEquipSlot, int destInvenSlot, Sprite icon, WeaponGrade grade, int star)
    {
        if (destEquipSlot >= 0 && destEquipSlot < equipSlots.Length)
            equipSlots[destEquipSlot].RefreshCaptured(icon, grade, star);
        else if (destInvenSlot >= 0 && destInvenSlot < invenSlots.Length)
            invenSlots[destInvenSlot].RefreshCaptured(icon, grade, star);
    }

    /// <summary>소스 슬롯 고스트가 목적지로 날아가 박히는 슬램 연출.</summary>
    private IEnumerator SlamRoutine(Vector2 srcLocal, Vector2 srcSize, Vector2 destLocal,
        WeaponGrade grade, Sprite icon, float flyDuration, float punchDuration, float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        var go = new GameObject("SlamGhost", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(rootCanvas.transform, false);
        go.transform.SetAsLastSibling();

        var mainRt = go.GetComponent<RectTransform>();
        mainRt.anchoredPosition = srcLocal;
        mainRt.sizeDelta        = srcSize;

        var cg = go.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        // 프레임
        var frameGo = new GameObject("F", typeof(RectTransform), typeof(Image));
        frameGo.transform.SetParent(go.transform, false);
        var fRt = frameGo.GetComponent<RectTransform>();
        fRt.anchorMin = fRt.anchorMax = new Vector2(0.5f, 0.5f);
        fRt.sizeDelta = srcSize;
        var fImg = frameGo.GetComponent<Image>();
        fImg.sprite        = WeaponVisualConfig.Shared?.GetGradeSprite(grade);
        fImg.type          = Image.Type.Simple;
        fImg.raycastTarget = false;

        // 아이콘 (캡처된 sprite 사용 — Destroy 이후에도 유효)
        if (icon != null)
        {
            var iconGo = new GameObject("I", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iRt = iconGo.GetComponent<RectTransform>();
            iRt.anchorMin = iRt.anchorMax = new Vector2(0.5f, 0.5f);
            iRt.sizeDelta = srcSize * 0.75f;
            var iImg = iconGo.GetComponent<Image>();
            iImg.sprite        = icon;
            iImg.raycastTarget = false;
        }

        // 포물선 호: 가까운 슬롯도 이동감이 느껴지도록 최소 호 높이 보장
        float dist = Vector2.Distance(srcLocal, destLocal);
        float arcH = Mathf.Max(80f, dist * 0.4f);
        Vector2 ctrl = (srcLocal + destLocal) * 0.5f + Vector2.up * arcH;

        // ease-in 비행 (2차 베지어)
        float elapsed = 0f;
        while (elapsed < flyDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = Mathf.Clamp01(elapsed / flyDuration);
            float et = t * t; // ease-in
            float u  = 1f - et;
            Vector2 pos = u * u * srcLocal + 2f * u * et * ctrl + et * et * destLocal;
            mainRt.anchoredPosition = pos;
            mainRt.localScale       = Vector3.one * Mathf.Lerp(1f, 1.15f, et);
            yield return null;
        }
        mainRt.anchoredPosition = destLocal;

        // 박힘 펀치: 커졌다가 사라짐
        elapsed = 0f;
        while (elapsed < punchDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / punchDuration;
            float s = t < 0.35f
                ? Mathf.Lerp(1.15f, 1.4f,  t / 0.35f)
                : Mathf.Lerp(1.4f,  0f,   (t - 0.35f) / 0.65f);
            cg.alpha          = t < 0.35f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.35f) / 0.65f);
            mainRt.localScale = Vector3.one * s;
            yield return null;
        }

        Destroy(go);
    }

    /// <summary>슬롯 RectTransform에 스케일 펄스 효과를 줍니다.</summary>
    private IEnumerator PulseSlotRoutine(RectTransform slotRt)
    {
        if (slotRt == null) yield break;
        Vector3 orig = slotRt.localScale;
        const float dur = 0.25f;
        float elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / dur;
            float s = t < 0.4f
                ? Mathf.Lerp(1f, 1.3f, t / 0.4f)
                : Mathf.Lerp(1.3f, 1f, (t - 0.4f) / 0.6f);
            slotRt.localScale = orig * s;
            yield return null;
        }
        slotRt.localScale = orig;
    }

    // ────────────────────────────────────────────────────────────────
    // 그랜드 머지 룰렛 연출
    // ────────────────────────────────────────────────────────────────

    /// <summary>그랜드 머지 결과 룰렛 → 화면 중앙 이동 → 반환. 더블업 시 추가 연출 포함.</summary>
    private IEnumerator GrandMergeRevealRoutine(RectTransform destRt, Vector2 destLocal,
        PlayerInventory.MergeAnimInfo info)
    {
        Camera cam      = rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
        var    parentRt = (RectTransform)rootCanvas.transform;

        // ── 1. 목적지 슬롯 IconImage에 직접 룰렛 (슬롯 스케일 유지) ──────
        Image destIconImg = null;
        if (info.destEquipSlot >= 0 && info.destEquipSlot < equipSlots.Length)
            destIconImg = equipSlots[info.destEquipSlot].IconImage;
        else if (info.destInvenSlot >= 0 && info.destInvenSlot < invenSlots.Length)
            destIconImg = invenSlots[info.destInvenSlot].IconImage;

        WeaponGrade baseGrade = info.consumedGrades != null && info.consumedGrades.Count > 0
            ? info.consumedGrades[0] : WeaponGrade.Common;
        var candidates = UpgradeManager.Shared?.GetGrandMergeCandidates(baseGrade)
                      ?? new System.Collections.Generic.List<WeaponData>();

        if (destIconImg != null && candidates.Count > 0)
        {
            destIconImg.enabled    = true;
            float rouletteDuration = Random.Range(0.7f, 1.0f);
            float rouletteElapsed  = 0f;
            float switchInterval   = 0.08f;
            float nextSwitch       = 0f;

            while (rouletteElapsed < rouletteDuration)
            {
                rouletteElapsed += Time.unscaledDeltaTime;
                if (rouletteElapsed >= nextSwitch)
                {
                    var pick = candidates[Random.Range(0, candidates.Count)];
                    if (pick?.icon != null) destIconImg.sprite = pick.icon;
                    nextSwitch     = rouletteElapsed + switchInterval;
                    switchInterval = Mathf.Lerp(0.08f, 0.25f, rouletteElapsed / rouletteDuration);
                }
                yield return null;
            }
            // resultWeapon이 후속 합성에 의해 Destroy됐을 수 있으므로 캡처된 아이콘 사용
            var finalIcon = info.resultIcon ?? info.resultWeapon?.icon;
            if (finalIcon != null) destIconImg.sprite = finalIcon;
        }

        yield return new WaitForSecondsRealtime(0.08f);

        // ── 2. 오버레이 카드 생성 후 목적지 슬롯 콘텐츠만 숨기기 ──────────
        float   slotScale = destRt.localScale.x;
        Vector2 slotSize  = destRt.rect.size * slotScale;
        var cardGo = CreateMergeResultCard(destLocal, slotSize, info.resultIcon, info.resultGrade);
        var cardRt = cardGo.GetComponent<RectTransform>();

        // 슬롯 스케일은 건드리지 않고 아이콘/별만 숨김 → 슬롯 프레임은 유지, 스케일 이상 방지
        if (info.destEquipSlot >= 0 && info.destEquipSlot < equipSlots.Length)
            equipSlots[info.destEquipSlot].HideContentImages();
        else if (info.destInvenSlot >= 0 && info.destInvenSlot < invenSlots.Length)
            invenSlots[info.destInvenSlot].HideContentImages();

        // 카드 팝인 (0 → 1.05 → 1.0)
        cardRt.localScale = Vector3.zero;
        const float popDuration = 0.12f;
        float popElapsed = 0f;
        while (popElapsed < popDuration)
        {
            popElapsed += Time.unscaledDeltaTime;
            float t = popElapsed / popDuration;
            float s = t < 0.7f
                ? Mathf.Lerp(0f, 1.05f, t / 0.7f)
                : Mathf.Lerp(1.05f, 1.0f, (t - 0.7f) / 0.3f);
            cardRt.localScale = Vector3.one * s;
            yield return null;
        }
        cardRt.localScale = Vector3.one;

        // ── 3. 화면 중앙으로 이동 & 스케일 업 ────────────────────────────
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRt, RectTransformUtility.WorldToScreenPoint(cam, grandMergeCenterAnchor.position),
            cam, out Vector2 centerLocal);

        const float moveDuration = 0.2f;
        float moveElapsed = 0f;
        Vector2 fromPos   = cardRt.anchoredPosition;
        Vector3 toScale   = Vector3.one * 3.5f;

        while (moveElapsed < moveDuration)
        {
            moveElapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, moveElapsed / moveDuration);
            cardRt.anchoredPosition = Vector2.Lerp(fromPos, centerLocal, t);
            cardRt.localScale       = Vector3.Lerp(Vector3.one, toScale, t);
            yield return null;
        }
        cardRt.anchoredPosition = centerLocal;
        cardRt.localScale       = toScale;

        SoundManager.Shared?.Play(SoundKey.Gacha_ItemReveal);

        // ── 4. 더블업이면 추가 연출 ────────────────────────────────────────
        if (info.isDoubleUp && info.doubleUpResult != null)
        {
            yield return new WaitForSecondsRealtime(0.25f);
            yield return StartCoroutine(DoubleUpRevealRoutine(cardGo, centerLocal, info));
        }
        else
        {
            yield return new WaitForSecondsRealtime(0.4f);
        }

        // ── 5. 원래 슬롯으로 복귀 (스케일 유지) ─────────────────────────
        moveElapsed = 0f;
        Vector2 backFrom = cardRt.anchoredPosition;

        while (moveElapsed < moveDuration)
        {
            moveElapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, moveElapsed / moveDuration);
            cardRt.anchoredPosition = Vector2.Lerp(backFrom, destLocal, t);
            // 리턴 중 스케일은 건드리지 않음 — 줄어드는 연출 방지
            yield return null;
        }

        Destroy(cardGo);
        // RefreshInventoryUI()가 슬롯 콘텐츠를 복원하므로 별도 처리 불필요
    }

    /// <summary>더블업 머지 연출 — Y축 회전 상승 (뒤집힐 때 결과 교체) → 중앙 쿵 → 반동.</summary>
    private IEnumerator DoubleUpRevealRoutine(GameObject cardGo, Vector2 centerLocal,
        PlayerInventory.MergeAnimInfo info)
    {
        var cardRt       = cardGo.GetComponent<RectTransform>();
        var cardIconImg  = cardGo.transform.Find("I")?.GetComponent<Image>();
        var cardFrameImg = cardGo.transform.Find("F")?.GetComponent<Image>();
        float baseScale  = cardRt.localScale.x; // 현재 스케일 그대로 유지

        // ── 1. Y축 회전하며 상승 (180도 시점에 아이콘/프레임 교체) ───
        const float riseDuration = 0.3f;
        float riseElapsed = 0f;
        Vector2 riseTarget = centerLocal + Vector2.up * 200f;
        bool swapped = false;

        while (riseElapsed < riseDuration)
        {
            riseElapsed += Time.unscaledDeltaTime;
            float t    = Mathf.Clamp01(riseElapsed / riseDuration);
            float ease = 1f - (1f - t) * (1f - t); // ease-out
            cardRt.anchoredPosition = Vector2.Lerp(centerLocal, riseTarget, ease);
            cardRt.localRotation    = Quaternion.Euler(0f, Mathf.Lerp(0f, 360f, ease), 0f);
            // 180도 시점(뒤집어지는 순간)에 결과로 교체
            // doubleUpResult가 후속 합성에 의해 Destroy됐을 수 있으므로 캡처된 값 사용
            if (!swapped && t >= 0.5f)
            {
                swapped = true;
                var duIcon  = info.doubleUpResultIcon;
                var duGrade = info.doubleUpResultGrade;
                if (cardIconImg != null && duIcon != null)
                    cardIconImg.sprite = duIcon;
                if (cardFrameImg != null)
                    cardFrameImg.sprite = WeaponVisualConfig.Shared?.GetGradeSprite(duGrade);
            }
            yield return null;
        }
        cardRt.localRotation = Quaternion.identity;
        // 혹시 루프가 빠르게 끝나서 미교체된 경우 보정
        if (!swapped)
        {
            var duIcon  = info.doubleUpResultIcon;
            var duGrade = info.doubleUpResultGrade;
            if (cardIconImg != null && duIcon != null)
                cardIconImg.sprite = duIcon;
            if (cardFrameImg != null)
                cardFrameImg.sprite = WeaponVisualConfig.Shared?.GetGradeSprite(duGrade);
        }

        yield return new WaitForSecondsRealtime(0.08f);

        // ── 2. 중앙으로 쿵 (ease-in 낙하) ───────────────────────────
        const float slamDuration = 0.12f;
        float slamElapsed = 0f;
        Vector2 slamFrom = cardRt.anchoredPosition;

        while (slamElapsed < slamDuration)
        {
            slamElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(slamElapsed / slamDuration);
            cardRt.anchoredPosition = Vector2.Lerp(slamFrom, centerLocal, t * t);
            yield return null;
        }
        cardRt.anchoredPosition = centerLocal;

        // ── 3. 착지 반동 ─────────────────────────────────────────────
        SoundManager.Shared?.Play(SoundKey.Gacha_ItemReveal);
        const float bounceDuration = 0.15f;
        float bounceElapsed = 0f;
        while (bounceElapsed < bounceDuration)
        {
            bounceElapsed += Time.unscaledDeltaTime;
            float t = bounceElapsed / bounceDuration;
            float s = t < 0.4f
                ? Mathf.Lerp(1f, 1.2f, t / 0.4f)
                : Mathf.Lerp(1.2f, 1f, (t - 0.4f) / 0.6f);
            cardRt.localScale = Vector3.one * (baseScale * s);
            yield return null;
        }
        cardRt.localScale = Vector3.one * baseScale;

        yield return new WaitForSecondsRealtime(0.6f);
    }

    /// <summary>합성 결과 오버레이 카드를 생성합니다 (프레임 "F" + 아이콘 "I").
    /// resultWeapon이 Destroy됐을 수 있으므로 사전 캡처된 icon/grade를 직접 받습니다.</summary>
    private GameObject CreateMergeResultCard(Vector2 localPos, Vector2 size, Sprite icon, WeaponGrade grade)
    {
        var go = new GameObject("MergeResultCard", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(rootCanvas.transform, false);
        go.transform.SetAsLastSibling();

        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = localPos;
        rt.sizeDelta        = size;
        rt.localScale       = Vector3.one;

        var cg = go.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        // 등급 프레임
        var fGo = new GameObject("F", typeof(RectTransform), typeof(Image));
        fGo.transform.SetParent(go.transform, false);
        var fRt  = fGo.GetComponent<RectTransform>();
        fRt.anchorMin = fRt.anchorMax = new Vector2(0.5f, 0.5f);
        fRt.sizeDelta = size;
        var fImg = fGo.GetComponent<Image>();
        fImg.sprite        = WeaponVisualConfig.Shared?.GetGradeSprite(grade);
        fImg.type          = Image.Type.Simple;
        fImg.raycastTarget = false;

        // 무기 아이콘
        if (icon != null)
        {
            var iGo = new GameObject("I", typeof(RectTransform), typeof(Image));
            iGo.transform.SetParent(go.transform, false);
            var iRt  = iGo.GetComponent<RectTransform>();
            iRt.anchorMin = iRt.anchorMax = new Vector2(0.5f, 0.5f);
            iRt.sizeDelta = size * 0.75f;
            var iImg = iGo.GetComponent<Image>();
            iImg.sprite        = icon;
            iImg.raycastTarget = false;
        }

        return go;
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

    public void SoldOutCard(WeaponData data)
    {
        for (int i = 0; i < cards.Length; i++)
            if (cards[i].Data == data)
                cards[i].SoldOut();
    }
}
