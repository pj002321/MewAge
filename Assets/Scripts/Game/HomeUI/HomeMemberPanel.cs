using Mewage.DB;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 홈 대원(캐릭터) 패널.
/// 탭: 캐릭터 · 룬  (장비 탭 없음)
/// 슬롯 클릭 시 하이라이트 + 장착/구매 버튼 표시.
/// 캐릭터 포트레이트는 HomeCharacterDisplay → RenderTexture → RawImage로 표시.
/// </summary>
public class HomeMemberPanel : MonoBehaviour
{
    [Header("캐릭터 뷰 — 포트레이트 RawImage (HomeCharacterDisplay RT 연결)")]
    public RawImage         characterPortrait;
    public TextMeshProUGUI  characterNameText;

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

        // RawImage에 HomeCharacterDisplay의 RenderTexture 연결
        if (characterPortrait != null && HomeCharacterDisplay.Shared != null)
            characterPortrait.texture = HomeCharacterDisplay.Shared.renderTexture;

        // 패널이 열릴 때 현재 장착 캐릭터 포트레이트 반영
        var charId = PlayerCharacterManager.Shared?.EquippedCharID;
        if (!string.IsNullOrEmpty(charId))
            HomeCharacterDisplay.Shared?.ChangeCharacter(charId);
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
        if (characterNameText) characterNameText.text = d.f_이름;
        HomeCharacterDisplay.Shared?.ChangeCharacter(d.f_캐릭ID);
    }

    void RefreshTopView(DB_캐릭터 d)
    {
        if (characterNameText) characterNameText.text = d.f_이름;

        // 포트레이트: HomeCharacterDisplay가 모델을 교체 → RT 자동 갱신
        HomeCharacterDisplay.Shared?.ChangeCharacter(d.f_캐릭ID);

        if (augmentLv1Text) augmentLv1Text.text = BuildStatLine(d.f_속성1ID, d.f_S1);
        if (augmentLv2Text) augmentLv2Text.text = BuildStatLine(d.f_속성2ID, d.f_S2);
        if (augmentLv3Text) augmentLv3Text.text = BuildStatLine(d.f_속성3ID, d.f_S3);
        if (augmentLv4Text) augmentLv4Text.text = BuildAbilityLine(d.f_능력ID, d.f_능력치설명);
    }

    static string BuildStatLine(string attrId, string value)
    {
        if (string.IsNullOrEmpty(attrId)) return string.Empty;
        return string.IsNullOrEmpty(value) ? attrId : $"{attrId} +{value}";
    }

    static string BuildAbilityLine(string abilityId, string desc)
    {
        if (!string.IsNullOrEmpty(desc)) return desc;
        return abilityId ?? string.Empty;
    }
}
