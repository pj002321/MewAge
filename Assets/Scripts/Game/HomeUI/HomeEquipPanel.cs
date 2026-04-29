using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 홈 장비 패널.
/// 상단: 캐릭터 이미지 + 장착 슬롯 6개 (EquipmentSlotView) — 좌3 / 우3
/// 하단: 탭(전체 / 등급순 / 문명순) + 보유 장비 스크롤 목록
///
/// Inspector 연결:
///   characterImage   — 현재 장착 캐릭터 이미지
///   equipSlots[6]    — 장착 슬롯 EquipmentSlotView 6개
///   tabAll / tabByGrade / tabByCiv — 탭 버튼
///   inventoryContent — 인벤 그리드 Content
///   slotPrefab       — EquipmentSlotView 프리팹
///   detailPopup      — EquipmentDetailPopup
///   statChangeText   — 스탯 변화 표시 텍스트
/// </summary>
public class HomeEquipPanel : MonoBehaviour
{
    [Header("장착 슬롯 (0~2=왼쪽, 3~5=오른쪽)")]
    public EquipmentSlotView[] equipSlots = new EquipmentSlotView[6];

    [Header("탭 버튼")]
    public Button tabAll;
    public Button tabByGrade;
    public Button tabByCiv;

    [Header("탭 스프라이트")]
    public Sprite tabActiveSprite;
    public Sprite tabInactiveSprite;

    [Header("인벤토리")]
    public Transform            inventoryContent;
    public EquipmentSlotView    slotPrefab;
    public EquipmentDetailPopup detailPopup;

    [Header("스탯 변화 표시")]
    public TMPro.TMP_Text statChangeText;

    [Header("캐릭터 이미지")]
    public UnityEngine.UI.Image characterImage;

    EquipSortMode _sortMode = EquipSortMode.None;
    private bool _showStatChange;
    private System.Collections.Generic.Dictionary<StatType, float> _prevStats;
    private Coroutine _statChangeCoroutine;

    void Awake()
    {
        if (EquipmentInventory.Shared == null)
            MonoBehaviourSingleton<EquipmentInventory>.Create("EquipmentInventory");
        if (PlayerStatManager.Shared == null)
            MonoBehaviourSingleton<PlayerStatManager>.Create("PlayerStatManager");
        if (statChangeText != null)
            statChangeText.gameObject.SetActive(false);
    }

    void Start()
    {
        tabAll?.onClick.AddListener(()     => SetSort(EquipSortMode.None));
        tabByGrade?.onClick.AddListener(() => SetSort(EquipSortMode.ByGrade));
        tabByCiv?.onClick.AddListener(()   => SetSort(EquipSortMode.ByCivilization));
        SetSort(EquipSortMode.None);
    }

    void OnEnable()
    {
        RefreshAll();
        var inv = EquipmentInventory.Shared;
        if (inv != null) inv.OnChanged += OnInventoryChanged;
    }

    void OnDisable()
    {
        var inv = EquipmentInventory.Shared;
        if (inv != null) inv.OnChanged -= OnInventoryChanged;
        if (_statChangeCoroutine != null) StopCoroutine(_statChangeCoroutine);
    }

    void SetSort(EquipSortMode mode)
    {
        _sortMode = mode;
        SetTabSprite(tabAll,     mode == EquipSortMode.None);
        SetTabSprite(tabByGrade, mode == EquipSortMode.ByGrade);
        SetTabSprite(tabByCiv,   mode == EquipSortMode.ByCivilization);
        RefreshInventory();
    }

