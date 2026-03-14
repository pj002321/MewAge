using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 증강 선택 패널.
/// - 3장의 ImprovCard 중 하나를 선택하면 즉시 효과가 적용됩니다.
/// - 선택 후 "다음" 버튼을 누르면 UpgradePanel로 넘어갑니다.
///
/// Inspector 연결:
///   cards       — ImprovCard 컴포넌트 배열 (3개)
///   nextButton  — "다음" 버튼
/// </summary>
public class ImprovPanel : MonoBehaviour
{
    [SerializeField] private ImprovCard[] cards;
    [SerializeField] private Button       nextButton;

    private bool selectedOnce;

    // ────────────────────────────────────────────────────────────────
    // 생명주기
    // ────────────────────────────────────────────────────────────────
    private void Awake()
    {
        nextButton?.onClick.AddListener(OnNextButtonClicked);
    }

    private void OnEnable()
    {
        // Time.timeScale = 0 상태에서도 애니메이션이 동작하도록
        foreach (var anim in GetComponentsInChildren<Animator>(true))
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    // ────────────────────────────────────────────────────────────────
    // 외부 진입점 (ImprovManager가 호출)
    // ────────────────────────────────────────────────────────────────
    public void Setup(ImprovData[] improvs)
    {
        selectedOnce = false;

        // 선택 전까지 "다음" 버튼 비활성
        if (nextButton) nextButton.interactable = false;

        for (int i = 0; i < cards.Length; i++)
        {
            if (i < improvs.Length && improvs[i] != null)
                cards[i].Setup(improvs[i], OnCardClicked);
            else
                cards[i].SetEmpty();
        }
    }

    // ────────────────────────────────────────────────────────────────
    // 내부 콜백
    // ────────────────────────────────────────────────────────────────
    private void OnCardClicked(ImprovData data)
    {
        if (selectedOnce) return;
        selectedOnce = true;

        // 효과 즉시 적용
        ImprovManager.Shared?.OnImprovSelected(data);

        // 선택된 카드 하이라이트, 나머지 비활성
        foreach (var card in cards)
            card.SetInteractable(card.Data == data);

        // "다음" 버튼 활성화
        if (nextButton) nextButton.interactable = true;
    }

    private void OnNextButtonClicked()
    {
        ImprovManager.Shared?.OnNextClicked();
    }
}
