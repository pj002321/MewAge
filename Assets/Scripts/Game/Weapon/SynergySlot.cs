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
    public Image gradeImage;       // 등급 배지 이미지 (색상으로 등급 구분)

    [Header("시너지 아이콘")]
    public Image iconImage;        // 시너지 아이콘 (Resources/Synergy/{f_시너지ID.lower()})

    [Header("텍스트")]
    public TextMeshProUGUI nameText;       // 시너지 이름 (f_이름)
    public TextMeshProUGUI conditionText;  // 달성 조건 (f_조건설명)
    public TextMeshProUGUI effectText;     // 효과 설명 (f_효과)

    // ── 바인딩 ────────────────────────────────────────────────────────
    public void Bind(DB_시너지 row)
    {
        if (row == null) return;

        if (gradeImage != null)
            gradeImage.color = GradeToColor(row.f_등급);

        if (iconImage != null)
        {
            var sprite = Resources.Load<Sprite>($"Synergy/{row.f_시너지ID.ToLower()}");
            iconImage.sprite  = sprite;
            iconImage.enabled = sprite != null;
        }

        if (nameText)      nameText.text      = row.f_이름;
        if (conditionText) conditionText.text = row.f_조건설명;
        if (effectText)    effectText.text    = row.f_효과;
    }

    static Color GradeToColor(string grade) => grade switch
    {
        "S" => new Color(1f,    0.84f, 0f,    1f),  // 금
        "A" => new Color(0.56f, 0.24f, 0.9f,  1f),  // 보라
        "B" => new Color(0.2f,  0.6f,  1f,    1f),  // 파랑
        _   => Color.white,
    };
}
