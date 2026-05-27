using UnityEngine;

/// <summary>
/// 플레이어 자원 흡수 반경을 발밑에 희미한 원으로 시각화합니다.
/// 플레이어 루트(또는 발 위치) 자식 오브젝트에 부착하세요.
/// MagnetRadius 스탯이 변경되면 자동으로 크기가 갱신됩니다.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class MagnetRadiusIndicator : MonoBehaviour
{
    private const int   Segments  = 64;
    private const float LineWidth = 0.07f;
    private static readonly Color RingColor = new Color(0.65f, 0.88f, 1f, 0.22f);

    private LineRenderer _lr;
    private float        _lastRadius = -1f;

    private void Awake()
    {
        _lr = GetComponent<LineRenderer>();
        _lr.loop              = true;
        _lr.positionCount     = Segments;
        _lr.useWorldSpace     = false;
        _lr.startWidth        = LineWidth;
        _lr.endWidth          = LineWidth;
        _lr.startColor        = RingColor;
        _lr.endColor          = RingColor;
        _lr.material          = new Material(Shader.Find("Sprites/Default"));
        _lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lr.receiveShadows    = false;

        // 부모 SpriteRenderer 기준으로 렌더 순서 설정 (없으면 0)
        var parentSr = GetComponentInParent<SpriteRenderer>();
        _lr.sortingLayerID = parentSr != null ? parentSr.sortingLayerID : 0;
        _lr.sortingOrder   = parentSr != null ? parentSr.sortingOrder - 1 : -1;
    }

    private void Start()
    {
        // 첫 프레임에 바로 그림 (플레이어 없으면 기본 반경 1.5 사용)
        float initial = PlayerController.Instance != null
            ? PlayerController.Instance.MagnetRadius
            : 1.5f;
        ApplyRadius(initial);
        _lastRadius = initial;
    }

    private void LateUpdate()
    {
        var player = PlayerController.Instance;
        if (player == null) return;

        float radius = player.MagnetRadius;
        if (Mathf.Abs(radius - _lastRadius) > 0.01f)
        {
            ApplyRadius(radius);
            _lastRadius = radius;
        }
    }

    private void ApplyRadius(float radius)
    {
        float step = 2f * Mathf.PI / Segments;
        for (int i = 0; i < Segments; i++)
        {
            float a = i * step;
            _lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }
    }
}
