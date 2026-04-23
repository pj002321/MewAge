using UnityEngine;

/// <summary>
/// WeaponPoint가 Slash VFX 스폰 직후 AddComponent로 부착.
/// 스폰 방향으로 살짝 밀려나가면서 알파 페이드 아웃.
/// </summary>
public class SlashVfxBehaviour : MonoBehaviour
{
    private float _duration;
    private float _driftDistance;
    private Vector3 _startPos;
    private Vector3 _driftDir;

    private SpriteRenderer[] _renderers;
    private float[] _originalAlphas;

    // 전체 지속시간 중 이 비율 이후부터 페이드 시작 (0.4 = 40% 지점부터)
    private const float FadeStartRatio = 0.4f;

    public void Init(float duration, float driftDistance, Vector2 driftDir)
    {
        _duration      = Mathf.Max(duration, 0.01f);
        _driftDistance = driftDistance;
        _startPos      = transform.position;
        _driftDir      = ((Vector3)driftDir).normalized;

        _renderers     = GetComponentsInChildren<SpriteRenderer>(true);
        _originalAlphas = new float[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _originalAlphas[i] = _renderers[i].color.a;
    }

    private void Update()
    {
        float t = Mathf.Clamp01((Time.time - _spawnTime) / _duration);

        // 드리프트: 스폰 위치에서 driftDir 방향으로 부드럽게 이동
        transform.position = _startPos + _driftDir * (_driftDistance * t);

        // 페이드: FadeStartRatio 이후 구간에서 알파 0으로
        float fadeT = Mathf.Clamp01((t - FadeStartRatio) / (1f - FadeStartRatio));
        for (int i = 0; i < _renderers.Length; i++)
        {
            Color c = _renderers[i].color;
            c.a = _originalAlphas[i] * (1f - fadeT);
            _renderers[i].color = c;
        }
    }

    private float _spawnTime;

    private void Awake()
    {
        _spawnTime = Time.time;
    }
}
