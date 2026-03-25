using Mewage.DB;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 홈 대원(캐릭터) 패널.
/// 탭: 캐릭터 · 룬  (장비 탭 없음)
/// 슬롯 클릭 시 하이라이트 + 장착/구매 버튼 표시.
/// </summary>
public class HomeMemberPanel : MonoBehaviour
{
    [Header("캐릭터 뷰 — 포트레이트 (좌상단)")]
    public Image           characterPortrait;
    public TextMeshProUGUI characterNameText;

    [Header("증강 능력치 패널 (우상단)")]
    public TextMeshProUGUI augmentLv1Text;
    public TextMeshProUGUI augmentLv2Text;
    public TextMeshProUGUI augmentLv3Text;
    public TextMeshProUGUI augmentLv4Text;

    [Header("탭 버튼 (캐릭터 · 룬)")]
    public Button tabCharacter;
    public Button tabRune;

    [Header("탭 활성 배경 컬러 (텍스트 색상은 변경하지 않음)")]
    public Color tabActiveColor   = new Color(1f,  0.80f, 0.20f, 1f);
    public Color tabInactiveColor = new Color(0.15f, 0.20f, 0.35f, 1f);

    [Header("스크롤 Content")]
    public Transform characterContent;

    [Header("슬롯 프리팹")]
    public CharacterSlotView slotPrefab;

    CharacterSlotView _selectedSlot;

    void Start()
    {
        tabCharacter?.onClick.AddListener(() => ShowTab(0));
        tabRune     ?.onClick.AddListener(() => ShowTab(1));
        ShowTab(0);
        LoadCharacters();
    }

    void LoadCharacters()
    {
        if (slotPrefab == null || characterContent == null) return;

        foreach (Transform child in characterContent)
            Destroy(child.gameObject);

        DB_캐릭터.ForEachEntity(row =>
        {
            var slot = Instantiate(slotPrefab, characterContent);
            slot.Bind(row);
        });
    }

    public void ShowTab(int index)
    {
        SetTabBg(tabCharacter, index == 0);
        SetTabBg(tabRune,      index == 1);
    }

    // 배경 색상만 변경, 텍스트 색상 건드리지 않음
    void SetTabBg(Button btn, bool active)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img) img.color = active ? tabActiveColor : tabInactiveColor;
    }

    /// <summary>CharacterSlotView 에서 호출 — 선택 슬롯 교체.</summary>
    public void SelectSlot(CharacterSlotView slot)
    {
        if (_selectedSlot != null && _selectedSlot != slot)
            _selectedSlot.Deselect();

        _selectedSlot = slot;
        _selectedSlot.Select();

        if (slot.data != null) RefreshTopView(slot.data);
    }

    /// <summary>장착 버튼 클릭 시 CharacterSlotView → 여기로 전달.</summary>
    public void SetPortrait(DB_캐릭터 d)
    {
        if (characterPortrait && d.f_썸네일)
        {
            characterPortrait.sprite  = d.f_썸네일;
            characterPortrait.enabled = true;
        }
        if (characterNameText)
            characterNameText.text = d.f_이름;
    }

    void RefreshTopView(DB_캐릭터 d)
    {
        if (characterNameText) characterNameText.text = d.f_이름;

        // Lv1: f_능력치1 (설명 텍스트) + f_속성1ID / f_S1 (게임 로직용 수치)
        if (augmentLv1Text) augmentLv1Text.text = BuildStatLine(d.f_능력치1, d.f_속성1ID, d.f_S1);
        // Lv2~3: 속성ID + 수치값으로 표시
        if (augmentLv2Text) augmentLv2Text.text = BuildStatLine(null, d.f_속성2ID, d.f_S2);
        if (augmentLv3Text) augmentLv3Text.text = BuildStatLine(null, d.f_속성3ID, d.f_S3);
        // Lv4: 특수 능력 (능력ID + 설명)
        if (augmentLv4Text) augmentLv4Text.text = BuildAbilityLine(d.f_능력ID, d.f_능력치설명);
    }

    // 설명 텍스트가 있으면 우선 사용, 없으면 "속성 +수치" 형식으로 fallback
    static string BuildStatLine(string desc, string attrId, string value)
    {
        if (!string.IsNullOrEmpty(desc)) return desc;
        if (string.IsNullOrEmpty(attrId)) return string.Empty;
        return string.IsNullOrEmpty(value) ? attrId : $"{attrId} +{value}";
    }

    static string BuildAbilityLine(string abilityId, string desc)
    {
        if (!string.IsNullOrEmpty(desc)) return desc;
        return abilityId ?? string.Empty;
    }
}
