using UnityEngine;

/// <summary>
/// 아웃게임 재화(골드·계정경험치·루비)와 계정 레벨을 관리합니다.
/// DontDestroy=true로 씬 전환 후에도 유지됩니다.
///
/// 레벨 공식: expToNext(lv) = 200 + (lv-1) × 100
///   Lv1→2: 200  Lv5→6: 600  Lv10→11: 1100  Lv20→21: 2100
/// </summary>
public class RewardManager : MonoBehaviourSingleton<RewardManager>
{
    protected override bool DontDestroy => true;

    public const int MaxLevel = 50;

    private const string K_Gold  = "RW_Gold";
    private const string K_Exp   = "RW_Exp";
    private const string K_Ruby  = "RW_Ruby";
    private const string K_Level = "RW_Level";

    private int _gold;
    private int _exp;
    private int _ruby;
    private int _level;

    public static event System.Action OnChanged;

    public int Gold  => _gold;
    public int Exp   => _exp;
    public int Ruby  => _ruby;
    public int Level => _level;

    // ── 초기화 ────────────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        Load();
    }

    private void Load()
    {
        _gold  = PlayerPrefs.GetInt(K_Gold,  0);
        _exp   = PlayerPrefs.GetInt(K_Exp,   0);
        _ruby  = PlayerPrefs.GetInt(K_Ruby,  0);
        _level = Mathf.Max(1, PlayerPrefs.GetInt(K_Level, 1));
    }

    // ── 보상 지급 ─────────────────────────────────────────────────────────
    public void GiveReward(RunReward reward)
    {
        _gold += reward.gold;
        _exp  += reward.exp;
        _ruby += reward.ruby;
        CheckLevelUp();
        Save();
        OnChanged?.Invoke();
    }

    public bool SpendGold(int amount)
    {
        if (_gold < amount) return false;
        _gold -= amount;
        Save();
        OnChanged?.Invoke();
        return true;
    }

    public bool SpendRuby(int amount)
    {
        if (_ruby < amount) return false;
        _ruby -= amount;
        Save();
        OnChanged?.Invoke();
        return true;
    }

    // ── 레벨업 ────────────────────────────────────────────────────────────
    private void CheckLevelUp()
    {
        while (_level < MaxLevel)
        {
            int needed = ExpToNext(_level);
            if (_exp < needed) break;
            _exp   -= needed;
            _level++;
        }
    }

    /// <summary>현재 레벨에서 다음 레벨까지 필요한 경험치.</summary>
    public int ExpToNext(int level) => 200 + (level - 1) * 100;

    // ── 저장 ──────────────────────────────────────────────────────────────
    private void Save()
    {
        PlayerPrefs.SetInt(K_Gold,  _gold);
        PlayerPrefs.SetInt(K_Exp,   _exp);
        PlayerPrefs.SetInt(K_Ruby,  _ruby);
        PlayerPrefs.SetInt(K_Level, _level);
        PlayerPrefs.Save();
    }
}
