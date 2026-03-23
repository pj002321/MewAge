using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mewage.DB;

/// <summary>
/// 증강 선택 패널.
/// Inspector 연결: cards (ImprovCard 배열 3개), nextButton, goldText
/// </summary>
public class ImprovPanel : MonoBehaviour
{
    [SerializeField] private ImprovCard[]     cards;
    [SerializeField] private Button           nextButton;
    [SerializeField] private Button           rerollButton;
    [SerializeField] private TextMeshProUGUI  goldText;

    private bool selectedOnce;
    private DB_증강 pendingData;

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

    public void RefreshGold()
    {
        if (goldText && PlayerStats.Shared != null)
            goldText.text = $"{PlayerStats.Shared.Gold}";
    }

    public void Setup(DB_증강[] improvs)
    {
        selectedOnce = false;
        pendingData = null;
        RefreshGold();

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

    private void OnCardClicked(ImprovCard card, DB_증강 data)
    {
        selectedOnce = true;
        pendingData = data;

        card.PlaySelectAnimation();

        // 선택된 카드 외 나머지를 원위치로 리셋하고 어둡게
        foreach (var c in cards)
        {
            if (c.gameObject.activeSelf)
            {
                if (c != card) c.ResetAnimation();
                c.SetDimmed(c != card);
            }
        }

        SoundManager.Shared?.Play(SoundKey.UI_Click);
        if (nextButton) nextButton.interactable = true;
    }

    private void OnNextButtonClicked()
    {
        SoundManager.Shared?.Play(SoundKey.UI_Click);
        ImprovManager.Shared?.OnImprovSelected(pendingData);
        ImprovManager.Shared?.OnNextClicked();
    }

    private void OnRerollButtonClicked()
    {
        selectedOnce = false;
        if (nextButton) nextButton.interactable = false;
        SoundManager.Shared?.Play(SoundKey.UI_Reroll);
        ImprovManager.Shared?.Reroll();
    }
}
