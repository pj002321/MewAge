using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Mewage.DB;

/// <summary>
/// 증강 선택 패널.
/// Inspector 연결: cards (ImprovCard 배열 3개), statPanelHoldButton
/// </summary>
public class ImprovPanel : MonoBehaviour
{
    [SerializeField] private ImprovCard[] cards;

    [SerializeField] private float autoNextDelay = 0.4f; // 카드 선택 후 자동 진행 딜레이

    [Header("스탯 패널 홀드 버튼 (누르는 동안 StatPanel 표시)")]
    [SerializeField] private Button statPanelHoldButton;

    private void Awake()
    {
        if (statPanelHoldButton != null)
        {
            var trigger = statPanelHoldButton.GetComponent<EventTrigger>()
                       ?? statPanelHoldButton.gameObject.AddComponent<EventTrigger>();

            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ => StatPanelUI.Shared?.Show());
            trigger.triggers.Add(down);

            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ => StatPanelUI.Shared?.Hide());
            trigger.triggers.Add(up);
        }
    }

    private void OnEnable()
    {
        StructureInventory.BlockAuto = true;
        foreach (var anim in GetComponentsInChildren<Animator>(true))
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    private void OnDisable()
    {
        StructureInventory.BlockAuto = false;
    }

    public void Setup(DB_증강[] improvs)
    {
        StatPanelUI.Shared?.ClearPreview();

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
    }

    private void OnCardClicked(ImprovCard card, DB_증강 data)
    {
        // 선택된 카드 외 나머지를 어둡게
        foreach (var c in cards)
        {
            if (!c.gameObject.activeSelf) continue;

            if (c == card)
                c.PlaySelectAnimation();
            else
            {
                c.PlayDimAnimation();
                c.SetDimmed(true);
            }
        }

        SoundManager.Shared?.Play(SoundKey.Improv_Select);

        // 스탯 미리보기
        var preview = GetPreviewStat(data);
        if (preview.HasValue)
            StatPanelUI.Shared?.SetPreview(preview.Value.stat, preview.Value.pct);
        else
            StatPanelUI.Shared?.ClearPreview();

        // 딜레이 후 자동 진행
        StopAllCoroutines();
        StartCoroutine(AutoNextRoutine(data));
    }

    private IEnumerator AutoNextRoutine(DB_증강 data)
    {
        yield return new WaitForSecondsRealtime(autoNextDelay);
        StatPanelUI.Shared?.Hide();
        StatPanelUI.Shared?.ClearPreview();
        SoundManager.Shared?.Play(SoundKey.Improv_Confirm);
        ImprovManager.Shared?.OnImprovSelected(data);
        ImprovManager.Shared?.OnNextClicked();
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
