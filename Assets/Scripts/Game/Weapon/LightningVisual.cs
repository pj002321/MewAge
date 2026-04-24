using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LightningVisual : MonoBehaviour
{
    [SerializeField] private Sprite[] sprites;          // 교대 재생할 스프라이트 2개
    [SerializeField] private int      segments        = 10;
    [SerializeField] private float    jitterAmount    = 0.2f;
    [SerializeField] private float    flickerInterval = 0.05f;
    [SerializeField] private float    lifetime        = 0.15f;

    private LineRenderer _lr;
    private Transform    _source;
    private Transform    _target;
    private float        _flickerTimer;
    private float        _lifetimeTimer;
    private int          _spriteIndex;
    private Material[]   _mats;

    private void Awake()
    {
        _lr = GetComponent<LineRenderer>();
        _lr.useWorldSpace = true;

        if (sprites != null && sprites.Length > 0)
        {
            _mats = new Material[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                var sp  = sprites[i];
                var mat = new Material(Shader.Find("Sprites/Default"));
                mat.mainTexture = sp.texture;

                // 슬라이스 스프라이트의 UV 영역을 tiling/offset으로 설정
                Rect   rect   = sp.textureRect;
                float  tw     = sp.texture.width;
                float  th     = sp.texture.height;
                mat.mainTextureScale  = new Vector2(rect.width / tw, rect.height / th);
                mat.mainTextureOffset = new Vector2(rect.x    / tw, rect.y    / th);

                _mats[i] = mat;
            }
            _lr.sharedMaterial = _mats[0];
        }
    }

    public void Setup(Transform source, Transform target)
    {
        _source        = source;
        _target        = target;
        _lifetimeTimer = lifetime;
        UpdatePoints();
    }

    private void LateUpdate()
    {
        if (_source == null || _target == null || !_target.gameObject.activeInHierarchy)
        {
            Destroy(gameObject);
            return;
        }

        _lifetimeTimer -= Time.deltaTime;
        if (_lifetimeTimer <= 0f) { Destroy(gameObject); return; }

        // 시작점·끝점은 매 프레임 추적 (플레이어 이동·무기 회전 반영)
        _lr.SetPosition(0, _source.position);
        _lr.SetPosition(_lr.positionCount - 1, _target.position);

        // 지글지글 중간 포인트 + 머티리얼 교체는 flickerInterval마다
        _flickerTimer -= Time.deltaTime;
        if (_flickerTimer <= 0f)
        {
            _flickerTimer = flickerInterval;

            if (_mats != null && _mats.Length > 1)
            {
                _spriteIndex = (_spriteIndex + 1) % _mats.Length;
                _lr.sharedMaterial = _mats[_spriteIndex];
            }

            UpdatePoints();
        }
    }

    private void UpdatePoints()
    {
        _lr.positionCount = segments + 2;
        Vector3 start = _source.position;
        Vector3 end   = _target.position;

        _lr.SetPosition(0, start);
        _lr.SetPosition(segments + 1, end);

        for (int i = 1; i <= segments; i++)
        {
            float   t    = i / (float)(segments + 1);
            Vector3 mid  = Vector3.Lerp(start, end, t);
            Vector3 perp = Vector3.Cross((end - start).normalized, Vector3.forward);
            mid += perp * Random.Range(-jitterAmount, jitterAmount);
            _lr.SetPosition(i, mid);
        }
    }

    private void OnDestroy()
    {
        if (_mats == null) return;
        foreach (var mat in _mats)
            if (mat != null) Destroy(mat);
    }
}
