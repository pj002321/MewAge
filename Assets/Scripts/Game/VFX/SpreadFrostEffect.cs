using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SHOOT_SPREAD 분사 파티클이 적에게 닿을 때마다 호출.
/// 누적 피해가 쌓일수록 스프라이트를 파랗게 물들이고, 1.5s 후 서서히 복구.
/// </summary>
public class SpreadFrostEffect : MonoBehaviour
{
    private static readonly Color FrostBlue = new Color(0.35f, 0.65f, 1f, 1f);
    private const float FadeDelay = 1.5f;
    private const float FadeRate  = 0.4f;

    private SpriteRenderer[] _srs;
    private Color[]          _origColors;
    private float            _frost;       // 0~1
    private float            _maxDamage;
    private float            _fadeTimer;

    private void Awake()
    {
        _srs = GetComponentsInChildren<SpriteRenderer>(true);
        _origColors = new Color[_srs.Length];
        for (int i = 0; i < _srs.Length; i++)
            _origColors[i] = _srs[i].color;
    }

    private void Update()
    {
        if (_frost <= 0f) return;

        _fadeTimer -= Time.deltaTime;
        if (_fadeTimer > 0f) return;

        _frost = Mathf.Max(0f, _frost - FadeRate * Time.deltaTime);
        RefreshColor();
        if (_frost <= 0f) RestoreColors();
    }

    private void OnDisable() => RestoreColors();

    public static void Apply(GameObject target, float damage, float maxDamage)
    {
        if (target == null) return;
        var fx = target.GetComponent<SpreadFrostEffect>();
        if (fx == null) fx = target.AddComponent<SpreadFrostEffect>();
        fx.AddFrost(damage, maxDamage);
    }

    private void AddFrost(float damage, float maxDamage)
    {
        if (_maxDamage <= 0f) _maxDamage = maxDamage;
        _frost     = Mathf.Clamp01(_frost + damage / _maxDamage);
        _fadeTimer = FadeDelay;
        RefreshColor();
    }

    private void RefreshColor()
    {
        for (int i = 0; i < _srs.Length; i++)
        {
            if (_srs[i] == null) continue;
            _srs[i].color = Color.Lerp(_origColors[i], FrostBlue, _frost);
        }
    }

    private void RestoreColors()
    {
        for (int i = 0; i < _srs.Length; i++)
        {
            if (_srs[i] == null) continue;
            _srs[i].color = _origColors[i];
        }
    }
}
