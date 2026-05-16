using UnityEngine;

/// <summary>
/// 게임 내 UI 패널의 표준 베이스 클래스.
///
/// 표준 생명주기:
///   OnEnable  → SubscribeEvents() → Refresh()
///   OnDisable → UnsubscribeEvents()
///
/// 서브클래스 구현 가이드:
///   - Show() / Hide()      : 패널 표시/숨김 (필요 시 오버라이드)
///   - Refresh()            : 현재 데이터 상태를 UI에 반영 (필수 오버라이드)
///   - SubscribeEvents()    : GameEvents.On* += Handler 등록
///   - UnsubscribeEvents()  : GameEvents.On* -= Handler 해제
///
/// 주의: MonoBehaviourSingleton&lt;T&gt;를 상속하는 패널은
///       Awake()를 base.Awake()로 위임하고 이 클래스를 직접 상속하지 않아도 됩니다.
///       이벤트 구독/해제 패턴만 준수하면 됩니다.
/// </summary>
public abstract class UIPanel : MonoBehaviour
{
    // ── 라이프사이클 ─────────────────────────────────────────────
    protected virtual void OnEnable()
    {
        SubscribeEvents();
        Refresh();
    }

    protected virtual void OnDisable()
    {
        UnsubscribeEvents();
    }

    // ── 공개 API ─────────────────────────────────────────────────
    public virtual void Show() => gameObject.SetActive(true);
    public virtual void Hide() => gameObject.SetActive(false);

    /// <summary>현재 데이터 상태를 읽어 UI를 갱신합니다.</summary>
    public virtual void Refresh() { }

    // ── 이벤트 구독 (서브클래스 오버라이드) ──────────────────────
    protected virtual void SubscribeEvents()   { }
    protected virtual void UnsubscribeEvents() { }
}
