using System.Collections.Generic;
using Mewage.DB;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// home@scene 에만 배치. DontDestroy로 씬 전환 후에도 유지.
///
/// 구조:
///   DebugPanel (root, 항상 active)
///     └─ overlay          ← PopUpMenu.Toggle() 로 show/hide
///          ├─ panelContainer (Button) ← 클릭 시 homeContent / gameContent 접기·펼치기
///          ├─ homeContent  ← home@scene 전용 버튼들
///          └─ gameContent  ← stage@scene 전용 버튼들
/// </summary>
public class DebugPanel : MonoBehaviourSingleton<DebugPanel>
{
    protected override bool DontDestroy => true;

    [Header("오버레이 (PopUpMenu 가 show/hide)")]
    [SerializeField] private GameObject overlay;
    [SerializeField] private Button     closeButton;

    [Header("하위 컨테이너 접기·펼치기 버튼")]
    [SerializeField] private Button panelContainer;

    [Header("씬별 콘텐츠 루트")]
    [SerializeField] private GameObject homeContent;
    [SerializeField] private GameObject gameContent;

    // ── 홈씬 전용 ────────────────────────────────────────────────────
    [Header("홈씬 (homeContent 하위)")]
    [SerializeField] private MainMenuPanel mainMenuPanel;
    [SerializeField] private Button        menuToggleButton; // label → GetComponentInChildren
    [SerializeField] private Button        resetDataButton;

    // ── 인게임 전용 ──────────────────────────────────────────────────
    [Header("인게임 (gameContent 하위)")]
    [SerializeField] private Button   skipWaveButton;
    [SerializeField] private Button[] bossWaveButtons;
    [SerializeField] private int[]    bossWaves = { 10, 20, 30 };
    [SerializeField] private Button   invincibleButton;
    [SerializeField] private Button   levelUpButton;

    [Header("웨이브 점프")]
    [SerializeField] private Button[] jumpButtons;
    [SerializeField] private int[]    jumpWaves = { 9, 10, 19, 20, 29, 30, 40 };

    [Header("무기 드롭다운")]
    [SerializeField] private TMP_Dropdown weaponDropdown;

    private bool _menuUnlocked  = false;   // 시작 시 잠금 상태
    private bool _invincible    = false;
    private bool _contentOpen   = true;

    private readonly List<DB_무기> _debugWeaponList = new();

    // ── 초기화 ───────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        if (Shared != this) return;

        SceneManager.sceneLoaded += OnSceneLoaded;

        overlay?.SetActive(false);

        closeButton?.onClick.AddListener(Hide);
        panelContainer?.onClick.AddListener(OnToggleContent);

        menuToggleButton?.onClick.AddListener(OnToggleMenuButtons);
        resetDataButton?.onClick.AddListener(OnResetData);

        skipWaveButton?.onClick.AddListener(OnSkipWave);
        invincibleButton?.onClick.AddListener(OnToggleInvincible);
        levelUpButton?.onClick.AddListener(OnLevelUp);

        for (int i = 0; i < bossWaveButtons.Length; i++)
        {
            int wave = i < bossWaves.Length ? bossWaves[i] : 10;
            bossWaveButtons[i]?.onClick.AddListener(() => GameManager.Shared?.DebugJumpToWave(wave));
        }
        for (int i = 0; i < jumpButtons.Length; i++)
        {
            int wave = i < jumpWaves.Length ? jumpWaves[i] : (i + 1) * 10;
            jumpButtons[i]?.onClick.AddListener(() => GameManager.Shared?.DebugJumpToWave(wave));
        }

        // onValueChanged 리스너는 PopulateWeaponDropdown 내부에서 갱신 후 등록합니다.
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public bool IsVisible => overlay != null && overlay.activeSelf;

    private void Start()
    {
        RefreshContent();
        PopulateWeaponDropdown();
    }

