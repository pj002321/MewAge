using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 홈 장비 패널.
/// 상단: 캐릭터 뷰 + 장착 슬롯 6개 (좌3/우3) + ATK/HP 스탯
/// 하단: 보유 장비 스크롤 목록 (탭: 등급별 · 나의 장비 · 합성)
/// </summary>
public class HomeEquipPanel : MonoBehaviour
{
    [Header("장착 슬롯 (0~2=왼쪽, 3~5=오른쪽)")]
    public Image[] equipFrames = new Image[6];
    public Image[] equipIcons  = new Image[6];

    [Header("스탯")]
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI hpText;

    [Header("탭 버튼 (등급별 · 나의장비 · 합성)")]
    public Button tabGrade;
    public Button tabMyEquip;
    public Button tabSynth;

    [Header("탭 컬러")]
    public Color tabActiveColor   = new Color(1f,  0.80f, 0.20f, 1f);
    public Color tabInactiveColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    [Header("인벤토리 Content")]
    public Transform myEquipContent;   // GridLayoutGroup content
    public Transform gradeContent;
    public Transform synthContent;

    void Start()
    {
        tabGrade?  .onClick.AddListener(() => ShowTab(0));
        tabMyEquip?.onClick.AddListener(() => ShowTab(1));
        tabSynth?  .onClick.AddListener(() => ShowTab(2));
        ShowTab(1);
    }

    void OnEnable() => RefreshEquipSlots();

    public void ShowTab(int index)
    {
        gradeContent?  .gameObject.SetActive(index == 0);
        myEquipContent?.gameObject.SetActive(index == 1);
        synthContent?  .gameObject.SetActive(index == 2);

        SetTabColor(tabGrade,   index == 0);
        SetTabColor(tabMyEquip, index == 1);
        SetTabColor(tabSynth,   index == 2);
    }

    void SetTabColor(Button btn, bool active)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img) img.color = active ? tabActiveColor : tabInactiveColor;
    }

    public void RefreshEquipSlots()
    {
        var inv = PlayerInventory.Shared;
        for (int i = 0; i < 6; i++)
        {
            var data = inv != null && i < inv.Equipped.Count ? inv.Equipped[i] : null;
            bool has = data != null;

            if (equipIcons != null && i < equipIcons.Length && equipIcons[i] != null)
            {
                equipIcons[i].enabled = has;
                if (has && data.icon != null) equipIcons[i].sprite = data.icon;
            }
            if (equipFrames != null && i < equipFrames.Length && equipFrames[i] != null)
                equipFrames[i].color = has ? WeaponGradeColors.Get(data.grade)
                                           : new Color(0.3f, 0.3f, 0.3f, 0.8f);
        }

        if (attackText) attackText.text = "—";
        if (hpText)     hpText.text     = "—";
    }
}
