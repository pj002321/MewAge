using Mewage.DB;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 캐릭터 레벨업 패널.
/// HomeMemberPanel.ToggleLevelUpPanel()로 열고 닫습니다.
///
/// Inspector 연결:
///   scrollIcon        — 영웅스크롤 아이콘 (RewardVisualConfig MAT_HERO)
///   scrollCountText   — 영웅스크롤 보유 수
///   fragIcon          — 영웅조각 아이콘 (캐릭터 전용 조각)
///   fragCountText     — 영웅조각 보유 수 / 필요 수
///   levelUpButton     — 레벨업 버튼
///   closeButton       — 닫기 버튼
/// </summary>
public class CharacterLevelUpPanel : MonoBehaviour
{
    [Header("영웅스크롤")]
    [SerializeField] private Image           scrollIcon;
    [SerializeField] private TextMeshProUGUI scrollCountText;

    [Header("영웅조각")]
    [SerializeField] private Image           fragIcon;
    [SerializeField] private TextMeshProUGUI fragCountText;

    [Header("버튼")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button levelUpButton;

    private const string HexGreen = "#00C800";
    private const string HexRed   = "#FF3232";

    private const int ScrollCost  = 1;
    public  const int FragCost    = 10;

    private DB_캐릭터 _currentData;

    private void Awake()
    {
        closeButton  ?.onClick.AddListener(() => gameObject.SetActive(false));
        levelUpButton?.onClick.AddListener(OnLevelUpClicked);
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        RewardManager.OnChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        RewardManager.OnChanged -= Refresh;
    }

    public void Show(DB_캐릭터 data)
    {
        _currentData = data;
        gameObject.SetActive(true);
        Refresh();
    }

    // ── 내부 ─────────────────────────────────────────────────────
    private void Refresh()
    {
        var cfg = RewardVisualConfig.Shared;

        // 영웅스크롤 아이콘
        if (scrollIcon)
        {
            scrollIcon.sprite = cfg?.GetIcon("MAT_HERO");
            scrollIcon.SetNativeSize();
        }

        // 영웅스크롤 개수
        int scrollCount = RewardManager.Shared?.CharMat ?? 0;
        if (scrollCountText)
        {
            string c = scrollCount >= ScrollCost ? HexGreen : HexRed;
            scrollCountText.text = $"<color={c}>{scrollCount}</color>/{ScrollCost}";
        }

        // 영웅조각 아이콘
        if (fragIcon && _currentData != null)
        {
            var spr = MemberVisualConfig.Shared?.GetFragmentIcon(_currentData.f_캐릭ID);
            fragIcon.enabled = spr != null;
            if (spr != null) { fragIcon.sprite = spr; fragIcon.SetNativeSize(); }
        }

        // 영웅조각 개수
        int fragCount = GetHeroFragCount();
        if (fragCountText)
        {
            string c = fragCount >= FragCost ? HexGreen : HexRed;
            fragCountText.text = $"<color={c}>{fragCount}</color>/{FragCost}";
        }

        // 레벨업 버튼 활성 여부
        if (levelUpButton)
            levelUpButton.interactable = scrollCount >= ScrollCost && fragCount >= FragCost;
    }

    private void OnLevelUpClicked()
    {
        if (_currentData == null) return;

        int scrollCount = RewardManager.Shared?.CharMat ?? 0;
        int fragCount   = GetHeroFragCount();

        if (scrollCount < ScrollCost || fragCount < FragCost)
        {
            PopupBaseNoti.Shared?.Show("레벨업 재료가 부족합니다");
            return;
        }

        // 재료 차감
        RewardManager.Shared?.GiveReward(new RewardItem(RewardType.CharMat, -ScrollCost));
        SpendHeroFrag(FragCost);

        // TODO: 캐릭터 레벨업 실제 처리 (PlayerCharacterManager 연동)
        Debug.Log($"[CharLevelUp] {_currentData.f_이름} 레벨업");
    }

    // ── 영웅조각 — PlayerPrefs per-character ─────────────────────
    private int GetHeroFragCount()
    {
        if (_currentData == null) return 0;
        return GetHeroFragCount(_currentData.f_캐릭ID);
    }

    public static int GetHeroFragCount(string charId)
        => PlayerPrefs.GetInt(FragKey(charId), 0);

    private void SpendHeroFrag(int amount)
    {
        if (_currentData == null) return;
        string key = FragKey(_currentData.f_캐릭ID);
        int    cur = PlayerPrefs.GetInt(key, 0);
        PlayerPrefs.SetInt(key, Mathf.Max(0, cur - amount));
        PlayerPrefs.Save();
        Refresh();
    }

    public static void AddHeroFrag(string charId, int amount)
    {
        string key = FragKey(charId);
        PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) + amount);
        PlayerPrefs.Save();
    }

    private static string FragKey(string charId) => $"HeroFrag_{charId}";
}