    // ── 씬 전환 ──────────────────────────────────────────────────────

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;
        RefreshContent(scene.name);
        if (IsGameScene(scene.name)) PopulateWeaponDropdown();
    }

    private static bool IsGameScene(string sceneName = null) =>
        (sceneName ?? SceneManager.GetActiveScene().name).Contains("stage");

    private void RefreshContent(string sceneName = null)
    {
        bool isGame = IsGameScene(sceneName);
        homeContent?.SetActive(!isGame);
        gameContent?.SetActive(isGame);
        _contentOpen = true;
        if (!isGame) ApplyMenuButtons();
    }

    private void ApplyMenuButtons()
    {
        if (mainMenuPanel == null) return;
        foreach (var btn in new[]
        {
            mainMenuPanel.storageButton,     mainMenuPanel.specialGiftButton,
            mainMenuPanel.dailymissionButton, mainMenuPanel.achievementButton,
            mainMenuPanel.attendanceButton,   mainMenuPanel.rankingButton,
            mainMenuPanel.inventoryButton,
        })
            if (btn) btn.gameObject.SetActive(_menuUnlocked);

        var label = menuToggleButton?.GetComponentInChildren<TextMeshProUGUI>();
        if (label) label.text = _menuUnlocked ? "메뉴 잠금" : "메뉴 해금";
    }

    // ── 패널 show / hide ─────────────────────────────────────────────

    public void Toggle()
    {
        if (overlay == null) return;
        if (overlay.activeSelf) Hide(); else Show();
    }

    public void Show()
    {
        RefreshContent();
        overlay?.SetActive(true);
    }

    public void Hide() => overlay?.SetActive(false);

    // ── 접기·펼치기 ──────────────────────────────────────────────────

    private void OnToggleContent()
    {
        _contentOpen = !_contentOpen;
        homeContent?.SetActive(!IsGameScene() && _contentOpen);
        gameContent?.SetActive(IsGameScene() && _contentOpen);
    }

    // ── 홈씬 핸들러 ──────────────────────────────────────────────────

    private void OnToggleMenuButtons()
    {
        _menuUnlocked = !_menuUnlocked;
        ApplyMenuButtons();
    }

    private void OnResetData()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        SoundManager.Shared?.StopBGM();
        this.DLog("[Debug] PlayerPrefs 전체 초기화 완료 — 인트로 씬으로 이동");
        SceneLoadManager.LoadScene("introloading@scene");
    }

    // ── 인게임 핸들러 ─────────────────────────────────────────────────

    public void OnSkipWave()    => GameManager.Shared?.DebugSkipWave();

    public void OnToggleInvincible()
    {
        _invincible = !_invincible;
        PlayerController.Instance?.SetDebugInvincible(_invincible);
        var label = invincibleButton?.GetComponentInChildren<TextMeshProUGUI>();
        if (label) label.text = _invincible ? "무적 ON" : "무적 OFF";
    }

    public void OnLevelUp() => PlayerStats.Shared?.DebugLevelUp();

    // ── 무기 드롭다운 ─────────────────────────────────────────────────

    private void OnWeaponDropdownChanged(int index)
    {
        if (index < 0 || index >= _debugWeaponList.Count) return;
        var weapon = WeaponData.FromDB(_debugWeaponList[index]);
        if (weapon == null) return;
        var inv = PlayerInventory.Shared;
        if (inv == null) return;
        inv.QuietEquip(weapon);
    }

    private void PopulateWeaponDropdown()
    {
        if (weaponDropdown == null) return;
        _debugWeaponList.Clear();

        // ClearOptions() 내부에서 value=0 이 설정되면 onValueChanged 가 발화할 수 있으므로
        // 갱신 중에는 리스너를 제거합니다.
        weaponDropdown.onValueChanged.RemoveListener(OnWeaponDropdownChanged);
        weaponDropdown.ClearOptions();

        string chapterID = PlayerPrefs.GetString("SelectedChapterID", "C1_S1");
        int chapter = 1;
        var parts = chapterID.Split('_');
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
        weaponDropdown.onValueChanged.AddListener(OnWeaponDropdownChanged);

        if (weaponDropdown.template != null)
        {
            var tog = weaponDropdown.template.GetComponentInChildren<Toggle>(true);
            float h = tog != null ? ((RectTransform)tog.transform).rect.height : 30f;
            if (h < 1f) h = 30f;
            weaponDropdown.template.sizeDelta = new Vector2(
                weaponDropdown.template.sizeDelta.x,
                Mathf.Min(options.Count, 12) * h + 8f);
        }
    }

    private static int GradeOrder(string g) => g switch
    {
        "Common"    or "일반" => 0, "Rare"      or "레어" => 1,
        "Epic"      or "희귀" => 2, "Legendary" or "전설" => 3,
        "Mythic"    or "신화" => 4, _                    => 0,
    };
    private static string GradeLabel(string g) => g switch
    {
        "Common"    or "일반" => "일반", "Rare"      or "레어" => "레어",
        "Epic"      or "희귀" => "희귀", "Legendary" or "전설" => "전설",
        "Mythic"    or "신화" => "신화", _                    => g ?? "?",
    };
}
