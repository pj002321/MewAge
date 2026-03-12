using UnityEngine;

/// <summary>
/// 플레이어의 골드·경험치·레벨을 관리합니다.
/// DropItem이 수집될 때 호출되며, 변경 시 GameManager UI를 동기화합니다.
/// </summary>
public class PlayerStats : MonoBehaviourSingleton<PlayerStats>
{
    [Header("레벨 설정")]
    [SerializeField] private int baseExpPerLevel = 50;  // 레벨 1→2 에 필요한 경험치
    [SerializeField] private float expScalePerLevel = 1.4f; // 레벨업마다 필요 경험치 배율

    private int gold;
    private int exp;
    private int level = 1;
    private int expToNext;

    protected override bool DontDestroy => false;

    protected override void Awake()
    {
        base.Awake();
        expToNext = baseExpPerLevel;
        SyncUI();
    }

    public void AddGold(int amount)
    {
        gold += amount;
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
        exp += amount;
        while (exp >= expToNext)
        {
            exp     -= expToNext;
            level++;
            expToNext = Mathf.RoundToInt(baseExpPerLevel * Mathf.Pow(expScalePerLevel, level - 1));
            Debug.Log($"[PlayerStats] Level Up! → {level}");
        }
        SyncUI();
    }

    private void SyncUI()
    {
        GameManager.Shared?.UpdateGoldUI(gold);
        GameManager.Shared?.UpdateExpUI(exp, expToNext, level);
    }

    public int Gold  => gold;
    public int Exp   => exp;
    public int Level => level;
}
