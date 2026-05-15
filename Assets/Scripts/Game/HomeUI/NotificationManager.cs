using System;
using System.Collections.Generic;

public class NotificationManager : MonoBehaviourSingleton<NotificationManager>
{
    protected override bool DontDestroy => false;

    readonly Dictionary<NotificationKey, bool> _states = new();

    public event Action<NotificationKey, bool> OnNotificationChanged;

    public void Set(NotificationKey key, bool active)
    {
        if (_states.TryGetValue(key, out bool current) && current == active) return;
        _states[key] = active;
        OnNotificationChanged?.Invoke(key, active);
    }

    public bool Get(NotificationKey key) =>
        _states.TryGetValue(key, out bool v) && v;
}
