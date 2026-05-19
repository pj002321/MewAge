using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class SpriteOutline : MonoBehaviour
{
    private static readonly string ColorProperty = "_SolidOutline";

    private static readonly Color[] GradeColors =
    {
        new Color(1.00f, 1.00f, 1.00f),  // Common    — 흰색
        new Color(0.30f, 0.60f, 1.00f),  // Rare      — 파랑
        new Color(0.70f, 0.20f, 1.00f),  // Epic      — 보라
        new Color(1.00f, 0.90f, 0.10f),  // Legendary — 노랑
        new Color(1.00f, 0.20f, 0.20f),  // Mythic    — 빨강
    };

    private SpriteRenderer _sr;
    private void Awake() => _sr = GetComponent<SpriteRenderer>();

    // star 1=커먼, 2=레어, 3=에픽, 4=전설, 5=신화
    public void SetStar(int star)
    {
        if (_sr == null || _sr.material == null) return;
        _sr.material.SetColor(ColorProperty, GradeColors[Mathf.Clamp(star - 1, 0, GradeColors.Length - 1)]);
    }

    public void SetGrade(WeaponGrade grade)
    {
        if (_sr == null || _sr.material == null) return;
        _sr.material.SetColor(ColorProperty, GradeColors[Mathf.Clamp((int)grade, 0, GradeColors.Length - 1)]);
    }
}
