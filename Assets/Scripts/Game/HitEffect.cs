using UnityEngine;

/// <summary>
/// 적이 타격당할 때 재생되는 원형 버스트 이펙트.
/// SpriteRenderer가 있는 Prefab에 부착하세요. (Sprites/Circle 추천)
/// </summary>
public class HitEffect : MonoBehaviour
{
    [SerializeField] private float duration  = 0.25f;
    [SerializeField] private float maxScale  = 1.2f;
    [SerializeField] private Color hitColor  = new(1f, 0.9f, 0.2f, 1f); // 노란 섬광

    private SpriteRenderer sr;
    private float timer;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = hitColor;
        timer = duration;
        transform.localScale = Vector3.zero;
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        float t = 1f - (timer / duration); // 0 → 1

        // 빠르게 커졌다가 페이드아웃
        float scale = Mathf.Sin(t * Mathf.PI) * maxScale;
        transform.localScale = Vector3.one * scale;

        if (sr != null)
        {
            float alpha = 1f - t;
            sr.color = new Color(hitColor.r, hitColor.g, hitColor.b, alpha);
        }

        if (timer <= 0f)
            Destroy(gameObject);
    }
}
