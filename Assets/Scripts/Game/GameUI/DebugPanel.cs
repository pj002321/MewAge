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
    [SerializeField] private Button   waveEndButton;
    [SerializeField] private Button   invincibleButton;
    [SerializeField] private Button   levelUpButton;

    [Header("웨이브 점프 패널")]
    [SerializeField] private Button    waveListToggle;  // "웨이브 이동 ▼" 토글 버튼
    [SerializeField] private Transform waveListContent; // 버튼 목록 컨테이너
    [SerializeField] private Button[]  waveButtons;     // 씬에 배치된 버튼 1~N (순서대로 텍스트·리스너 자동 설정)

    [Header("무기 선택 패널")]
    [SerializeField] private Button    weaponListToggle;
    [SerializeField] private Transform weaponListContent;
    [SerializeField] private Button[]  weaponButtonsCommon;    // 일반 5개
    [SerializeField] private Button[]  weaponButtonsRare;      // 고급 5개
    [SerializeField] private Button[]  weaponButtonsEpic;      // 희귀 5개
    [SerializeField] private Button[]  weaponButtonsLegendary; // 전설 5개
    [SerializeField] private Button[]  weaponButtonsMythic;    // 신화 5개

    private bool _menuUnlocked    = false;
    private bool _invincible      = false;
    private bool _contentOpen     = true;
    private bool _waveListOpen    = false;
    private bool _weaponListOpen  = false;

    private Coroutine _waveStagger;
    private Coroutine _weaponStagger;

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

        waveEndButton?.onClick.AddListener(() => GameManager.Shared?.DebugEndWave());
        invincibleButton?.onClick.AddListener(OnToggleInvincible);
        levelUpButton?.onClick.AddListener(OnLevelUp);

        waveListToggle?.onClick.AddListener(OnToggleWaveList);
        waveListContent?.gameObject.SetActive(false);

        weaponListToggle?.onClick.AddListener(OnToggleWeaponList);
        weaponListContent?.gameObject.SetActive(false);
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
        BuildWaveButtons();
        BuildWeaponButtons();
    }

    // ── 씬 전환 ──────────────────────────────────────────────────────

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;
        RefreshContent(scene.name);
        if (IsGameScene(scene.name)) { BuildWaveButtons(); BuildWeaponButtons(); }
        waveListContent?.gameObject.SetActive(false);
        weaponListContent?.gameObject.SetActive(false);
        _waveListOpen   = false;
        _weaponListOpen = false;
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

    public void OnToggleInvincible()
    {
        _invincible = !_invincible;
        PlayerController.Instance?.SetDebugInvincible(_invincible);
        var label = invincibleButton?.GetComponentInChildren<TextMeshProUGUI>();
        if (label) label.text = _invincible ? "무적 ON" : "무적 OFF";
    }

    public void OnLevelUp() => PlayerStats.Shared?.DebugLevelUp();

    // ── 웨이브 점프 패널 ──────────────────────────────────────────────

    private void OnToggleWaveList()
    {
        _waveListOpen = !_waveListOpen;

        if (_waveListOpen)
        {
            if (_waveStagger != null) StopCoroutine(_waveStagger);
            _waveStagger = StartCoroutine(StaggerShow(waveListContent));
        }
        else
        {
            if (_waveStagger != null) { StopCoroutine(_waveStagger); _waveStagger = null; }
            waveListContent?.gameObject.SetActive(false);
        }
    }

    private void BuildWaveButtons()
    {
        if (waveButtons == null) return;

        for (int i = 0; i < waveButtons.Length; i++)
        {
            var btn = waveButtons[i];
            if (btn == null) continue;

            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label) label.text = (i + 1).ToString();

            btn.onClick.RemoveAllListeners();
            int wave = i + 1;
            btn.onClick.AddListener(() => GameManager.Shared?.DebugJumpToWave(wave));
        }
    }

    // ── 무기 선택 패널 ────────────────────────────────────────────────

    private void OnToggleWeaponList()
    {
        _weaponListOpen = !_weaponListOpen;

        if (_weaponListOpen)
        {
            if (_weaponStagger != null) StopCoroutine(_weaponStagger);
            _weaponStagger = StartCoroutine(StaggerShow(weaponListContent));
        }
        else
        {
            if (_weaponStagger != null) { StopCoroutine(_weaponStagger); _weaponStagger = null; }
            weaponListContent?.gameObject.SetActive(false);
        }
    }

    private void BuildWeaponButtons()
    {
        _debugWeaponList.Clear();

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

        BindGradeButtons(weaponButtonsCommon,    0);
        BindGradeButtons(weaponButtonsRare,      1);
        BindGradeButtons(weaponButtonsEpic,      2);
        BindGradeButtons(weaponButtonsLegendary, 3);
        BindGradeButtons(weaponButtonsMythic,    4);
    }

    private void BindGradeButtons(Button[] buttons, int gradeOrder)
    {
        if (buttons == null) return;

        var gradeWeapons = _debugWeaponList.FindAll(r => GradeOrder(r.f_등급) == gradeOrder);

        for (int i = 0; i < buttons.Length; i++)
        {
            var btn = buttons[i];
            if (btn == null) continue;

            btn.onClick.RemoveAllListeners();

            if (i < gradeWeapons.Count)
            {
                var row   = gradeWeapons[i];
                var label = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (label) label.text = row.f_이름;

                btn.gameObject.SetActive(true);
                btn.onClick.AddListener(() =>
                {
                    var weapon = WeaponData.FromDB(row);
                    if (weapon != null) PlayerInventory.Shared?.QuietEquip(weapon);
                });
            }
            else
            {
                btn.gameObject.SetActive(false);
            }
        }
    }

    private System.Collections.IEnumerator StaggerShow(Transform content, float interval = 0.008f)
    {
        if (content == null) yield break;

        foreach (Transform child in content)
            child.gameObject.SetActive(false);

        content.gameObject.SetActive(true);

        var wait = new WaitForSecondsRealtime(interval);
        foreach (Transform child in content)
        {
            child.gameObject.SetActive(true);
            yield return wait;
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
