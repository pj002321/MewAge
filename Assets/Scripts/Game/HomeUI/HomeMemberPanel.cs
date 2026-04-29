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

    [Header("증강 능력치 패널 3개 (AugmentStatPanel 컴포넌트 부착)")]
    public AugmentStatPanel[] augmentPanels; // [0]=Lv1, [1]=Lv2, [2]=Lv3

    [Header("고유능력 텍스트")]
    public TextMeshProUGUI abilityNameText;  // f_고유능력  (예: "숙련")
    public TextMeshProUGUI abilityDescText;  // f_능력설명  (예: "많은 적을 처치 시...")

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
    bool              _started;

    void Start()
    {
        tabCharacter?.onClick.AddListener(() => ShowTab(0));
        tabRune     ?.onClick.AddListener(() => ShowTab(1));
        ShowTab(0);

        PlayerCharacterManager.Shared?.EnsureDefaultCharacter();
        LoadCharacters();

        if (characterPortrait != null && HomeCharacterDisplay.Shared != null)
            characterPortrait.texture = HomeCharacterDisplay.Shared.renderTexture;

        _started = true;
        TriggerDropIn();
    }

    void OnEnable()
    {
        if (!_started) return; // Start 전에는 Skip (Start에서 처리)
        TriggerDropIn();
    }

    void TriggerDropIn()
    {
        var charId = PlayerCharacterManager.Shared?.EquippedCharID;
        if (string.IsNullOrEmpty(charId)) return;
        HomeCharacterDisplay.Shared?.ChangeCharacter(charId);
        HomeCharacterDisplay.Shared?.DropIn();
    }

    void LoadCharacters()
    {
        if (slotPrefab == null || characterContent == null) return;

        foreach (Transform child in characterContent)
            Destroy(child.gameObject);

        string equippedId = PlayerCharacterManager.Shared?.EquippedCharID;

        DB_캐릭터.ForEachEntity(row =>
        {
            var slot = Instantiate(slotPrefab, characterContent);
            slot.Bind(row);

            // 장착 중인 영웅 슬롯을 자동 선택해 상단 정보 즉시 표시
            if (row.f_캐릭ID == equippedId)
                SelectSlot(slot);
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

        // 증강 티어 — 미보유 캐릭터는 0으로 처리(전부 잠금)
        int tier = d.f_보유여부
            ? (PlayerCharacterManager.Shared?.GetAugmentTier(d.f_캐릭ID) ?? 1)
            : 0;

        // 증강 능력치 패널 3개
        if (augmentPanels != null)
        {
            if (augmentPanels.Length > 0 && augmentPanels[0] != null)
                augmentPanels[0].Refresh(d.f_속성1ID, d.f_S1, tier >= 1, d.f_등급);
            if (augmentPanels.Length > 1 && augmentPanels[1] != null)
                augmentPanels[1].Refresh(d.f_속성2ID, d.f_S2, tier >= 2, d.f_등급);
            if (augmentPanels.Length > 2 && augmentPanels[2] != null)
                augmentPanels[2].Refresh(d.f_속성3ID, d.f_S3, tier >= 3, d.f_등급);
        }

        // 고유능력
        if (abilityNameText) abilityNameText.text = d.f_고유능력 ?? string.Empty;
        if (abilityDescText)  abilityDescText.text  = d.f_능력설명  ?? string.Empty;
    }
}
