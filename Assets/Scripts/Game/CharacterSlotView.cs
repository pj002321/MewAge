using Mewage.DB;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 대원 패널 — 캐릭터 슬롯 한 칸.
/// · 슬롯 클릭 → 하이라이트 + 액션 버튼 표시
/// · f_보유여부=true  → 장착 버튼
/// · f_보유여부=false → 구매 버튼 (가격 표시)
/// · 슬롯 배경색은 f_등급에 따라 결정
/// </summary>
[RequireComponent(typeof(Button))]
public class CharacterSlotView : MonoBehaviour
{
    [Header("데이터 (런타임 바인딩)")]
    public DB_캐릭터 data;

    [Header("슬롯 UI")]
    public Image           slotBackground;  // 등급 색상 적용 대상
    public Image           thumbnailImage;  // 캐릭터 썸네일
    public TextMeshProUGUI gradeText;       // 등급 텍스트 (S / A / B / C)

    [Header("잠금 상태 (미보유)")]
    public GameObject      lockImage;        // 자물쇠 아이콘
    public GameObject      lockedOverlay;    // 어두운 오버레이

    [Header("선택 하이라이트")]
    public GameObject      highlight;

    [Header("선택 시 확대 배율")]
    public float           selectedScale = 1.12f;

    [Header("액션 버튼 — 장착 (보유 중)")]
    public Button          equipButton;

    [Header("액션 버튼 — 구매 (미보유)")]
    public Button          buyButton;
    public TextMeshProUGUI buyPriceText;

    HomeMemberPanel _panel;

    void Awake()
    {
        _panel = GetComponentInParent<HomeMemberPanel>(true);
        GetComponent<Button>().onClick.AddListener(OnSlotClick);
        equipButton?.onClick.AddListener(OnEquip);
        buyButton  ?.onClick.AddListener(OnBuy);
        Deselect();
    }

    // ── 데이터 바인딩 ────────────────────────────────────────────
    public void Bind(DB_캐릭터 row)
    {
        data = row;
        if (row == null) return;

        // 등급 색상
        if (slotBackground)
            slotBackground.color = CharacterGradeColor.Get(row.f_등급);

        // 썸네일 — f_캐릭ID를 소문자로 변환 → Resources/Character/ 에서 로드
        if (thumbnailImage)
        {
            var spr = string.IsNullOrEmpty(row.f_캐릭ID)
                ? null
                : Resources.Load<Sprite>("Character/" + row.f_캐릭ID.ToLower());
            thumbnailImage.enabled = spr != null;
            if (spr != null) thumbnailImage.sprite = spr;
        }

        // 등급 텍스트
        if (gradeText) gradeText.text = row.f_등급;

        // 구매 가격
        if (buyPriceText) buyPriceText.text = $"x{row.f_가격}";

        // 잠금 상태: 미보유면 잠금 이미지 + 오버레이 표시
        bool owned = row.f_보유여부;
        if (lockImage)     lockImage    .SetActive(!owned);
        if (lockedOverlay) lockedOverlay.SetActive(!owned);
    }

    // ── 클릭 / 장착 / 구매 ──────────────────────────────────────
    void OnSlotClick() => _panel?.SelectSlot(this);

    void OnEquip()
    {
        if (data == null) return;
        PlayerCharacterManager.Shared?.EquipCharacter(data);
        _panel?.SetPortrait(data);
    }

    void OnBuy()
    {
        if (data == null) return;
        Debug.Log($"[Member] {data.f_이름} 구매 시도 | 가격: {data.f_가격} 보석");
        // TODO: 보석 차감 → data.f_보유여부 = true → Bind(data)
    }

    // ── 선택 상태 ─────────────────────────────────────────────────
    public void Select()
    {
        if (highlight) highlight.SetActive(true);
        SetChildrenScale(1.1f);

        bool owned = data?.f_보유여부 ?? false;
        if (equipButton)
        {
            equipButton.gameObject.SetActive(owned);
            bool isEquipped = data != null &&
                              PlayerCharacterManager.Shared?.EquippedCharID == data.f_캐릭ID;
            var label = equipButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label) label.text = isEquipped ? "장착중" : "장착";
        }
        if (buyButton) buyButton.gameObject.SetActive(!owned);
    }

    public void Deselect()
    {
        if (highlight)   highlight  .SetActive(false);
        SetChildrenScale(1f);

        if (equipButton) equipButton.gameObject.SetActive(false);
        if (buyButton)   buyButton  .gameObject.SetActive(false);
    }

    void SetChildrenScale(float scale)
    {
        foreach (Transform child in transform)
            child.localScale = Vector3.one * scale;
    }
}
