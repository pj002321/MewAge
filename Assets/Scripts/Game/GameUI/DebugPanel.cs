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
    [SerializeField] private Button        menuToggleButton;       // label → GetComponentInChildren
    [SerializeField] private Button        resetDataButton;
    [SerializeField] private Button        unlockAllChaptersButton;

    // ── 인게임 전용 ──────────────────────────────────────────────────
    [Header("인게임 (gameContent 하위)")]
    [SerializeField] private Button   waveEndButton;
    [SerializeField] private Button   invincibleButton;
    [SerializeField] private Button   infiniteTimeButton;
    [SerializeField] private Button   levelUpButton;
    [SerializeField] private Button   addGoldButton;

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

    private bool _invincible      = false;
    private bool _infiniteTime    = false;
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
        unlockAllChaptersButton?.onClick.AddListener(OnUnlockAllChapters);

        waveEndButton?.onClick.AddListener(() => GameManager.Shared?.DebugEndWave());
        invincibleButton?.onClick.AddListener(OnToggleInvincible);
        infiniteTimeButton?.onClick.AddListener(OnToggleInfiniteTime);
        levelUpButton?.onClick.AddListener(OnLevelUp);
        addGoldButton?.onClick.AddListener(() => PlayerStats.Shared?.AddGold(100));

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
        if (!IsGameScene(scene.name)) mainMenuPanel = FindObjectOfType<MainMenuPanel>(); // RefreshContent 전에 갱신
        RefreshContent(scene.name);
        if (IsGameScene(scene.name)) { BuildWaveButtons(); BuildWeaponButtons(); }
        else StartCoroutine(SyncMenuLabelAfterStart());
        waveListContent?.gameObject.SetActive(false);
        weaponListContent?.gameObject.SetActive(false);
        _waveListOpen   = false;
        _weaponListOpen = false;
    }

    // MainMenuPanel.Start()가 실행된 후 라벨을 동기화
    private System.Collections.IEnumerator SyncMenuLabelAfterStart()
    {
        yield return null;
        RefreshMenuToggleLabel();
    }

    private static bool IsGameScene(string sceneName = null) =>
        (sceneName ?? SceneManager.GetActiveScene().name).Contains("stage");

    private void RefreshContent(string sceneName = null)
    {
        bool isGame = IsGameScene(sceneName);
        homeContent?.SetActive(!isGame);
        gameContent?.SetActive(isGame);
        _contentOpen = true;
        if (!isGame) RefreshMenuToggleLabel();
    }

    private void RefreshMenuToggleLabel()
    {
        var label = menuToggleButton?.GetComponentInChildren<TextMeshProUGUI>();
        if (label == null || mainMenuPanel == null) return;
        label.text = mainMenuPanel.IsMenuUnlocked ? "메뉴 잠금" : "메뉴 해금";
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
        mainMenuPanel?.DebugToggleMenu();
        RefreshMenuToggleLabel();
    }

    private void OnResetData()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        this.DLog("[Debug] PlayerPrefs 전체 초기화 완료 — 앱 종료");
        PopupBaseNoti.Shared?.Show("원점으로 되돌아갑니다.");
        StartCoroutine(QuitAfterDelay(1.8f));
    }

    private System.Collections.IEnumerator QuitAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnUnlockAllChapters()
    {
        Mewage.DB.DB_챕터.ForEachEntity(row =>
        {
            if (string.IsNullOrEmpty(row.f_챕터ID)) return;
            // C4 이상은 강제 잠금 — 디버그 전체 해금에서도 제외
            string[] parts = row.f_챕터ID.Split('_');
            if (parts.Length >= 1 && parts[0].Length >= 2 &&
                int.TryParse(parts[0].Substring(1), out int chapterNum) &&
                chapterNum >= 4) return;
            PlayerPrefs.SetInt($"ChapterCleared_{row.f_챕터ID}", 1);
        });
        PlayerPrefs.Save();

        // mainMenuPanel이 stale(씬 리로드 후 destroyed)할 경우 동적으로 탐색
        var panel = mainMenuPanel != null ? mainMenuPanel : FindObjectOfType<MainMenuPanel>();
        panel?.RefreshStages();
        this.DLog("[Debug] 전 챕터 해금 완료");
    }

    // ── 인게임 핸들러 ─────────────────────────────────────────────────

    public void OnToggleInvincible()
    {
        _invincible = !_invincible;
        PlayerController.Instance?.SetDebugInvincible(_invincible);
        var label = invincibleButton?.GetComponentInChildren<TextMeshProUGUI>();
        if (label) label.text = _invincible ? "무적 ON" : "무적 OFF";
    }

    public void OnToggleInfiniteTime()
    {
        _infiniteTime = !_infiniteTime;
        GameManager.Shared?.DebugSetInfiniteTime(_infiniteTime);
        var label = infiniteTimeButton?.GetComponentInChildren<TextMeshProUGUI>();
        if (label) label.text = _infiniteTime ? "무제한 ON" : "무제한 OFF";
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
                    if (weapon != null) DebugAddWeapon(weapon);
                });
            }
            else
            {
                btn.gameObject.SetActive(false);
            }
        }
    }

    // ── 디버그 무기 추가 ──────────────────────────────────────────────

    private static void DebugAddWeapon(WeaponData weapon)
    {
        var inv = PlayerInventory.Shared;
        if (inv == null) return;

        // 0. 이미 같은 무기가 2개 쌓인 성급이 있으면 그 성급으로 맞춤
        //    (FromDB는 항상 1성이므로, 2성×2개 장착 상태에서 3번째를 눌러도 합성이 트리거되도록)
        weapon = ResolveDebugStar(weapon, inv);

        // 1. 인벤토리에 추가 — 꽉 찬 경우 토스트
        if (!inv.TryAddToInventory(weapon))
        {
            PopupBaseNoti.Shared?.Show("인벤토리가 가득 찼습니다.");
            return;
        }

        // 2. 같은 이름·성 3개 모이면 자동 합성 (TryMergeAll이 슬롯 배치까지 처리)
        if (weapon.star < 5 && inv.CanMergeAll(weapon.weaponName, weapon.star))
        {
            inv.TryMergeAll(weapon.weaponName, weapon.star, weapon.UpgradedTo(weapon.star + 1));
            return;
        }

        // 3. 장착 빈 슬롯 있으면 방금 추가된 인벤 마지막 항목을 바로 장착
        bool hasEmpty = inv.Equipped.Count < PlayerInventory.MaxEquipped;
        if (!hasEmpty)
            for (int i = 0; i < inv.Equipped.Count; i++)
                if (inv.Equipped[i] == null) { hasEmpty = true; break; }

        if (hasEmpty && inv.Inventory.Count > 0)
            inv.EquipFromInventory(inv.Inventory.Count - 1);
    }

    // 장착+인벤 합산으로 같은 이름이 2개인 성급을 찾아 그 성급으로 반환합니다.
    // 해당하는 성급이 없으면 원본(1성)을 그대로 반환합니다.
    private static WeaponData ResolveDebugStar(WeaponData weapon, PlayerInventory inv)
    {
        for (int s = 4; s >= 1; s--)
        {
            if (inv.CountAll(weapon.weaponName, s) == 2)
                return weapon.UpgradedTo(s);
        }
        return weapon;
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
