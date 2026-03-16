using UnityEngine;
using UnityEngine.UI;
using Mewage.DB;

/// <summary>
/// 증강 선택 패널.
/// Inspector 연결: cards (ImprovCard 배열 3개), nextButton
/// </summary>
public class ImprovPanel : MonoBehaviour
{
    [SerializeField] private ImprovCard[] cards;
    [SerializeField] private Button       nextButton;
    [SerializeField] private Button       rerollButton;

    private bool selectedOnce;

    private void Awake()
    {
        nextButton?.onClick.AddListener(OnNextButtonClicked);
        rerollButton?.onClick.AddListener(OnRerollButtonClicked);
    }

    private void OnEnable()
    {
        foreach (var anim in GetComponentsInChildren<Animator>(true))
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    public void Setup(DB_증강[] improvs)
    {
        selectedOnce = false;

        int visibleCount = 0;
        for (int i = 0; i < cards.Length; i++)
        {
            if (i < improvs.Length && improvs[i] != null)
            {
                cards[i].Setup(improvs[i], OnCardClicked);
                visibleCount++;
            }
            else
            {
                cards[i].SetEmpty();
            }
        }

        // 카드가 없으면 바로 다음으로 진행 가능
        if (nextButton) nextButton.interactable = (visibleCount == 0);
    }

    private void OnCardClicked(DB_증강 data)
    {
        if (selectedOnce) return;
        selectedOnce = true;

        ImprovManager.Shared?.OnImprovSelected(data);
        if (nextButton) nextButton.interactable = true;
    }

    private void OnNextButtonClicked()
    {
        ImprovManager.Shared?.OnNextClicked();
    }

    private void OnRerollButtonClicked()
    {
        selectedOnce = false;
        if (nextButton) nextButton.interactable = false;
        ImprovManager.Shared?.Reroll();
    }
}
