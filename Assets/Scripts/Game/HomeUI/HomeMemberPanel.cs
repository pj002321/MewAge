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

    [Header("스킬 아이콘 (MemberVisualConfig)")]
    public Image            skillIconImg;

    [Header("증강 능력치 패널 — 속성 텍스트")]
    public TextMeshProUGUI augmentLv1Text;
    public TextMeshProUGUI augmentLv2Text;
    public TextMeshProUGUI augmentLv3Text;

    [Header("증강 능력치 패널 — 인덱스 이미지 (속성 1~3 + 고유능력)")]
    public Image augmentIndex1Img;
    public Image augmentIndex2Img;
    public Image augmentIndex3Img;
    public Image augmentIndex4Img;

    [Header("고유능력 텍스트")]
    public TextMeshProUGUI abilityNameText;   // f_고유능력  (예: "숙련")
    public TextMeshProUGUI abilityDescText;   // f_능력설명  (예: "많은 적을 처치 시...")

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

        PlayerCharacterManager.Shared?.EnsureDefaultCharacter();
        LoadCharacters();

        if (characterPortrait != null && HomeCharacterDisplay.Shared != null)
            characterPortrait.texture = HomeCharacterDisplay.Shared.renderTexture;

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

        HomeCharacterDisplay.Shared?.ChangeCharacter(d.f_캐릭ID);

        // 스킬 아이콘
        if (skillIconImg)
        {
            var icon = MemberVisualConfig.Shared?.GetSkillIcon(d.f_스킬ID);
            skillIconImg.sprite  = icon;
            skillIconImg.enabled = icon != null;
        }

        // 증강 티어 — 미보유 캐릭터는 0으로 처리(잠금)
        int tier = d.f_보유여부
            ? (PlayerCharacterManager.Shared?.GetAugmentTier(d.f_캐릭ID) ?? 1)
            : 0;

        // 속성 텍스트
        if (augmentLv1Text) augmentLv1Text.text = BuildStatLine(d.f_속성1ID, d.f_S1);
        if (augmentLv2Text) augmentLv2Text.text = BuildStatLine(d.f_속성2ID, d.f_S2);
        if (augmentLv3Text) augmentLv3Text.text = BuildStatLine(d.f_속성3ID, d.f_S3);

        // 인덱스 이미지 (잠금/해금)
        SetAugmentIndex(augmentIndex1Img, 1, tier >= 1);
        SetAugmentIndex(augmentIndex2Img, 2, tier >= 2);
        SetAugmentIndex(augmentIndex3Img, 3, tier >= 3);
        SetAugmentIndex(augmentIndex4Img, 4, true); // 고유능력은 항상 해금 표시

        // 고유능력
        if (abilityNameText) abilityNameText.text = d.f_고유능력 ?? string.Empty;
        if (abilityDescText)  abilityDescText.text  = d.f_능력설명  ?? string.Empty;
    }

    void SetAugmentIndex(Image img, int level, bool unlocked)
    {
        if (img == null) return;
        var sprite = MemberVisualConfig.Shared?.GetAugmentIndex(level, unlocked);
        img.sprite  = sprite;
        img.enabled = sprite != null;
    }

    // ── StatID → 한국어 라벨 ──────────────────────────────────────────────

    static string BuildStatLine(string statId, string value)
    {
        if (string.IsNullOrEmpty(statId)) return string.Empty;

        string label = StatIdToLabel(statId);
        if (string.IsNullOrEmpty(value)) return label;
        return $"{label} +{value}%";
    }

    static string StatIdToLabel(string statId) => statId switch
    {
        "STAT_ATK"       => "공격력",
        "STAT_DEF"       => "방어력",
        "STAT_HP"        => "최대체력",
        "STAT_SPD_ATK"   => "공격속도",
        "STAT_SPD_MOV"   => "이동속도",
        "STAT_CRI_RATE"  => "치명타 확률",
        "STAT_CRI_DMG"   => "치명타 데미지",
        "STAT_EVA_RATE"  => "회피 확률",
        "STAT_ATK_RNG"   => "공격사거리",
        "STAT_CUR_REWARD"=> "추가 골드",
        "STAT_CUR_EXP"   => "추가 경험치",
        "STAT_SKL_CD"    => "기물 쿨타임",
        "STAT_STUN"      => "적 둔화",
        "STAT_DOT_DMG"   => "출혈 데미지",
        "STAT_DOT"       => "지속피해",
        _                => statId,
    };
}
