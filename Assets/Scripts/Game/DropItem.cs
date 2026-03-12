using UnityEngine;

/// <summary>
/// 적 사망 시 떨어지는 골드/경험치 아이템.
/// 일정 시간 후 플레이어를 향해 자석처럼 이동하며, 닿으면 수집됩니다.
/// </summary>
public class DropItem : MonoBehaviour
{
    public enum ItemType { Gold, Exp }

    [Header("아이템 설정")]
    [SerializeField] private ItemType itemType;
    [SerializeField] private int value = 10;

    [Header("자석 이동")]
    [SerializeField] private float magnetRadius = 3f;   // 이 거리 안에 들어오면 끌림
    [SerializeField] private float moveSpeed    = 6f;
    [SerializeField] private float magnetDelay  = 0.4f; // 스폰 직후 잠깐 대기

    private Vector3 orginScale;
    private float timer;
    private bool attracted;

    // ItemDropManager에서 스폰 직후 호출
    public void Init(ItemType type, int val)
    {
        orginScale= transform.localScale;
        itemType = type;
        value    = val;
    }

    private void Start()
    {
        // 스폰 시 작은 팝 스케일 연출
        StartCoroutine(SpawnPop());
    }

    private System.Collections.IEnumerator SpawnPop()
    {
        transform.localScale = Vector3.zero;
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(0f, 1.2f, t / 0.2f);
            transform.localScale = orginScale * s;
            yield return null;
        }
        t = 0f;
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(1.2f, 1f, t / 0.1f);
            transform.localScale = orginScale * s;
            yield return null;
        }
        transform.localScale = orginScale;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer < magnetDelay) return;

        var player = PlayerController.Instance;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.Position);
        if (dist <= magnetRadius || attracted)
        {
            attracted = true;
            transform.position = Vector3.MoveTowards(
                transform.position, player.Position, moveSpeed * Time.deltaTime);
        }
    }

    /// <summary>즉시 수집 (패널 열릴 때 강제 흡수용)</summary>
    public void Collect()
    {
        if (itemType == ItemType.Gold) PlayerStats.Shared?.AddGold(value);
        else                           PlayerStats.Shared?.AddExp(value);

        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent<PlayerController>(out _)) return;
        Collect();
    }
}
