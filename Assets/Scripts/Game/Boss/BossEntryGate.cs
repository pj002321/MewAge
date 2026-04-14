using System;
using UnityEngine;

/// <summary>
/// 보스 웨이브 진입/퇴장 시 ExitPoint 오브젝트에 부착하는 트리거 컴포넌트.
/// Activate(callback) 호출로 활성화, Deactivate()로 비활성화.
/// 플레이어가 트리거에 진입하면 callback을 1회 호출합니다.
/// 그 외에는 항상 비활성화 상태를 유지합니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BossEntryGate : MonoBehaviour
{
    private Action _onEnter;
    private bool   _active;

    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
        gameObject.SetActive(false);
    }

    /// <summary>게이트를 활성화합니다. 플레이어가 진입하면 onEnter가 1회 호출됩니다.</summary>
    public void Activate(Action onEnter)
    {
        _onEnter = onEnter;
        _active  = true;
        gameObject.SetActive(true);
    }

    /// <summary>게이트를 비활성화합니다.</summary>
    public void Deactivate()
    {
        _active  = false;
        _onEnter = null;
        gameObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_active) return;
        if (!other.CompareTag("Player")) return;

        var cb = _onEnter;
        Deactivate();
        cb?.Invoke();
    }
}
