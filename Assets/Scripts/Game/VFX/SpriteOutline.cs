using UnityEngine;

/// <summary>
/// "Sprites/Outline" 머티리얼의 Outline Color Base를 성(星) 등급에 따라 변경합니다.
/// WeaponPoint.Init() → SetStar(data.star) 호출로 활성화됩니다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteOutline : MonoBehaviour
{
    
    private string colorProperty = "_SolidOutline";

    [Header("성 등급별 색상 (1성~5성)")]
    [SerializeField] private Color[] starColors =
    {
        new Color(1.00f, 1.00f, 1.00f, 1f), // 1성 — 흰색
        new Color(1.00f, 0.85f, 0.00f, 1f), // 2성 — 골드
        new Color(0.40f, 0.80f, 1.00f, 1f), // 3성 — 블루
        new Color(0.80f, 0.40f, 1.00f, 1f), // 4성 — 퍼플
        new Color(1.00f, 0.35f, 0.35f, 1f), // 5성 — 레드
    };

    private SpriteRenderer sr;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    /// <summary>WeaponPoint.Init()에서 호출 — star 값에 맞는 외곽선 색 적용.</summary>
    public void SetStar(int star)
    {
        if (sr == null || sr.material == null) return;

        Color col = starColors[Mathf.Clamp(star - 1, 0, starColors.Length - 1)];
        sr.material.SetColor(colorProperty, col);
    }
}
