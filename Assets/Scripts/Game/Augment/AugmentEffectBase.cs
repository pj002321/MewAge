using Mewage.DB;

public abstract class AugmentEffectBase
{
    protected DB_증강 Data { get; private set; }

    public void Initialize(DB_증강 data) => Data = data;

    public abstract void OnGain();
    public virtual void OnWaveEnd() { }
    public virtual void OnRemove() { }
}
