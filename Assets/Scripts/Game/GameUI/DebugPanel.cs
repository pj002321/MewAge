using System.Collections.Generic;
using Mewage.DB;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 디버그 전용 패널.
/// Inspector에서 skipWaveButton, bossWaveButton, waveLabel 연결.
/// jumpButtons[i] 는 jumpWaves[i] 웨이브로 즉시 점프합니다.
/// </summary>
public class DebugPanel : MonoBehaviour
{
    [Header("가시성 토글 (선택)")]
    [SerializeField] private Button          toggleVisibilityButton;
    [SerializeField] private GameObject      buttonContainer;

    [Header("UI 연결")]
    [SerializeField] private Button          skipWaveButton;
    [SerializeField] private Button          bossWaveButton;
    [SerializeField] private Button          invincibleButton;
    [SerializeField] private Button          levelUpButton;
    [SerializeField] private TextMeshProUGUI waveLabel;

    private bool isVisible    = true;
    private bool _invincible  = false;

    [Header("웨이브 점프 버튼 (jumpWaves 와 1:1 대응)")]
    [SerializeField] private Button[] jumpButtons;
    [SerializeField] private int[]    jumpWaves = { 9, 10, 19, 20, 29, 30, 40 };

    [Header("무기 디버그 장착")]
    [SerializeField] private TMP_Dropdown weaponDropdown;
    [SerializeField] private Button       equipWeaponButton;

    private readonly List<DB_무기> _debugWeaponList = new();

    private void Awake()
    {
        // 에디터/디버그 빌드 한정 활성화 로직 제거. (항상 PlayerPrefs 기준으로만 판단)
        if (PlayerPrefs.GetInt("DebugMode", 1) == 0)
        {
            gameObject.SetActive(false);
            return;
        }
        toggleVisibilityButton?.onClick.AddListener(OnToggleVisibility);

        skipWaveButton?.onClick.AddListener(OnSkipWave);
        bossWaveButton?.onClick.AddListener(OnBossWave);
        invincibleButton?.onClick.AddListener(OnToggleInvincible);
        levelUpButton?.onClick.AddListener(OnLevelUp);

        for (int i = 0; i < jumpButtons.Length; i++)
        {
            int wave = i < jumpWaves.Length ? jumpWaves[i] : (i + 1) * 10;
            jumpButtons[i]?.onClick.AddListener(() => OnJumpToWave(wave));
        }

        equipWeaponButton?.onClick.AddListener(OnEquipSelectedWeapon);
    }

    private void Start()
    {
        PopulateWeaponDropdown();
    }

    private void Update()
    {
        RefreshLabel();
        // 키보드 단축키로 버튼 우회 테스트 (F1=SkipWave, F2=BossWave)
        if (Input.GetKeyDown(KeyCode.F1)) OnSkipWave();
        if (Input.GetKeyDown(KeyCode.F2)) OnBossWave();
    }

    public void OnSkipWave()
    {
        Debug.Log($"[Debug] SkipWave 버튼 클릭 — GameManager={GameManager.Shared}");
        GameManager.Shared?.DebugSkipWave();
    }

    public void OnBossWave()
    {
        Debug.Log($"[Debug] BossWave 버튼 클릭 — GameManager={GameManager.Shared}");
        GameManager.Shared?.DebugJumpToBossWave();
    }

    public void OnToggleInvincible()
    {
        _invincible = !_invincible;
        PlayerController.Instance?.SetDebugInvincible(_invincible);

        if (invincibleButton != null)
        {
            var label = invincibleButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label) label.text = _invincible ? "무적 ON" : "무적 OFF";
        }

