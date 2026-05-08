using UnityEngine;

/// <summary>
/// 전설 부메랑 스킬: 타격 시 일정 확률로 4방향 반투명 대형 복제본 발사.
/// 복제본은 WeaponPoint와 동일한 projectilePrefab을 사용하거나,
/// 없으면 현재 무기 스프라이트를 그대로 복사해 날아가는 연출을 씁니다.
/// </summary>
public class LegendaryBoomerangSkill : WeaponLegendarySkill
{
    [SerializeField] private float chance        = 0.3f;
    [SerializeField] private float cloneSpeed    = 10f;
    [SerializeField] private float cloneLifetime = 1.8f;
    [SerializeField] private float cloneScale    = 2f;    // 원본 대비 크기 배율
    [SerializeField] private float cloneAlpha    = 0.45f;
    [SerializeField] private GameObject cloneProjectilePrefab; // null이면 스프라이트 복사

    private static readonly Vector2[] _cardinalDirs =
    {
        Vector2.up, Vector2.right, Vector2.down, Vector2.left
    };

    public override void OnHit(Vector2 hitDir, Collider2D[] hitBuffer, int colCount)
    {
        if (Random.value > chance) return;

        Vector3 origin = transform.position;
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();

        foreach (var dir in _cardinalDirs)
            SpawnClone(origin, dir, sr);
    }

    private void SpawnClone(Vector3 origin, Vector2 dir, SpriteRenderer sr)
    {
        if (cloneProjectilePrefab != null)
        {
            var go   = Instantiate(cloneProjectilePrefab, origin, Quaternion.identity);
            var proj = go.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.Init(WP != null ? WP.AttackDamage * 0.6f : 1f, dir, cloneSpeed,
                    lifetime: cloneLifetime);
                go.transform.localScale *= cloneScale;
                SetAlpha(go, cloneAlpha);
            }
            else
            {
                Destroy(go, cloneLifetime);
            }
            return;
        }

        // 프리팹 없으면 스프라이트 고스트 투사
        if (sr == null || sr.sprite == null) return;
        var ghost = new GameObject("BoomerangClone");
        ghost.transform.position   = origin;
        ghost.transform.localScale = transform.lossyScale * cloneScale;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        ghost.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        var ghostSr       = ghost.AddComponent<SpriteRenderer>();
        ghostSr.sprite         = sr.sprite;
        ghostSr.sortingLayerID = sr.sortingLayerID;
        ghostSr.sortingOrder   = sr.sortingOrder;
        ghostSr.color          = new Color(1f, 1f, 1f, cloneAlpha);

        var mover = ghost.AddComponent<BoomerangCloneMover>();
        mover.Init(dir, cloneSpeed, cloneLifetime);
    }

    private void SetAlpha(GameObject go, float alpha)
    {
        foreach (var r in go.GetComponentsInChildren<SpriteRenderer>())
        {
            Color c = r.color;
            c.a = alpha;
            r.color = c;
        }
    }
}

/// <summary>단순 직선 이동 후 자동 소멸하는 복제본 이동 컴포넌트.</summary>
public class BoomerangCloneMover : MonoBehaviour
{
    private Vector2 _dir;
    private float   _speed;
    private float   _lifetime;

    public void Init(Vector2 dir, float speed, float lifetime)
    {
        _dir      = dir;
        _speed    = speed;
        _lifetime = lifetime;
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        transform.position += (Vector3)(_dir * _speed * Time.deltaTime);
    }
}
