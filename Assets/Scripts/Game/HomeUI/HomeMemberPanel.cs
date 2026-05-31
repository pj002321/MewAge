using Mewage.DB;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

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
    public TextMeshProUGUI abilityNameText;
    public TextMeshProUGUI abilityDescText;

    [Header("스크롤")]
    public ScrollRect      characterScrollRect;
    public Transform       characterContent;

    [Header("슬롯 프리팹")]
    public CharacterSlotView slotPrefab;

    [Header("캐릭터 레벨업 패널")]
    public CharacterLevelUpPanel levelUpPanel;

    CharacterSlotView _selectedSlot;
    bool              _started;
    bool              _wasScrolling;

    void Start()
    {
        PlayerCharacterManager.Shared?.EnsureDefaultCharacter();
        LoadCharacters();

        if (characterPortrait != null && HomeCharacterDisplay.Shared != null)
            characterPortrait.texture = HomeCharacterDisplay.Shared.renderTexture;

        StartCoroutine(ApplyContentBottomPadding());

        _started = true;
        TriggerDropIn();
    }

IEnumerator ApplyContentBottomPadding()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        if (characterScrollRect == null || characterContent == null) yield break;

        float viewportH = characterScrollRect.viewport.rect.height;

        var spacer = new GameObject("BottomSpacer", typeof(RectTransform), typeof(LayoutElement));
        spacer.transform.SetParent(characterContent, false);
        var le = spacer.GetComponent<LayoutElement>();
        le.preferredHeight = viewportH;
        le.preferredWidth  = 1f;
    }

    void Update()
    {
        if (characterScrollRect == null) return;

        bool isScrolling = characterScrollRect.velocity.sqrMagnitude > 100f;

        if (isScrolling && !_wasScrolling)
            _selectedSlot?.OnScrollBegin();
        else if (!isScrolling && _wasScrolling)
            _selectedSlot?.OnScrollEnd();

        _wasScrolling = isScrolling;

    }

    void OnEnable()
    {
        if (!_started) return;
        ReselectEquipped();
        TriggerDropIn();
    }

    void OnDisable()
    {
        if (!gameObject.scene.isLoaded) return;
        levelUpPanel?.gameObject.SetActive(false);
        var equippedId = PlayerCharacterManager.Shared?.EquippedCharID;
        if (!string.IsNullOrEmpty(equippedId))
            HomeCharacterDisplay.Shared?.ChangeCharacter(equippedId);
    }

    void ReselectEquipped()
    {
        string equippedId = PlayerCharacterManager.Shared?.EquippedCharID;
        if (string.IsNullOrEmpty(equippedId) || characterContent == null) return;

        foreach (Transform child in characterContent)
        {
            var slot = child.GetComponent<CharacterSlotView>();
            if (slot != null && slot.data?.f_캐릭ID == equippedId)
            {
                SelectSlot(slot);
                return;
            }
        }
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

        int limit = DB_캐릭터.CountEntities > 0 ? DB_캐릭터.GetEntity(0).f_캐릭행수 : 0;
        for (int i = 0; i < limit && i < DB_캐릭터.CountEntities; i++)
        {
            var row  = DB_캐릭터.GetEntity(i);
            var slot = Instantiate(slotPrefab, characterContent);
            slot.Init(this);
            slot.Bind(row);

            if (row.f_캐릭ID == equippedId)
                SelectSlot(slot);
        }
    }

    public void SelectSlot(CharacterSlotView slot)
    {
        if (levelUpPanel != null && levelUpPanel.gameObject.activeSelf)
            levelUpPanel.gameObject.SetActive(false);

        if (_selectedSlot != null && _selectedSlot != slot)
            _selectedSlot.Deselect();

        _selectedSlot = slot;
        _selectedSlot.Select();

        if (slot.data != null) RefreshTopView(slot.data);
    }

    public void ToggleLevelUpPanel(DB_캐릭터 d)
    {
        if (levelUpPanel == null) return;
        if (levelUpPanel.gameObject.activeSelf)
        {
            levelUpPanel.gameObject.SetActive(false);
            _selectedSlot?.SetCanvasOverride(true);
        }
        else
        {
            _selectedSlot?.SetCanvasOverride(false);
            if (_selectedSlot != null && characterScrollRect != null)
                StartCoroutine(ScrollToSlotTop(_selectedSlot));
            levelUpPanel.OnLevelUpSuccess = OnCharacterLeveledUp;
            levelUpPanel.Show(d);
        }
    }

    private IEnumerator ScrollToSlotTop(CharacterSlotView slot)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        var slotRect    = slot.GetComponent<RectTransform>();
        var contentRect = characterContent as RectTransform;
        if (slotRect == null || contentRect == null) yield break;

        Vector2 slotPos     = (Vector2)contentRect.InverseTransformPoint(slotRect.position);
        float   topOffset   = contentRect.rect.height * contentRect.pivot.y + slotPos.y
                              + slotRect.rect.height * (1f - slotRect.pivot.y);
        float   scrollRange = contentRect.rect.height - characterScrollRect.viewport.rect.height;

        if (scrollRange > 0f)
            characterScrollRect.verticalNormalizedPosition = Mathf.Clamp01(topOffset / scrollRange);
    }

    private void OnCharacterLeveledUp()
    {
        if (_selectedSlot?.data != null)
            RefreshTopView(_selectedSlot.data);
    }

    public void RefreshAllSlots()
    {
        if (characterContent == null) return;
        foreach (Transform child in characterContent)
            child.GetComponent<CharacterSlotView>()?.RefreshState();
    }

    public void SetPortrait(DB_캐릭터 d)
    {
        if (characterNameText) characterNameText.text = d.f_이름;
        HomeCharacterDisplay.Shared?.ChangeCharacter(d.f_캐릭ID);
    }

    void RefreshTopView(DB_캐릭터 d)
    {
        if (characterNameText) characterNameText.text = d.f_이름;

        HomeCharacterDisplay.Shared?.ChangeCharacter(d.f_캐릭ID);

        if (skillIconImg)
        {
            var icon = MemberVisualConfig.Shared?.GetSkillIcon(d.f_스킬ID);
            skillIconImg.sprite  = icon;
            skillIconImg.enabled = icon != null;
        }

        int rawTier = PlayerCharacterManager.Shared?.GetAugmentTier(d.f_캐릭ID) ?? 0;
        int tier    = d.f_보유여부 ? Mathf.Max(1, rawTier) : 0;

        if (augmentPanels != null)
        {
            if (augmentPanels.Length > 0 && augmentPanels[0] != null)
                augmentPanels[0].Refresh(d.f_속성1ID, d.f_S1, tier >= 1, d.f_등급);
            if (augmentPanels.Length > 1 && augmentPanels[1] != null)
                augmentPanels[1].Refresh(d.f_속성2ID, d.f_S2, tier >= 2, d.f_등급);
            if (augmentPanels.Length > 2 && augmentPanels[2] != null)
                augmentPanels[2].Refresh(d.f_속성3ID, d.f_S3, tier >= 3, d.f_등급);
        }

        if (abilityNameText) abilityNameText.text = d.f_고유능력 ?? string.Empty;
        if (abilityDescText)  abilityDescText.text  = d.f_능력설명  ?? string.Empty;
    }
}
