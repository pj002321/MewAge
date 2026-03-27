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
        StatPanelUI.Shared?.ClearPreview();
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

        // 스탯 미리보기 전달
        var preview = GetPreviewStat(data);
        if (preview.HasValue)
            StatPanelUI.Shared?.SetPreview(preview.Value.stat, preview.Value.pct);
        else
            StatPanelUI.Shared?.ClearPreview();
    }

    private void OnNextButtonClicked()
    {
        SoundManager.Shared?.Play(SoundKey.UI_Click);
        StatPanelUI.Shared?.ClearPreview();
        ImprovManager.Shared?.OnImprovSelected(pendingData);
        ImprovManager.Shared?.OnNextClicked();
    }

    private void OnRerollButtonClicked()
    {
        selectedOnce = false;
        if (nextButton) nextButton.interactable = false;
        SoundManager.Shared?.Play(SoundKey.UI_Reroll);
        StatPanelUI.Shared?.ClearPreview();
        ImprovManager.Shared?.Reroll();
    }

    /// <summary>증강 데이터에서 미리볼 스탯 타입과 % 변화량을 추출합니다.</summary>
    private (StatType stat, float pct)? GetPreviewStat(DB_증강 data)
    {
        if (data == null) return null;
        float main = data.f_주수치;
        float sub  = data.f_보조수치;
        switch (data.f_증강ID)
        {
            case ImprovType.AUG_ATK:          return (StatType.ATK,               main);
            case ImprovType.AUG_DEF:          return (StatType.DEF,               main);
            case ImprovType.AUG_MAX_HP:       return (StatType.MaxHP,             main);
            case ImprovType.AUG_SPD_MOV:      return (StatType.MoveSpeed,         main);
            case ImprovType.AUG_SPD_ATK:      return (StatType.AtkSpeed,          main);
            case ImprovType.AUG_EVA:          return (StatType.Evasion,           main);
            case ImprovType.AUG_CRI_RATE:     return (StatType.CritChance,        main);
            case ImprovType.AUG_CRI_DMG:      return (StatType.CritDmg,           main);
            case ImprovType.AUG_DOT_DMG:      return (StatType.BleedPower,        main);
            case ImprovType.AUG_SKL:          return (StatType.StructureCooldown, -main);
            case ImprovType.AUG_REWARD_RNG:   return (StatType.MagnetRadius,      main);
            case ImprovType.AUG_SIZE_HP_UP:   return (StatType.MaxHP,             sub);
            case ImprovType.AUG_SIZE_HP_DOWN: return (StatType.MaxHP,             -sub);
            case ImprovType.AUG_HEAL_DEAD:    return (StatType.HealOnKill,        main);
            default:                          return null;
        }
    }
}
