using UnityEngine;

/// <summary>
/// 플레이어의 골드·경험치·레벨을 관리합니다.
/// DropItem이 수집될 때 호출되며, 변경 시 GameManager UI를 동기화합니다.
/// </summary>
public class PlayerStats : MonoBehaviourSingleton<PlayerStats>
{
    [Header("레벨 설정")]
    [SerializeField] private int maxLevel          = 20;  // 만렙
    [SerializeField] private int baseExpPerLevel   = 20;  // 레벨 1→2 필요 경험치
    [SerializeField] private int expGrowthPerLevel = 50;  // 레벨마다 증가량

    [Header("시작 골드")]
    [SerializeField] private int startingGold = 2;

    private int gold;
    private int exp;
    private int level = 1;
    private int expToNext;

    // 흡수 반경 보너스 (AUG_REWARD_RNG)
    private float _magnetRadiusBonus;
    public float MagnetRadiusBonus => _magnetRadiusBonus;
    public void IncreaseMagnetRadius(float amount) => _magnetRadiusBonus += amount;

    protected override bool DontDestroy => false;

    // 레벨 lv에서 다음 레벨까지 필요한 경험치
    // lv=1:20 / lv=5:60 / lv=10:110 / lv=19:200
    private int CalcExpToNext(int lv) => baseExpPerLevel + (lv - 1) * expGrowthPerLevel;

    protected override void Awake()
    {
        base.Awake();
        expToNext = CalcExpToNext(1);
        gold      = startingGold;
    }

    private void Start()
    {
        SyncUI();  // GameManager Awake 완료 후 → goldText, expSlider 초기값 0 반영
    }

    public void AddGold(int amount)
    {
        gold += amount;
        GameManager.Shared?.AddSessionGold(amount);  // session tracker for Clear Panel
        SoundManager.Shared?.Play(SoundKey.Pickup_Gold);
        SyncUI();
    }

    public bool SpendGold(int amount)
    {
        if (gold < amount) return false;
        gold -= amount;
        SyncUI();
        return true;
    }

    public void AddExp(int amount)
    {
        if (level >= maxLevel) return;

        SoundManager.Shared?.Play(SoundKey.Pickup_Exp);
        GameManager.Shared?.AddSessionExp(amount);    // session tracker for Clear Panel
        exp += amount;
        while (exp >= expToNext && level < maxLevel)
        {
            exp      -= expToNext;
            level++;
            expToNext = CalcExpToNext(level);
            Debug.Log($"[PlayerStats] Level Up! → {level}");
            PlayerController.Instance?.HealHP(10f);
            FXManager.Shared?.SpawnPlayerText("+10", Color.green);
        }

        // 만렙 도달 시 exp 바를 0/200으로 고정
        if (level >= maxLevel)
        {
            exp       = 0;
            expToNext = CalcExpToNext(maxLevel - 1); // 200
        }

        SyncUI();
    }

    /// <summary>
    /// 디버그용 강제 레벨업 메서드입니다.
    /// 현재 레벨에서 다음 레벨로 가기 위한 정확한 경험치만을 추가합니다.
    /// </summary>
    public void DebugLevelUp()
    {
        if (level >= maxLevel) return;

        int requiredExp = expToNext - exp;
        exp += requiredExp;
        
        while (exp >= expToNext && level < maxLevel)
        {
            exp -= expToNext;
            level++;
            expToNext = CalcExpToNext(level);
            Debug.Log($"[PlayerStats] Debug Level Up! → {level}");
            PlayerController.Instance?.HealHP(10f);
            FXManager.Shared?.SpawnPlayerText("+10", Color.green);
        }

        if (level >= maxLevel)
        {
            exp = 0;
            expToNext = CalcExpToNext(maxLevel - 1);
        }

        SyncUI();
    }

    private void SyncUI()
    {
        GameManager.Shared?.UpdateGoldUI(gold);
        GameManager.Shared?.UpdateExpUI(exp, expToNext, level);
    }

    public int Gold      => gold;
    public int Exp       => exp;
    public int Level     => level;
    public int MaxLevel  => maxLevel;
    public int ExpToNext => expToNext;
}
