using UnityEngine;
using UnityEngine.UI;

/// 레드닷 Image GameObject에 직접 부착. 알림이 없으면 Image 비활성, 있으면 활성.
public class AlarmNoti : MonoBehaviour
{
    [SerializeField] private NotificationKey _key;

    Image _image;

    void Awake()
    {
        _image = GetComponent<Image>();
        SetVisible(false);
    }

    void OnEnable()
    {
        if (NotificationManager.Shared == null) return;
        NotificationManager.Shared.OnNotificationChanged += HandleChange;
        SetVisible(NotificationManager.Shared.Get(_key));
    }

    void OnDisable()
    {
        if (NotificationManager.Shared != null)
            NotificationManager.Shared.OnNotificationChanged -= HandleChange;
    }

    void HandleChange(NotificationKey key, bool active)
    {
        if (key == _key) SetVisible(active);
    }

    void SetVisible(bool visible)
    {
        if (_image) _image.enabled = visible;
    }
}
