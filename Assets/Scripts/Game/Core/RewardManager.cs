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

    private const string K_Gold    = "RW_Gold";
    private const string K_Exp     = "RW_Exp";
    private const string K_Ruby    = "RW_Ruby";
    private const string K_Level   = "RW_Level";
    private const string K_Stamina = "RW_Stamina";

    private int _gold;
    private int _exp;
    private int _ruby;
    private int _level;
    private int _stamina;

    public static event System.Action OnChanged;
    // 재화 직접 지급 시 발생 — (타입, 수량, 스크린 시작 위치)
    public static event System.Action<RewardType, int, Vector2> OnCurrencyFlying;

    public const int MaxStamina = 30;

    public int Gold    => _gold;
    public int Exp     => _exp;
    public int Ruby    => _ruby;
    public int Level   => _level;
    public int Stamina => _stamina;

    // ── 초기화 ────────────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        Load();
    }

    private void Load()
    {
        _gold    = PlayerPrefs.GetInt(K_Gold,    0);
        _exp     = PlayerPrefs.GetInt(K_Exp,     0);
        _ruby    = PlayerPrefs.GetInt(K_Ruby,    0);
        _level   = Mathf.Max(1, PlayerPrefs.GetInt(K_Level, 1));
        _stamina = PlayerPrefs.GetInt(K_Stamina, MaxStamina);
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

    public void GiveReward(RewardItem item)
    {
        switch (item.type)
        {
            case RewardType.Gold:    _gold    += item.amount; break;
            case RewardType.Exp:     _exp     += item.amount; break;
            case RewardType.Ruby:    _ruby    += item.amount; break;
            case RewardType.Stamina: _stamina += item.amount; break;
            case RewardType.EquipFrag:
                PlayerPrefs.SetInt("EquipFrag", PlayerPrefs.GetInt("EquipFrag", 0) + item.amount);
                break;
            case RewardType.WareFrag:
                PlayerPrefs.SetInt("WareFrag", PlayerPrefs.GetInt("WareFrag", 0) + item.amount);
                break;
            case RewardType.EquipTopMat:   EquipmentInventory.Shared?.AddMat(EquipCategory.TOP,   item.amount); break;
            case RewardType.EquipcloakMat: EquipmentInventory.Shared?.AddMat(EquipCategory.CLOAK, item.amount); break;
            case RewardType.EquipRingMat:  EquipmentInventory.Shared?.AddMat(EquipCategory.RING,  item.amount); break;
            case RewardType.EquipNeckMat:  EquipmentInventory.Shared?.AddMat(EquipCategory.NECK,  item.amount); break;
            case RewardType.EquipFootMat:  EquipmentInventory.Shared?.AddMat(EquipCategory.FOOT,  item.amount); break;
            case RewardType.EquipHeadMat:  EquipmentInventory.Shared?.AddMat(EquipCategory.HEAD,  item.amount); break;
            case RewardType.RelicScroll:   RelicInventory.Shared?.AddEnhanceMaterial(item.amount); break;
            default:
                // 뽑기권류: TicketManager 위임
                TicketManager.Add(item.type, item.amount);
                return;
        }
        CheckLevelUp();
        Save();
        OnChanged?.Invoke();
    }

    public void GiveRewards(RewardItem[] items)
    {
        if (items == null) return;
        foreach (var item in items) GiveReward(item);
    }

    // ── 통합 보상 분배 ────────────────────────────────────────────────────
    /// <summary>
    /// 모든 아이템을 즉시 지급. 비-재화 아이템은 PopUpInventory에 수령 목록 표시.
    /// 아웃게임·인게임 공통 보상 수령 진입점.
    /// </summary>
    public void Dispatch(RewardItem item, Sprite icon = null, Vector2 screenFrom = default)
    {
        GiveReward(item);
        if (IsDirectType(item.type))
            OnCurrencyFlying?.Invoke(item.type, item.amount, screenFrom);
        else
            PopUpInventory.Shared?.AddReward(item, icon);
    }

    public void DispatchAll(RewardItem[] items, Sprite[] icons = null)
    {
        if (items == null) return;
        for (int i = 0; i < items.Length; i++)
        {
            var icon = (icons != null && i < icons.Length) ? icons[i] : null;
            Dispatch(items[i], icon);
        }
    }

    private static bool IsDirectType(RewardType t) =>
        t == RewardType.Gold || t == RewardType.Exp || t == RewardType.Ruby || t == RewardType.Stamina;

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
        PlayerPrefs.SetInt(K_Gold,    _gold);
        PlayerPrefs.SetInt(K_Exp,     _exp);
        PlayerPrefs.SetInt(K_Ruby,    _ruby);
        PlayerPrefs.SetInt(K_Level,   _level);
        PlayerPrefs.SetInt(K_Stamina, _stamina);
        PlayerPrefs.Save();
        UIDataBus.BroadcastCurrencyChanged();
    }
}
