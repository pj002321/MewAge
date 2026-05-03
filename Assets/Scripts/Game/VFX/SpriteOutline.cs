using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteOutline : MonoBehaviour
{
    private static readonly string ColorProperty = "_SolidOutline";

    [Header("성 등급별 색상 (1~5성)")]
    [SerializeField] private Color[] starColors =
    {
        new Color(1.00f, 1.00f, 1.00f, 1f),
        new Color(1.00f, 0.85f, 0.00f, 1f),
        new Color(0.40f, 0.80f, 1.00f, 1f),
        new Color(0.80f, 0.40f, 1.00f, 1f),
        new Color(1.00f, 0.35f, 0.35f, 1f),
    };

    private SpriteRenderer _sr;
    private void Awake() => _sr = GetComponent<SpriteRenderer>();

    public void SetStar(int star)
    {
        if (_sr == null || _sr.material == null) return;
        _sr.material.SetColor(ColorProperty,
            starColors[Mathf.Clamp(star - 1, 0, starColors.Length - 1)]);
    }

    public void SetGrade(WeaponGrade grade)
    {
        if (_sr == null || _sr.material == null || grade == WeaponGrade.Common) return;
        _sr.material.SetColor(ColorProperty, WeaponGradeColors.Get(grade));
    }
}
