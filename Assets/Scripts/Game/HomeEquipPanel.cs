using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 홈 화면 장비 패널.
/// - 상단 캐릭터뷰: 장착 슬롯 6개 (좌3 우3) + 스탯 표시
/// - 내부 탭: 장비 / 성물 전환
/// - 하단 스크롤: 탭에 따라 보유 아이템 표시
/// </summary>
public class HomeEquipPanel : MonoBehaviour
{
    [Header("장착 슬롯 (0~2 = 왼쪽, 3~5 = 오른쪽)")]
    public Image[] equipFrames;     // 6개 슬롯 Frame Image
    public Image[] equipIcons;      // 6개 슬롯 Icon Image

    [Header("스탯 텍스트")]
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI hpText;

    [Header("내부 탭 버튼")]
    public Button tabWeapon;       // 장비
    public Button tabArtifact;     // 성물

    [Header("탭 컬러")]
    public Color tabActiveColor   = new Color(1f,  0.8f, 0.2f, 1f);
    public Color tabInactiveColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    [Header("스크롤 Content 루트")]
    public Transform weaponContent;    // 장비 탭 GridLayout content
    public Transform artifactContent;  // 성물 탭 GridLayout content

    // ────────────────────────────────────────────────────────────────

    void Start()
    {
        tabWeapon?.onClick.AddListener(() => ShowTab(0));
        tabArtifact?.onClick.AddListener(() => ShowTab(1));
        ShowTab(0);
    }

    void OnEnable()
    {
        RefreshEquipSlots();
    }

    // ────────────────────────────────────────────────────────────────
    // 탭 전환
    // ────────────────────────────────────────────────────────────────

    public void ShowTab(int index)
    {
        weaponContent?.gameObject.SetActive(index == 0);
        artifactContent?.gameObject.SetActive(index == 1);
        SetTabColor(tabWeapon,   index == 0);
        SetTabColor(tabArtifact, index == 1);
    }

    void SetTabColor(Button btn, bool active)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img) img.color = active ? tabActiveColor : tabInactiveColor;
    }

    // ────────────────────────────────────────────────────────────────
    // 장착 슬롯 갱신
    // ────────────────────────────────────────────────────────────────

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
                if (has && data.icon != null)
                    equipIcons[i].sprite = data.icon;
            }

            if (equipFrames != null && i < equipFrames.Length && equipFrames[i] != null)
                equipFrames[i].color = has ? WeaponGradeColors.Get(data.grade) : new Color(0.3f, 0.3f, 0.3f, 1f);
        }

        // 스탯 표시 (PlayerInventory or PlayerStats 연동 가능)
        if (attackText) attackText.text = "공격력  —";
        if (hpText)     hpText.text     = "HP  —";
    }
}