    void SetTabSprite(Button btn, bool active)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img == null) return;
        var target = active ? tabActiveSprite : tabInactiveSprite;
        if (target != null) img.sprite = target;
    }

    void RefreshAll()
    {
        RefreshCharacterImage();
        RefreshEquipSlots();
        RefreshInventory();
    }

    void RefreshCharacterImage()
    {
        if (characterImage == null) return;
        var charMgr = PlayerCharacterManager.Shared;
        if (charMgr == null) return;
        string charID = charMgr.EquippedCharID;
        if (string.IsNullOrEmpty(charID))
        {
            characterImage.enabled = false;
            return;
        }
        string spritePath = $"Character/{charID.ToLower()}";
        var sprite = Resources.Load<Sprite>(spritePath);
        if (sprite != null)
        {
            characterImage.sprite = sprite;
            characterImage.enabled = true;
        }
        else
        {
            characterImage.enabled = false;
        }
    }

    void RefreshEquipSlots()
    {
        var inv = EquipmentInventory.Shared;
        for (int i = 0; i < equipSlots.Length; i++)
        {
            var slot = equipSlots[i];
            if (slot == null) continue;
            var data = inv != null && i < inv.Equipped.Count ? inv.Equipped[i] : null;
            slot.Bind(data, OnEquipSlotClicked);
        }
    }

    void RefreshInventory()
    {
        if (inventoryContent == null || slotPrefab == null) return;

        foreach (Transform child in inventoryContent)
            Destroy(child.gameObject);

        var inv = EquipmentInventory.Shared;
        if (inv == null) return;

        foreach (var data in inv.GetSortedBag(_sortMode))
        {
            var slot = Instantiate(slotPrefab, inventoryContent);
            slot.Bind(data, OnBagSlotClicked);
        }
    }

    void OnBagSlotClicked(EquipmentSlotView slot)
    {
        _prevStats = GetCurrentStats();
        _showStatChange = true;
        detailPopup?.ShowForBagItem(slot.Data);
    }

    void OnInventoryChanged()
    {
        EquipmentInventory.Shared?.RecalculateEquipStats();
        RefreshAll();
        if (_showStatChange)
        {
            _showStatChange = false;
            ShowStatChange();
        }
    }

    void OnEquipSlotClicked(EquipmentSlotView slot)
    {
        if (slot.Data == null) return;
        _prevStats = GetCurrentStats();
        _showStatChange = true;
        detailPopup?.ShowForEquippedItem(slot.Data);
    }

    System.Collections.Generic.Dictionary<StatType, float> GetCurrentStats()
    {
        var sm = PlayerStatManager.Shared;
        if (sm == null) return null;
        return new System.Collections.Generic.Dictionary<StatType, float>
        {
            { StatType.ATK, sm.GetFinal(StatType.ATK) },
            { StatType.DEF, sm.GetFinal(StatType.DEF) },
            { StatType.MaxHP, sm.GetFinal(StatType.MaxHP) },
            { StatType.CritChance, sm.GetFinal(StatType.CritChance) },
            { StatType.CritDmg, sm.GetFinal(StatType.CritDmg) },
            { StatType.Evasion, sm.GetFinal(StatType.Evasion) }
        };
    }

    void ShowStatChange()
    {
        var currStats = GetCurrentStats();
        if (_prevStats == null || currStats == null || statChangeText == null) return;

        float atkDiff = currStats[StatType.ATK] - _prevStats[StatType.ATK];
        float defDiff = currStats[StatType.DEF] - _prevStats[StatType.DEF];
        float hpDiff  = currStats[StatType.MaxHP] - _prevStats[StatType.MaxHP];
        float critChanceDiff = currStats[StatType.CritChance] - _prevStats[StatType.CritChance];
        float critDmgDiff = currStats[StatType.CritDmg] - _prevStats[StatType.CritDmg];
        float evasionDiff = currStats[StatType.Evasion] - _prevStats[StatType.Evasion];

        string text = "";
        if (atkDiff != 0) text += $"공격력 {(atkDiff > 0 ? "+" : "")}{atkDiff:F0}\n";
        if (defDiff != 0) text += $"방어력 {(defDiff > 0 ? "+" : "")}{defDiff:F0}\n";
        if (hpDiff != 0)  text += $"체력 {(hpDiff > 0 ? "+" : "")}{hpDiff:F0}\n";
        if (critChanceDiff != 0) text += $"치명타 확률 {(critChanceDiff > 0 ? "+" : "")}{critChanceDiff:F0}%\n";
        if (critDmgDiff != 0) text += $"치명타 데미지 {(critDmgDiff > 0 ? "+" : "")}{critDmgDiff:F0}%\n";
        if (evasionDiff != 0) text += $"회피율 {(evasionDiff > 0 ? "+" : "")}{evasionDiff:F0}%\n";
        if (text == "") return;

        bool isIncrease = (atkDiff + defDiff + hpDiff + critChanceDiff + critDmgDiff + evasionDiff) > 0;
        statChangeText.color = isIncrease ? Color.green : Color.red;

        if (_statChangeCoroutine != null) StopCoroutine(_statChangeCoroutine);
        _statChangeCoroutine = StartCoroutine(StatChangeRoutine(text.Trim(), isIncrease));
    }

    System.Collections.IEnumerator StatChangeRoutine(string finalText, bool isIncrease)
    {
        // 팝업이 닫힌 뒤(한 프레임 후) 애니메이션 재생
        yield return null;

        if (isIncrease)
            HomeCharacterDisplay.Shared?.PlayHappy();
        else
            HomeCharacterDisplay.Shared?.PlaySad();

        statChangeText.gameObject.SetActive(true);

        // 빠친코 연출: 0.5초 동안 랜덤 숫자
        float duration = 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            string randomText = "";
            foreach (char c in finalText)
            {
                if (char.IsDigit(c) || c == '+' || c == '-')
                    randomText += Random.Range(0, 10).ToString();
                else
                    randomText += c;
            }
            statChangeText.text = randomText;
            yield return null;
        }

        statChangeText.text = finalText;

        // 2초 후 사라짐
        yield return new WaitForSeconds(2f);
        statChangeText.gameObject.SetActive(false);
    }
}
