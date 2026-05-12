using System.Collections.Generic;
using TMPro;
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
    public EquipmentSlotView[] equipSlots          = new EquipmentSlotView[6];
    [Tooltip("슬롯[i]가 비어있을 때 표시할 카테고리 (슬롯 순서와 1:1 매핑)")]
    public EquipCategory[]     slotCategories      = new EquipCategory[6];

    [Header("정렬 전환 버튼 (누를 때마다 등급→문명→유형 순환)")]
    public Button           sortButton;
    public TMPro.TMP_Text   sortButtonLabel;

    [Header("인벤토리")]
    public Transform inventoryContent;
    public EquipmentSlotView slotPrefab;
    public EquipmentDetailPopup detailPopup;

    [Header("스탯 변화 표시")]
    public TMP_Text statChangeText;

    [Header("캐릭터 이미지")]
    public RawImage characterImage;

    static readonly EquipSortMode[] _sortCycle =
        { EquipSortMode.ByGrade, EquipSortMode.ByCivilization, EquipSortMode.ByCategory };
    static readonly string[] _sortLabels = { "등급 순", "문명 순", "유형 순" };

    int           _sortIndex = 0;
    EquipSortMode _sortMode  = EquipSortMode.ByGrade;
    private bool _showStatChange;
    private Dictionary<StatType, float> _prevStats;
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
        sortButton?.onClick.AddListener(OnSortButtonClicked);
        SetSort(0);
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

    void OnSortButtonClicked()
    {
        _sortIndex = (_sortIndex + 1) % _sortCycle.Length;
        SetSort(_sortIndex);
    }

    void SetSort(int index)
    {
        _sortIndex = index;
        _sortMode  = _sortCycle[index];
        RefreshInventory();

        if (sortButtonLabel != null)
            sortButtonLabel.text = _sortLabels[index];
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

        characterImage.texture = HomeCharacterDisplay.Shared.renderTexture;
        characterImage.enabled = true;
    }

    void RefreshEquipSlots()
    {
        var inv = EquipmentInventory.Shared;
        for (int i = 0; i < equipSlots.Length; i++)
        {
            var slot = equipSlots[i];
            if (slot == null) continue;
            var cat  = i < slotCategories.Length ? slotCategories[i] : (EquipCategory)i;
            // EquipmentInventory.equipped 배열은 (int)category 인덱스로 저장됨
            var data = inv?.Equipped[(int)cat];
            slot.Bind(data, OnEquipSlotClicked, cat);
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
        float hpDiff = currStats[StatType.MaxHP] - _prevStats[StatType.MaxHP];
        float critChanceDiff = currStats[StatType.CritChance] - _prevStats[StatType.CritChance];
        float critDmgDiff = currStats[StatType.CritDmg] - _prevStats[StatType.CritDmg];
        float evasionDiff = currStats[StatType.Evasion] - _prevStats[StatType.Evasion];

        string text = "";
        if (atkDiff != 0) text += $"공격력 {(atkDiff > 0 ? "+" : "")}{atkDiff:F0}\n";
        if (defDiff != 0) text += $"방어력 {(defDiff > 0 ? "+" : "")}{defDiff:F0}\n";
        if (hpDiff != 0) text += $"체력 {(hpDiff > 0 ? "+" : "")}{hpDiff:F0}\n";
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
