using System.Collections;
using UnityEngine;

/// <summary>
/// 엘리트 몬스터 프리팹에 EnemyController와 함께 부착합니다.
/// 죽으면 static OnEliteDied 이벤트를 발생시킵니다.
/// GameManager가 이 이벤트를 구독해 즉시 웨이브를 클리어합니다.
/// </summary>
public class EliteMarker : MonoBehaviour
{
    public static event System.Action OnEliteDied;

    // 기본 아웃라인 색상 (HDR: 블룸 트리거)
    private static readonly Color OutlineBaseColor = new Color(1f, 0.05f, 0.05f, 1f);
    private const float OutlineThickness   = 3f;
    private const float PulseMinIntensity  = 1.5f;
    private const float PulseMaxIntensity  = 4.5f;
    private const float PulseSpeed        = 1.8f; // cycles/sec

    private EnemyController _ec;
    private Material        _outlineMat;
    private Coroutine       _pulseCoroutine;

    void Awake()
    {
        _ec = GetComponent<EnemyController>();
        if (_ec != null) _ec.KnockbackImmune = true;
        ApplyOutline();
    }

    void OnEnable()
    {
        if (_ec != null) _ec.OnDeath += HandleDeath;
        if (_outlineMat != null)
            _pulseCoroutine = StartCoroutine(PulseRoutine());
    }

    void OnDisable()
    {
        if (_ec != null) _ec.OnDeath -= HandleDeath;
        if (_pulseCoroutine != null) { StopCoroutine(_pulseCoroutine); _pulseCoroutine = null; }
    }

    private void OnDestroy()
    {
        if (_outlineMat != null) Destroy(_outlineMat);
    }

    private void HandleDeath() => OnEliteDied?.Invoke();

    // ── 아웃라인 초기화 ───────────────────────────────────────────────

    private void ApplyOutline()
    {
        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;

        var shader = Shader.Find("Sprites/Outline");
        if (shader == null) return;

        _outlineMat = new Material(shader);
        _outlineMat.SetColor("_SolidOutline", OutlineBaseColor * PulseMinIntensity);
        _outlineMat.SetFloat("_Thickness",       OutlineThickness);
        _outlineMat.SetFloat("_OutlineEnabled",  1f);
        _outlineMat.SetFloat("_OutlineMode",     0f); // Solid
        _outlineMat.SetFloat("_OutlineShape",    0f); // Contour
        _outlineMat.SetFloat("_OutlinePosition", 2f); // Outside

        sr.sharedMaterial = _outlineMat;
    }

    // ── 블룸 맥동 ────────────────────────────────────────────────────

    private IEnumerator PulseRoutine()
    {
        while (true)
        {
            // 0~1 사인파 → PulseMin~PulseMax 강도
            float t         = (Mathf.Sin(Time.time * PulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            float intensity = Mathf.Lerp(PulseMinIntensity, PulseMaxIntensity, t);
            _outlineMat.SetColor("_SolidOutline", OutlineBaseColor * intensity);
            yield return null;
        }
    }
}
