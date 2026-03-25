using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// 뽑기 결과 팝업 — 전체 화면 오버레이.
/// 빈 곳 탭하면 닫히고, DrawButton 으로 다시 뽑기.
/// </summary>
public class GachaResultPopup : MonoBehaviour
{
    [Header("결과 표시 UI")]
    public TextMeshProUGUI gradeText;
    public TextMeshProUGUI itemNameText;
    public Image           itemIcon;

    [Header("뽑기 버튼")]
    public Button          drawButton;
    public TextMeshProUGUI drawCostText;   // "x80" 등

    [Header("장비 스탯 텍스트 (선택)")]
    public TextMeshProUGUI statsText;      // "ATK +30  DEF +10 ..." 등

    [Header("닫기 (빈 곳 탭)")]
    public Button          closeHitArea;   // 전체 화면 투명 버튼

    // 뽑기 버튼 클릭 시 외부 콜백 (event 아닌 delegate — 외부에서 = null 할당 가능)
    public Action OnDrawRequested;

    void Awake()
    {
        closeHitArea?.onClick.AddListener(Close);
        drawButton?.onClick.AddListener(() => OnDrawRequested?.Invoke());
        gameObject.SetActive(false);
    }

    /// <summary>팝업 열기. 초기 상태는 보물상자(아이템 없음).</summary>
    public void OpenForDraw(int gemCost)
    {
        if (gradeText)    gradeText.gameObject.SetActive(false);
        if (itemNameText) itemNameText.gameObject.SetActive(false);
        if (itemIcon)     itemIcon.gameObject.SetActive(false);
        if (drawCostText) drawCostText.text = $"x{gemCost}";
        gameObject.SetActive(true);
    }

    /// <summary>뽑기 결과 표시.</summary>
    public void ShowResult(string grade, string itemName, Sprite icon, EquipmentData equip = null)
    {
        if (gradeText)
        {
            gradeText.text  = grade;
            gradeText.color = EquipmentGacha.GradeColor(grade);
            gradeText.gameObject.SetActive(true);
        }
        if (itemNameText)
        {
            itemNameText.text = itemName;
            itemNameText.gameObject.SetActive(true);
        }
        if (itemIcon)
        {
            itemIcon.enabled = icon != null;
            if (icon != null) itemIcon.sprite = icon;
            itemIcon.gameObject.SetActive(true);
        }
        if (statsText)
        {
            statsText.text = equip != null ? BuildStatsLine(equip) : string.Empty;
            statsText.gameObject.SetActive(equip != null);
        }
    }

    static string BuildStatsLine(EquipmentData e)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (e.atk      != 0) parts.Add($"ATK +{e.atk}");
        if (e.def      != 0) parts.Add($"DEF +{e.def}");
        if (e.hp       != 0) parts.Add($"HP +{e.hp}");
        if (e.evade    != 0) parts.Add($"회피 +{e.evade}%");
        if (e.atkSpeed != 0) parts.Add($"공속 +{e.atkSpeed}%");
        if (e.crit     != 0) parts.Add($"치명 +{e.crit}%");
        return string.Join("  ", parts);
    }

    public void Close()
    {
        OnDrawRequested = null;
        gameObject.SetActive(false);
    }
}
