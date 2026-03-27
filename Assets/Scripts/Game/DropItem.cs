using System.Collections;
using UnityEngine;

/// <summary>
/// 적 사망 시 떨어지는 골드/경험치 아이템.
/// 자석 반경에 진입하면 잠깐 튕겨났다가(Repel) 가속 흡수됩니다(Attract).
/// 보스 드롭 시 InitBurst()로 초기 속도를 줘 사방으로 튀어나갑니다.
/// </summary>
public class DropItem : MonoBehaviour
{
    public enum ItemType { Gold, Exp }

    [Header("아이템 설정")]
    [SerializeField] private ItemType itemType;
    [SerializeField] private int value = 10;

    [Header("자석 이동")]
    [SerializeField] private float magnetRadius = 3f;
    [SerializeField] private float magnetDelay  = 0.4f;

    [Header("흡수 연출")]
    [SerializeField] private float repelDuration  = 0.18f;
    [SerializeField] private float repelForce     = 5f;
    [SerializeField] private float repelScaleUp   = 1.6f;   // Repel 시 최대 스케일 배율
    [SerializeField] private float attractAccel   = 28f;
    [SerializeField] private float attractMaxSpeed = 20f;
    [SerializeField] private float attractScaleMin = 0.15f; // Attract 도착 직전 최소 스케일 배율

    [Header("잔상 (Ghost Sprite)")]
    [SerializeField] private bool  useGhost       = true;
    [SerializeField] private float ghostInterval  = 0.04f;  // 잔상 생성 간격 (초)
    [SerializeField] private float ghostLifetime  = 0.12f;  // 잔상 소멸 시간 (초)
    [SerializeField] private float ghostAlpha     = 0.45f;  // 잔상 초기 알파

    private SpriteRenderer _sr;
    private float          _ghostTimer;

    private enum AbsorbState { Idle, Repel, Attract }
    private AbsorbState _absorbState = AbsorbState.Idle;

    private Vector2 _velocity;
    private float   _repelTimer;
    private float   _attractSpeed;

    private Vector3 orginScale;
    private float   timer;

    // 초기 튕김 속도 (보스 드롭 전용)
    private Vector2 burstVelocity;
    private float   burstFriction = 8f;

    // ── 초기화 ────────────────────────────────────────────────────

    public void Init(ItemType type, int val, float speedOverride = 0f)
    {
        orginScale = transform.localScale;
        itemType   = type;
        value      = val;
    }

    public void InitBurst(ItemType type, int val, Vector2 velocity, float friction = 8f, float speedOverride = 0f)
    {
        Init(type, val, speedOverride);
        burstVelocity = velocity;
        burstFriction = friction;
    }

    private void Start()
    {
        _sr = GetComponentInChildren<SpriteRenderer>();
        StartCoroutine(SpawnPop());
    }

