using System.Collections;
using TMPro;
using UnityEngine;
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

    [SerializeField] private Button statPanelHoldButton;

    private bool _inputLocked;

    private void Awake()
    {
        if (statPanelHoldButton == null) return;

        statPanelHoldButton.onClick.AddListener(() =>
        {
            var panel = StatPanelUI.Shared;
            if (panel == null) return;
            if (panel.gameObject.activeSelf) panel.Hide();
            else                              panel.Show();
        });
    }

    private void OnEnable()
    {
        foreach (var anim in GetComponentsInChildren<Animator>(true))
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    public void Setup(DB_증강[] improvs)
    {
        _inputLocked = false;
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
        if (_inputLocked) return;
        _inputLocked = true;

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
        var stat = AugmentHelper.ToStatType(data.f_증강ID);
        return stat.HasValue ? (stat.Value, data.f_주수치) : ((StatType, float)?)null;
    }
}
