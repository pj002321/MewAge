// 체력이 최대 HP의 20% 이하일 때 공격력 f_주수치% 증가. 체력 회복 시 해제.
public class AugmentLowHpAtk : AugmentEffectBase
{
    private bool _buffActive;

    public override void OnGain()   => GameEvents.OnHPChanged += OnHpChanged;
    public override void OnRemove()
    {
        GameEvents.OnHPChanged -= OnHpChanged;
        RemoveBuff();
    }

    private void OnHpChanged(float current, float max)
    {
        bool isLow = max > 0f && current / max < 0.2f;
        if (isLow && !_buffActive)       ApplyBuff();
        else if (!isLow && _buffActive)  RemoveBuff();
    }

    private void ApplyBuff()
    {
        _buffActive = true;
        PlayerStatManager.Shared?.AddPctMod(StatType.ATK, Data.f_주수치);
    }

    private void RemoveBuff()
    {
        if (!_buffActive) return;
        _buffActive = false;
        PlayerStatManager.Shared?.AddPctMod(StatType.ATK, -Data.f_주수치);
    }
}