    private IEnumerator SpawnPop()
    {
        transform.localScale = Vector3.zero;
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            transform.localScale = orginScale * Mathf.Lerp(0f, 1.2f, t / 0.2f);
            yield return null;
        }
        t = 0f;
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            transform.localScale = orginScale * Mathf.Lerp(1.2f, 1f, t / 0.1f);
            yield return null;
        }
        transform.localScale = orginScale;
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (burstVelocity.sqrMagnitude > 0.01f)
        {
            transform.position += (Vector3)(burstVelocity * Time.deltaTime);
            burstVelocity       = Vector2.MoveTowards(burstVelocity, Vector2.zero,
                                                      burstFriction * Time.deltaTime);
        }

        if (timer < magnetDelay) return;

        var player = PlayerController.Instance;
        if (player == null) return;

        switch (_absorbState)
        {
            case AbsorbState.Idle:
            {
                float dist            = Vector2.Distance(transform.position, player.Position);
                float effectiveRadius = magnetRadius + (PlayerStats.Shared?.MagnetRadiusBonus ?? 0f);
                if (dist <= effectiveRadius)
                    BeginRepel(player);
                break;
            }

            case AbsorbState.Repel:
            {
                _repelTimer -= Time.deltaTime;

                transform.position += (Vector3)(_velocity * Time.deltaTime);
                _velocity          *= Mathf.Max(0f, 1f - 10f * Time.deltaTime);

                // 스케일: 1 → repelScaleUp → 1 (sin 곡선)
                float progress = 1f - Mathf.Clamp01(_repelTimer / repelDuration);
                float s = 1f + Mathf.Sin(progress * Mathf.PI) * (repelScaleUp - 1f);
                transform.localScale = orginScale * s;

                if (_repelTimer <= 0f)
                {
                    transform.localScale = orginScale;
                    _absorbState  = AbsorbState.Attract;
                    _attractSpeed = repelForce * 0.5f;
                
                }
                break;
            }

            case AbsorbState.Attract:
            {
                _attractSpeed = Mathf.Min(_attractSpeed + attractAccel * Time.deltaTime, attractMaxSpeed);

                Vector3 dir  = (Vector3)player.Position - transform.position;
                float   dist = dir.magnitude;
                if (dist > 0.01f)
                    transform.position += dir.normalized * _attractSpeed * Time.deltaTime;

                // 스케일: 1 → attractScaleMin (거리 1.5 이내부터 축소)
                float scaleFactor = Mathf.Lerp(attractScaleMin, 1f, Mathf.Clamp01(dist / 1.5f));
                transform.localScale = orginScale * scaleFactor;

                // 잔상 스폰
                if (useGhost && _sr != null)
                {
                    _ghostTimer -= Time.deltaTime;
                    if (_ghostTimer <= 0f)
                    {
                        _ghostTimer = ghostInterval;
                        SpawnGhost();
                    }
                }
                break;
            }
        }
    }

    private void BeginRepel(PlayerController player)
    {
        _absorbState = AbsorbState.Repel;
        _repelTimer  = repelDuration;
        _ghostTimer  = 0f;

        Vector2 awayDir = ((Vector2)transform.position - player.Position).normalized;
        if (awayDir.sqrMagnitude < 0.01f) awayDir = Vector2.up;
        awayDir   += new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0f, 0.3f));
        _velocity  = awayDir.normalized * repelForce;
    }

    private void SpawnGhost()
    {
        var ghost = new GameObject("OrbGhost");
        ghost.transform.position   = transform.position;
        ghost.transform.localScale = transform.localScale;

        var ghostSr        = ghost.AddComponent<SpriteRenderer>();
        ghostSr.sprite     = _sr.sprite;
        ghostSr.sortingLayerID = _sr.sortingLayerID;
        ghostSr.sortingOrder   = _sr.sortingOrder - 1;

        Color c = _sr.color;
        ghostSr.color = new Color(c.r, c.g, c.b, ghostAlpha);

        Destroy(ghost, ghostLifetime);
        StartCoroutine(FadeGhost(ghostSr, ghostLifetime));
    }

    private System.Collections.IEnumerator FadeGhost(SpriteRenderer ghostSr, float duration)
    {
        float elapsed = 0f;
        Color start   = ghostSr.color;
        while (elapsed < duration && ghostSr != null)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(start.a, 0f, elapsed / duration);
            ghostSr.color = new Color(start.r, start.g, start.b, a);
            yield return null;
        }
    }

    /// <summary>웨이브 종료 시 호출 — 즉시 플레이어 방향으로 유도 시작.</summary>
    public void ForceAttract()
    {
        burstVelocity = Vector2.zero;
        timer         = magnetDelay;
        _velocity     = Vector2.zero;
        _absorbState  = AbsorbState.Attract;
        _attractSpeed = 6f;
        _ghostTimer   = 0f;
        transform.localScale = orginScale;
    }

    /// <summary>즉시 수집 (패널 열릴 때 강제 흡수용)</summary>
    public void Collect()
    {
        var stats = PlayerStats.Shared;
        if (itemType == ItemType.Gold) stats?.AddGold(value + (stats?.GoldBonus ?? 0));
        else                           stats?.AddExp (value + (stats?.ExpBonus  ?? 0));

        var player = PlayerController.Instance;
        Vector3 spawnPos = player != null
            ? (Vector3)player.Position + new Vector3(0f, 0.3f, 0f)
            : transform.position;

        if (itemType == ItemType.Gold) FXManager.Shared?.SpawnGoldOrbRise(spawnPos);
        else                           FXManager.Shared?.SpawnExpOrbRise(spawnPos);

        Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent<PlayerController>(out _)) return;
        Collect();
    }
}
