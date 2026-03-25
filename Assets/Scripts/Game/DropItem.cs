using System.Collections;
using UnityEngine;

/// <summary>
/// 적 사망 시 떨어지는 골드/경험치 아이템.
/// 일정 시간 후 플레이어를 향해 자석처럼 이동하며, 닿으면 수집됩니다.
/// 보스 드롭 시 InitBurst()로 초기 속도를 줘 사방으로 튀어나갑니다.
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
    private float   timer;
    private bool    attracted;

    // 초기 튕김 속도 (보스 드롭 전용)
    private Vector2 burstVelocity;
    private float   burstFriction = 8f; // 감속 계수 (초당)

    // ── 초기화 ────────────────────────────────────────────────────

    /// <summary>일반 적 드롭 초기화.</summary>
    public void Init(ItemType type, int val)
    {
        orginScale = transform.localScale;
        itemType   = type;
        value      = val;
    }

    /// <summary>
    /// 보스 드롭 초기화 — 초기 속도를 줘 포물선처럼 퍼지게 합니다.
    /// </summary>
    public void InitBurst(ItemType type, int val, Vector2 velocity, float friction = 8f)
    {
        Init(type, val);
        burstVelocity = velocity;
        burstFriction = friction;
    }

    private void Start()
    {
        StartCoroutine(SpawnPop());
    }

    private IEnumerator SpawnPop()
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

        // 초기 버스트 이동 (자석 판정 전까지 유지)
        if (burstVelocity.sqrMagnitude > 0.01f)
        {
            transform.position  += (Vector3)(burstVelocity * Time.deltaTime);
            burstVelocity       = Vector2.MoveTowards(burstVelocity, Vector2.zero,
                                                      burstFriction * Time.deltaTime);
        }

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

    /// <summary>웨이브 종료 시 호출 — 즉시 플레이어 방향으로 유도 시작.</summary>
    public void ForceAttract()
    {
        attracted     = true;
        timer         = magnetDelay;
        burstVelocity = Vector2.zero; // 버스트 중단
    }

    /// <summary>즉시 수집 (패널 열릴 때 강제 흡수용)</summary>
    public void Collect()
    {
        if (itemType == ItemType.Gold) PlayerStats.Shared?.AddGold(value);
        else                           PlayerStats.Shared?.AddExp(value);

        if (itemType == ItemType.Gold) FXManager.Shared?.SpawnGoldOrbDebris(transform.position);
        else                           FXManager.Shared?.SpawnExpOrbDebris(transform.position);
        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent<PlayerController>(out _)) return;
        Collect();
    }
}
