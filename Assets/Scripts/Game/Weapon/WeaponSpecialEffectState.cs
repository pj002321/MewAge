/// <summary>
/// 무기 특수효과 중 패시브/글로벌 상태를 저장합니다.
/// WeaponPoint.Init()/OnDestroy()가 장착/해제 시 갱신합니다.
/// </summary>
public static class WeaponSpecialEffectState
{
    private static int   _bossDmgReduceCount;
    private static float _bossDmgReducePct;

    /// <summary>보스 피해 감소율 (0~1). PlayerController.TakeDamage에서 사용.</summary>
    public static float BossDamageReductionPct => _bossDmgReducePct;

    public static void AddBossDamageReduction(float pct = 0.30f)
    {
        _bossDmgReduceCount++;
        _bossDmgReducePct = UnityEngine.Mathf.Min(_bossDmgReducePct + pct, 0.75f);
    }

    public static void RemoveBossDamageReduction(float pct = 0.30f)
    {
        _bossDmgReduceCount = UnityEngine.Mathf.Max(0, _bossDmgReduceCount - 1);
        _bossDmgReducePct   = _bossDmgReduceCount > 0
            ? UnityEngine.Mathf.Max(0f, _bossDmgReducePct - pct)
            : 0f;
    }

    public static void Reset()
    {
        _bossDmgReduceCount = 0;
        _bossDmgReducePct   = 0f;
    }
}
