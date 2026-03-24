using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 홈 대원(캐릭터) 패널.
/// 상단 좌: 선택된 캐릭터 포트레이트 + 이름
/// 상단 우: 스킬 정보 (아이콘·이름·레벨별 설명)
/// 탭: 캐릭터 · 장비 · 룬
/// 하단: 보유 캐릭터 스크롤 그리드
/// </summary>
public class HomeMemberPanel : MonoBehaviour
{
    [Header("캐릭터 뷰 — 포트레이트 (좌상단)")]
    public Image           characterPortrait;
    public TextMeshProUGUI characterNameText;

    [Header("스킬 정보 패널 (우상단)")]
    public Image           skillIcon;
    public TextMeshProUGUI skillNameText;
    public Transform       skillLevelContainer;   // SkillLevelRow 들의 부모

    [Header("탭 버튼 (캐릭터 · 장비 · 룬)")]
    public Button tabCharacter;
    public Button tabEquip;
    public Button tabRune;

    [Header("탭 컬러")]
    public Color tabActiveColor   = new Color(1f,  0.80f, 0.20f, 1f);
    public Color tabInactiveColor = new Color(0.15f, 0.20f, 0.35f, 1f);

    [Header("스크롤 Content")]
    public Transform characterContent;

    void Start()
    {
        tabCharacter?.onClick.AddListener(() => ShowTab(0));
        tabEquip    ?.onClick.AddListener(() => ShowTab(1));
        tabRune     ?.onClick.AddListener(() => ShowTab(2));
        ShowTab(0);
    }

    public void ShowTab(int index)
    {
        SetTabColor(tabCharacter, index == 0);
        SetTabColor(tabEquip,     index == 1);
        SetTabColor(tabRune,      index == 2);
    }

    void SetTabColor(Button btn, bool active)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img) img.color = active ? tabActiveColor : tabInactiveColor;
        var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp) tmp.color = active ? Color.black : Color.white;
    }
}
