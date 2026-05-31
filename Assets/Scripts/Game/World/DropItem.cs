using System.Collections;
using UnityEngine;

/// <summary>
/// 적 사망 시 떨어지는 골드/경험치 아이템.
/// 자석 반경에 진입하면 잠깐 튕겨났다가(Repel) 가속 흡수됩니다(Attract).
/// 보스/엘리트 드롭: InitParabolic()으로 포물선 궤적 → 착지 후 플로팅 → 자동 수집.
/// 일반 드롭: Init()으로 제자리 스폰, 플로팅 효과 적용.
/// 웨이브 종료: ThrowUp()으로 짧은 파편 burst 후 강제 수집.
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
    [SerializeField] private float repelScaleUp   = 1.6f;
    [SerializeField] private float attractAccel   = 28f;
    [SerializeField] private float attractMaxSpeed = 20f;
    [SerializeField] private float attractScaleMin = 0.15f;

    [Header("플로팅 (둥둥)")]
    [SerializeField] private float floatAmplitude = 0.12f;
    [SerializeField] private float floatFrequency = 1.2f;

    [Header("잔상 (Ghost Sprite)")]
    [SerializeField] private bool  useGhost      = true;
    [SerializeField] private float ghostInterval = 0.04f;
    [SerializeField] private float ghostLifetime = 0.12f;
    [SerializeField] private float ghostAlpha    = 0.45f;

    private SpriteRenderer _sr;
    private float          _ghostTimer;

    private enum AbsorbState { Idle, Repel, Attract }
    private AbsorbState _absorbState = AbsorbState.Idle;

    private Vector2 _velocity;
    private float   _repelTimer;
    private float   _attractSpeed;

    private Vector3 orginScale;
    private float   timer;

    // 버스트 공통
    private Vector2 burstVelocity;
    private float   burstFriction = 8f;

    // 포물선 전용
    private bool  _useGravity;
    private float _gravityAccel;
    private bool  _blockAttract;
    private float _spawnY;

    // 플로팅
    private bool  _floating;
    private float _floatBaseY;
    private float _floatTime;

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

    /// <summary>보스/엘리트 드롭 — 포물선 궤도로 날아가 착지 후 플로팅 → 자동 수집됩니다.</summary>
    public void InitParabolic(ItemType type, int val, Vector2 velocity, float gravity = 16f)
    {
        Init(type, val);
        burstVelocity = velocity;
        burstFriction = 0f;
        _useGravity   = true;
        _gravityAccel = gravity;
        _blockAttract = true;
        _spawnY       = transform.position.y;
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

        // ── 이동 처리 ─────────────────────────────────────────────
        if (_useGravity)
        {
            // 포물선: Y축 중력, X축 공기 저항
            burstVelocity.y -= _gravityAccel * Time.deltaTime;
            burstVelocity.x  = Mathf.MoveTowards(burstVelocity.x, 0f, 4f * Time.deltaTime);
            transform.position += (Vector3)(burstVelocity * Time.deltaTime);

            // 하강 중 스폰 높이 이하면 착지
            if (_blockAttract && burstVelocity.y <= 0f && transform.position.y <= _spawnY + 0.1f)
            {
                burstVelocity = Vector2.zero;
                _useGravity   = false;
                _blockAttract = false;
                StartFloating();
            }
        }
        else if (burstVelocity.sqrMagnitude > 0.01f)
        {
            transform.position += (Vector3)(burstVelocity * Time.deltaTime);
            burstVelocity       = Vector2.MoveTowards(burstVelocity, Vector2.zero,
                                                      burstFriction * Time.deltaTime);
            if (!_floating && burstVelocity.sqrMagnitude <= 0.01f)
                StartFloating();
        }
        else if (!_floating && _absorbState == AbsorbState.Idle)
        {
            // 버스트 없이 스폰된 경우 (일반 드롭)
            StartFloating();
        }

        if (timer < magnetDelay) return;

        var player = PlayerController.Instance;
        if (player == null) return;

        switch (_absorbState)
        {
            case AbsorbState.Idle:
            {
                // 플로팅 bob
                if (_floating)
                {
                    _floatTime += Time.deltaTime;
                    float y = _floatBaseY + Mathf.Sin(_floatTime * floatFrequency * Mathf.PI * 2f) * floatAmplitude;
                    transform.position = new Vector3(transform.position.x, y, transform.position.z);
                }

                if (!_blockAttract)
                {
                    float dist            = Vector2.Distance(transform.position, player.Position);
                    float effectiveRadius = magnetRadius + (PlayerStats.Shared?.MagnetRadiusBonus ?? 0f);
                    if (dist <= effectiveRadius)
                        BeginRepel(player);
                }
                break;
            }

            case AbsorbState.Repel:
            {
                _repelTimer -= Time.deltaTime;

                transform.position += (Vector3)(_velocity * Time.deltaTime);
                _velocity          *= Mathf.Max(0f, 1f - 10f * Time.deltaTime);

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

                float scaleFactor = Mathf.Lerp(attractScaleMin, 1f, Mathf.Clamp01(dist / 1.5f));
                transform.localScale = orginScale * scaleFactor;

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

    private void StartFloating()
    {
        _floating   = true;
        _floatBaseY = transform.position.y;
        _floatTime  = Random.Range(0f, 1f);
        StartCoroutine(AutoAttractRoutine());
    }

    private IEnumerator AutoAttractRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        if (gameObject != null && gameObject.activeInHierarchy && _absorbState == AbsorbState.Idle)
            ForceAttract();
    }

    private void BeginRepel(PlayerController player)
    {
        _floating    = false;
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

        var ghostSr            = ghost.AddComponent<SpriteRenderer>();
        ghostSr.sprite         = _sr.sprite;
        ghostSr.sortingLayerID = _sr.sortingLayerID;
        ghostSr.sortingOrder   = _sr.sortingOrder - 1;

        Color c = _sr.color;
        ghostSr.color = new Color(c.r, c.g, c.b, ghostAlpha);

        Destroy(ghost, ghostLifetime);
        StartCoroutine(FadeGhost(ghostSr, ghostLifetime));
    }

    private IEnumerator FadeGhost(SpriteRenderer ghostSr, float duration)
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

    /// <summary>웨이브 종료 시 호출 — 제자리에서 파편 FX를 터뜨리고 즉시 파괴됩니다.</summary>
    public void ThrowUp()
    {
        StopAllCoroutines();
        if (itemType == ItemType.Gold) FXManager.Shared?.SpawnGoldOrbDebris(transform.position);
        else                           FXManager.Shared?.SpawnExpOrbDebris(transform.position);
        Destroy(gameObject);
    }

    private IEnumerator AutoCollectDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (gameObject != null && gameObject.activeInHierarchy) Collect();
    }

    /// <summary>즉시 플레이어 방향으로 유도 시작.</summary>
    public void ForceAttract()
    {
        burstVelocity = Vector2.zero;
        _useGravity   = false;
        _blockAttract = false;
        _floating     = false;
        timer         = magnetDelay;
        _velocity     = Vector2.zero;
        _absorbState  = AbsorbState.Attract;
        _attractSpeed = 6f;
        _ghostTimer   = 0f;
        transform.localScale = orginScale;
    }

    /// <summary>즉시 수집</summary>
    public void Collect()
    {
        var stats = PlayerStats.Shared;
        if (itemType == ItemType.Gold) stats?.AddGold(value);
        else                           stats?.AddExp (value);

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
