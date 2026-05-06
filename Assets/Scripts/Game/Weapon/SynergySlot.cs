using System.Collections.Generic;
using Mewage.DB;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시너지 정보 팝업의 스크롤 슬롯 1개.
/// SynergyInfoPanel이 런타임에 생성하고 Bind()로 데이터를 채웁니다.
///
/// DB_시너지 시트 구성:
///   f_시너지ID, f_이름, f_조건설명, f_등급(A/B/S), f_효과, f_출현시작챕터
/// </summary>
public class SynergySlot : MonoBehaviour
{
    [Header("등급 이미지")]
    public Image gradeImage;

    [Header("시너지 아이콘")]
    public Image iconImage;

    [Header("텍스트")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI conditionText;
    public TextMeshProUGUI effectText;

    [Header("달성 오버레이 (달성 시 비활성화)")]
    public Image overlayImg;

    private string _synergyId;

    // ── 라이프사이클 ──────────────────────────────────────────────────

    private void OnEnable()
    {
        SynergyEffectManager.OnAchievedChanged += RefreshOverlay;
    }

    private void OnDisable()
    {
        SynergyEffectManager.OnAchievedChanged -= RefreshOverlay;
    }

    // ── 바인딩 ────────────────────────────────────────────────────────
    public void Bind(DB_시너지 row)
    {
        if (row == null) return;

        _synergyId = row.f_시너지ID;

        var cfg = SynergyVisualConfig.Shared;

        if (gradeImage != null)
        {
            var gradeSprite = cfg?.GetGradeSprite(row.f_등급);
            if (gradeSprite != null)
            {
                gradeImage.sprite = gradeSprite;
                gradeImage.color  = Color.white;
            }
            else
            {
                gradeImage.color = GradeToColor(row.f_등급);
            }
        }

        if (iconImage != null)
        {
            var icon = cfg?.GetIcon(row.f_시너지ID)
                    ?? Resources.Load<Sprite>($"Synergy/{row.f_시너지ID.ToLower()}");
            iconImage.sprite  = icon;
            iconImage.enabled = icon != null;
        }

        if (nameText)      nameText.text      = row.f_이름;
        if (conditionText) conditionText.text = row.f_조건설명;
        if (effectText)    effectText.text    = row.f_효과;

        RefreshOverlay(SynergyEffectManager.AchievedSynergies);
    }

    // ── 오버레이 갱신 ─────────────────────────────────────────────────

    private void RefreshOverlay(HashSet<string> achieved)
    {
        if (overlayImg == null || string.IsNullOrEmpty(_synergyId)) return;
        bool isAchieved = achieved != null && achieved.Contains(_synergyId);
        overlayImg.gameObject.SetActive(!isAchieved);
    }

    // config에 스프라이트가 없을 때의 색상 폴백
    static Color GradeToColor(string grade) => grade switch
    {
        "S" => new Color(1f,    0.84f, 0f,    1f),
        "A" => new Color(0.56f, 0.24f, 0.9f,  1f),
        "B" => new Color(0.2f,  0.6f,  1f,    1f),
        _   => Color.white,
    };
}
