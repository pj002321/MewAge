using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 유물 한 칸 슬롯.
///
/// Inspector 연결:
///   iconImage  — 유물 아이콘 (잠금 시 어둡게 처리)
///   gradeFrame — 등급 색상 테두리 Image
///   button     — 클릭 시 상세 팝업 열기 (없으면 자동 추가)
///
/// 잠금 상태: iconImage 색을 어둡게 처리 → 별도 오버레이 불필요.
/// </summary>
public class RelicSlot : MonoBehaviour
{
    [SerializeField] private Image  iconImage;
    [SerializeField] private Image  gradeFrame;
    [SerializeField] private Button button;

    private static readonly Color LockedTint = new Color(0.25f, 0.25f, 0.25f, 1f);

    private RelicData _data;

    public event Action<RelicData> OnClicked;

    // ── 초기화 ────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (button == null) button = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
        button.onClick.AddListener(() => OnClicked?.Invoke(_data));
    }

    // ── 공개 API ──────────────────────────────────────────────────────────
    public void Refresh(RelicData data)
    {
        _data = data;
        if (data == null) { gameObject.SetActive(false); return; }
        gameObject.SetActive(true);

        var inv    = RelicInventory.Shared;
        bool locked = inv == null || !inv.IsUnlocked(data.relicId);

        // 아이콘: config에서 표시 시점에 로드
        if (iconImage)
        {
            var icon = RelicVisualConfig.Shared?.GetRelicIcon(data.relicId);
            iconImage.sprite = icon;
            iconImage.color  = locked ? LockedTint : Color.white;
        }

        // 등급 테두리 — DB 등급 기준, 잠금 여부 관계없이 항상 표시
        if (gradeFrame)
        {
            var cfg   = RelicVisualConfig.Shared;
            var grade = locked ? RelicVisualConfig.Grade.Common
                               : RelicVisualConfig.ParseGrade(data.grade);
            gradeFrame.sprite = cfg?.GetFrameSprite(grade);
            gradeFrame.color  = Color.white;
        }
    }
}