        Debug.Log($"[Debug] 무적 {(_invincible ? "ON" : "OFF")}");
    }

    public void OnLevelUp()
    {
        Debug.Log($"[Debug] Level Up 버튼 클릭");
        PlayerStats.Shared?.DebugLevelUp();
    }

    public void OnJumpToWave(int wave)
    {
        Debug.Log($"[Debug] JumpToWave {wave} 버튼 클릭");
        GameManager.Shared?.DebugJumpToWave(wave);
    }

    private void OnToggleVisibility()
    {
        isVisible = !isVisible;
        if (buttonContainer != null)
        {
            buttonContainer.SetActive(isVisible);
        }
        else
        {
            if (skipWaveButton) skipWaveButton.gameObject.SetActive(isVisible);
            if (bossWaveButton) bossWaveButton.gameObject.SetActive(isVisible);
            if (levelUpButton) levelUpButton.gameObject.SetActive(isVisible);
            if (jumpButtons != null)
            {
                foreach (var btn in jumpButtons)
                {
                    if (btn) btn.gameObject.SetActive(isVisible);
                }
            }
        }
    }

    private void PopulateWeaponDropdown()
    {
        if (weaponDropdown == null) return;
        _debugWeaponList.Clear();
        weaponDropdown.ClearOptions();

        string chapterID = PlayerPrefs.GetString("SelectedChapterID", "C1_S1");
        int chapter = 1;
        string[] parts = chapterID.Split('_');
        if (parts.Length > 0 && parts[0].Length >= 2)
            int.TryParse(parts[0].Substring(1), out chapter);
        DB_무기.ForEachEntity(row =>
        {
            var w = WeaponData.FromDB(row);
            if (w != null && w.chapter == chapter) _debugWeaponList.Add(row);
        });

        _debugWeaponList.Sort((a, b) => GradeOrder(a.f_등급).CompareTo(GradeOrder(b.f_등급)));

        var options = new List<TMP_Dropdown.OptionData>();
        foreach (var row in _debugWeaponList)
            options.Add(new TMP_Dropdown.OptionData($"[{GradeLabel(row.f_등급)}] {row.f_이름}"));

        weaponDropdown.AddOptions(options);
        weaponDropdown.value = 0;
        weaponDropdown.RefreshShownValue();

        // 드롭다운 팝업 높이를 항목 수에 맞게 조정 (마지막 항목 잘림 방지)
        if (weaponDropdown.template != null)
        {
            var itemToggle = weaponDropdown.template.GetComponentInChildren<UnityEngine.UI.Toggle>(true);
            float itemH = itemToggle != null
                ? ((RectTransform)itemToggle.transform).rect.height
                : 30f;
            if (itemH < 1f) itemH = 30f;

            int visible = Mathf.Min(options.Count, 12);
            weaponDropdown.template.sizeDelta =
                new Vector2(weaponDropdown.template.sizeDelta.x, visible * itemH + 8f);
        }
    }

    private static int GradeOrder(string g) => g switch
    {
        "Common"    or "일반" => 0,
        "Rare"      or "레어" => 1,
        "Epic"      or "희귀" => 2,
        "Legendary" or "전설" => 3,
        "Mythic"    or "신화" => 4,
        _                    => 0,
    };

    private static string GradeLabel(string g) => g switch
    {
        "Common"    or "일반" => "일반",
        "Rare"      or "레어" => "레어",
        "Epic"      or "희귀" => "희귀",
        "Legendary" or "전설" => "전설",
        "Mythic"    or "신화" => "신화",
        _                    => g ?? "?",
    };

    public void OnEquipSelectedWeapon()
    {
        if (weaponDropdown == null || _debugWeaponList.Count == 0) return;
        int idx = weaponDropdown.value;
        if (idx < 0 || idx >= _debugWeaponList.Count) return;

        var inv = PlayerInventory.Shared;
        if (inv == null) { Debug.LogWarning("[Debug] PlayerInventory 없음"); return; }

        var data = WeaponData.FromDB(_debugWeaponList[idx]);
        inv.ForceAddToInventory(data);
        inv.EquipFromInventory(inv.Inventory.Count - 1);

        Debug.Log($"[Debug] 무기 장착: {data.weaponName} ({data.weaponType})");
    }

    private void RefreshLabel()
    {
        if (waveLabel == null || GameManager.Shared == null) return;
        int cur      = GameManager.Shared.CurrentWave;
        int nextBoss = GameManager.Shared.GetNextBossWave(cur);
        string bossStr = nextBoss > 0 ? nextBoss.ToString() : "없음";
        waveLabel.text = $"Wave {cur}  |  Next Boss: {bossStr}";
    }
}
