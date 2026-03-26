using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 능력치 팝업 패널.
/// 자식 오브젝트에서 StatFieldUI 컴포넌트를 자동 수집하여 Refresh 호출합니다.
///
/// Inspector 연결:
///   closeButton  — 닫기 버튼
///   activeButton — 패널 열기/닫기 토글 버튼
/// </summary>
public class StatPanelUI : MonoBehaviourSingleton<StatPanelUI>
{
    protected override bool DontDestroy => false;

    [SerializeField] private Button closeButton;
    [SerializeField] private Button activeButton;

    private StatFieldUI[] _fields;

    protected override void Awake()
    {
        base.Awake();
        closeButton ?.onClick.AddListener(Hide);
        activeButton?.onClick.AddListener(Show);

        // timeScale=0 상태에서도 애니메이션이 재생되도록 설정
        foreach (var anim in GetComponentsInChildren<Animator>(includeInactive: true))
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;

        // 자식에서 StatFieldUI 자동 수집
        _fields = GetComponentsInChildren<StatFieldUI>(includeInactive: true);

        gameObject.SetActive(false);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        Refresh();
    }

    public void Hide() => gameObject.SetActive(false);

    public void Refresh()
    {
        if (!gameObject.activeSelf) return;

        var sm = PlayerStatManager.Shared;
        if (sm == null) return;

        foreach (var field in _fields)
            field.Refresh(sm);
    }
}

// ── Unity 구현 가이드 ───────────────────────────────────────────────
// 1. 각 stat_field에 StatFieldUI 컴포넌트 추가 → StatType + valueText 지정
// 2. 이 패널에 StatPanelUI 컴포넌트 추가 → closeButton / activeButton 연결
// 3. UpgradePanel의 statPanelButton → StatPanelUI.Shared.Show() 자동 연결됨
