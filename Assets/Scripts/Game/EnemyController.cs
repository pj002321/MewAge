using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 웨이브 구간별 스탯 오버라이드.
/// fromWave 이하 중 가장 큰 값이 적용됩니다. -1은 기본값 유지.
/// </summary>
[System.Serializable]
public class WaveStatBreakpoint
{
    [Tooltip("이 웨이브 이상부터 적용")]
    public int   fromWave     = 10;
    [Tooltip("-1 = 기본값 유지")]
    public float moveSpeed    = -1f;
    [Tooltip("-1 = 기본값 유지")]
    public float maxHP        = -1f;
    [Tooltip("-1 = 기본값 유지")]
    public float contactDamage = -1f;
    [Tooltip("-1 = 기본값 유지")]
    public int   goldDrop     = -1;
    [Tooltip("-1 = 기본값 유지")]
    public int   expDrop      = -1;
}

/// <summary>
/// 적의 핵심 컴포넌트 — 스탯, HP, 이동 헬퍼, 피격/사망을 담당합니다.
/// 공격 / 이동 세부 로직은 같은 GameObject에 부착된 <see cref="EnemyBehaviourBase"/>
/// 서브클래스(MeleeBehaviour, RangedBehaviour 등)에서 처리합니다.
/// </summary>
public class EnemyController : MonoBehaviour
{
    // ── 인스펙터 ────────────────────────────────────────────────
    [Header("기본 스탯")]
    [SerializeField] private float moveSpeed      = 2f;
    [SerializeField] private float maxHP          = 30f;
    [SerializeField] private float contactDamage  = 10f;
    [SerializeField] private int   scoreValue     = 10;
    [SerializeField] private float damageCooldown = 1f;
    [Tooltip("OnTrigger 접촉 데미지 최대 거리 — 콜라이더가 스프라이트보다 클 때 보정.")]
    [SerializeField] private float contactMaxDist = 0.8f;

    [Header("드롭")]
    [SerializeField] private int goldDrop = 5;
    [SerializeField] private int expDrop  = 10;

    [Header("분리 (간격 유지)")]
    [Tooltip("주변 적과 이 거리 이내면 밀어냅니다. 0 = 비활성")]
    [SerializeField] private float separationRadius   = 1.2f;
    [SerializeField] private float separationStrength = 8f;

    [Header("웨이브 구간별 스탯 (fromWave 오름차순 권장)")]
    [SerializeField] private List<WaveStatBreakpoint> waveBreakpoints = new();

    // ── 공개 프로퍼티 (EnemyBehaviourBase에서 사용) ────────────
    public float      MoveSpeed       => moveSpeed;
    public float      ContactDamage   => contactDamage;
    public Transform  PlayerTransform => playerTransform;
    public Rigidbody2D Rb             => rb;
    public Animator   EnemyAnimator   => animator;
    public float      HPRatio         => maxHP > 0f ? currentHP / maxHP : 0f;

    // ── 런타임 상태 ─────────────────────────────────────────────
    private float          currentHP;
    private float          damageTimer;
    private Transform      playerTransform;
    private Rigidbody2D    rb;
    private SpriteRenderer sr;
    private HitFlash       hitFlash;
    private Animator       animator;
    private EnemyBehaviourBase behaviour;

    private bool      isStunned;
    private Vector2   _pendingMove;
    private Coroutine knockbackCoroutine;
    private float     baseSpeed; // 넉백/스턴 전 원본 속도
    private Vector2   _deathDir;  // 사망 시 날아갈 방향

    // 사망 흰색 실루엣 재질 (SpriteSilhouette 쉐이더) — 씬 전체 공유
    private static Material _deathFlashMat;
    private static Material GetDeathFlashMat()
    {
        if (_deathFlashMat != null) return _deathFlashMat;
        var shader = Shader.Find("Custom/SpriteSilhouette");
        if (shader == null) return null;
        _deathFlashMat = new Material(shader);
        _deathFlashMat.SetColor("_Color", Color.white);
        return _deathFlashMat;
    }

    public event System.Action OnDeath;

    // ── 초기화 ──────────────────────────────────────────────────
    private void Awake()
    {
        _allEnemies.Add(this);
        currentHP = maxHP;
        baseSpeed = moveSpeed;
        rb        = GetComponent<Rigidbody2D>();
        sr        = GetComponentInChildren<SpriteRenderer>(true);
        hitFlash  = GetComponentInChildren<HitFlash>(true);
        animator  = GetComponentInChildren<Animator>();
        behaviour = GetComponent<EnemyBehaviourBase>();

        if (rb != null)
        {
            rb.bodyType      = RigidbodyType2D.Kinematic;
            rb.sleepMode     = RigidbodySleepMode2D.NeverSleep;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.constraints   = RigidbodyConstraints2D.FreezeRotation;
        }

        behaviour?.Init(this);
    }

    private void Start()
    {
        playerTransform = PlayerController.Instance?.transform;
    }

    // ── 업데이트 ────────────────────────────────────────────────
    private void Update()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;
        if (isStunned) return;
        behaviour?.OnAttackUpdate();
    }

    private void FixedUpdate()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;
        if (isStunned) return;

        if (playerTransform == null)
        {
            playerTransform = PlayerController.Instance?.transform;
            return;
        }

        behaviour?.OnMoveFixedUpdate();
        ApplySeparation();

        if (_pendingMove != Vector2.zero && rb != null)
            rb.MovePosition(rb.position + _pendingMove);
        _pendingMove = Vector2.zero;
    }

    // ── 분리: Physics 쿼리 없이 전체 목록에서 직접 계산 ─────────
    private static readonly List<EnemyController> _allEnemies = new List<EnemyController>();

    private void ApplySeparation()
    {
        if (separationRadius <= 0f || rb == null) return;

        float   r2   = separationRadius * separationRadius;
        Vector2 push = Vector2.zero;

        foreach (var other in _allEnemies)
        {
            if (other == this || other.rb == null) continue;
            Vector2 diff  = rb.position - other.rb.position;
            float   sqDist = diff.sqrMagnitude;
            if (sqDist > 0.000001f && sqDist < r2)
            {
                float dist = Mathf.Sqrt(sqDist);
                push += (diff / dist) * (1f - dist / separationRadius);
            }
        }

        if (push != Vector2.zero)
            _pendingMove += push * separationStrength * Time.fixedDeltaTime;
    }

    private void OnDestroy()
    {
        _allEnemies.Remove(this);
    }

    private void LateUpdate()
    {
        damageTimer -= Time.deltaTime;
    }

    // ── 이동 헬퍼 (EnemyBehaviourBase에서 호출) ─────────────────
    /// <summary>플레이어 방향으로 speed만큼 이동합니다.</summary>
    public void MoveTowardPlayer(float speed)
    {
        if (playerTransform == null || rb == null) return;
        Vector2 toPlayer = (Vector2)playerTransform.position - rb.position;
        MoveInDirection(toPlayer.normalized, speed);
    }

    /// <summary>지정 방향으로 speed만큼 이동하고 스프라이트를 뒤집습니다.</summary>
    public void MoveInDirection(Vector2 dir, float speed)
    {
        if (rb == null) return;
        _pendingMove += dir.normalized * speed * Time.fixedDeltaTime;
        FlipToward(dir);
    }

    /// <summary>방향 벡터에 따라 스프라이트를 좌우 반전합니다.</summary>
    public void FlipToward(Vector2 dir)
    {
        if (sr == null) return;
        if (dir.x > 0.01f)       sr.flipX = false;
        else if (dir.x < -0.01f) sr.flipX = true;
    }

    // ── 충돌 접촉 피해 ──────────────────────────────────────────
    private void OnTriggerStay2D(Collider2D other)
    {
        if (damageTimer > 0f) return;
        var p = other.GetComponentInParent<PlayerController>();
        if (p == null) return;
        if (Vector2.Distance(transform.position, p.transform.position) > contactMaxDist) return;
        p.TakeDamage(contactDamage);
        damageTimer = damageCooldown;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var p = other.GetComponentInParent<PlayerController>();
        if (p == null) return;
        if (Vector2.Distance(transform.position, p.transform.position) > contactMaxDist) return;
        p.TakeDamage(contactDamage);
        damageTimer = damageCooldown;
    }

    // ── 상태 효과 ────────────────────────────────────────────────
    /// <summary>지정 시간 동안 이동 불가 상태를 부여합니다.</summary>
    public void Paralyze(float seconds)
    {
        StopCoroutine(nameof(ParalyzeRoutine));
        StartCoroutine(nameof(ParalyzeRoutine), seconds);
    }

    private IEnumerator ParalyzeRoutine(float seconds)
    {
        moveSpeed = 0f;
        yield return new WaitForSeconds(seconds);
        moveSpeed = baseSpeed;
    }

    /// <summary>지정 시간 동안 이동·공격을 모두 멈추고 스프라이트를 노란색으로 변경합니다.</summary>
    public void Stun(float seconds)
    {
        StopCoroutine(nameof(StunRoutine));
        StartCoroutine(nameof(StunRoutine), seconds);
    }

    private IEnumerator StunRoutine(float seconds)
    {
        isStunned = true;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        Color originalColor = sr != null ? sr.color : Color.white;
        if (sr != null) sr.color = new Color(1f, 0.92f, 0f, 1f); // 노란색

        yield return new WaitForSeconds(seconds);

        isStunned = false;
        if (sr != null) sr.color = originalColor;
    }

    /// <summary>지정 방향으로 순간적으로 밀어냅니다.</summary>
    public void Knockback(Vector2 dir, float force)
    {
        if (knockbackCoroutine != null) StopCoroutine(knockbackCoroutine);
        knockbackCoroutine = StartCoroutine(KnockbackRoutine(dir.normalized, force));
    }

    private IEnumerator KnockbackRoutine(Vector2 dir, float force)
    {
        moveSpeed = 0f;

        float elapsed  = 0f;
        float duration = 0.3f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            rb?.MovePosition(rb.position + dir * force * (1f - t) * Time.fixedDeltaTime);
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        moveSpeed = baseSpeed;
        knockbackCoroutine = null;
    }

    // ── 피격 / 사망 ──────────────────────────────────────────────
    public void TakeDamage(float damage, bool isCritical = false, Vector2 knockbackDir = default, float knockbackForce = 0f)
    {
        currentHP -= damage;

        SoundManager.Shared?.Play(SoundKey.Enemy_Hit);
        // 스프라이트 상단 기준으로 스폰 (몸집이 커도 텍스트가 가려지지 않음)
        Vector3 textPos = sr != null
            ? new Vector3(transform.position.x, sr.bounds.max.y, transform.position.z)
            : transform.position;
        FXManager.Shared?.SpawnDamageText(textPos, damage, isCritical);
        FXManager.Shared?.SpawnHitEffect(transform.position);
        hitFlash?.Flash();

        if (knockbackDir != Vector2.zero && knockbackForce > 0f)
            Knockback(knockbackDir, knockbackForce);

        if (currentHP <= 0f)
        {
            _deathDir = knockbackDir.sqrMagnitude > 0.001f ? knockbackDir.normalized : Vector2.up;
            OnDeath?.Invoke();
            ItemDropManager.Shared?.SpawnDrops(transform.position, goldDrop, expDrop);
            StartCoroutine(DieRoutine());
        }
    }

    private IEnumerator DieRoutine()
    {
        // 이동·공격·충돌 데미지 비활성화
        enabled = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // 사망 위치에 파편 스폰 (FXManager 없으면 인라인 실행)
        var debrisPos = transform.position;
        if (FXManager.Shared != null)
            FXManager.Shared.SpawnDeathDebris(debrisPos);
        else
            SpawnDebrisFallback(debrisPos);

        // SpriteSilhouette 쉐이더로 재질 교체 → 흰색 실루엣
        var flashMat = GetDeathFlashMat();
        if (sr != null && flashMat != null)
            sr.material = flashMat;

        float duration  = 0.4f;
        float elapsed   = 0f;
        Vector3 initScale = transform.localScale;
        float spinSpeed = 600f; // deg/s
        float flySpeed  = 4f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(initScale, Vector3.zero, t * t);
            transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
            transform.position += (Vector3)(_deathDir * flySpeed * (1f - t) * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }

    // ── 웨이브 구간 스탯 적용 ────────────────────────────────────
    /// <summary>스폰 시 EnemySpawner에서 호출 — 현재 웨이브에 맞는 breakpoint 적용.</summary>
    public void InitForWave(int wave)
    {
        if (waveBreakpoints == null || waveBreakpoints.Count == 0) return;

        WaveStatBreakpoint matched = null;
        foreach (var bp in waveBreakpoints)
            if (bp.fromWave <= wave && (matched == null || bp.fromWave > matched.fromWave))
                matched = bp;

        if (matched == null) return;

        if (matched.moveSpeed     >= 0f) moveSpeed     = matched.moveSpeed;
        if (matched.maxHP         >= 0f) { maxHP = matched.maxHP; currentHP = maxHP; }
        if (matched.contactDamage >= 0f) contactDamage = matched.contactDamage;
        if (matched.goldDrop      >= 0)  goldDrop      = matched.goldDrop;
        if (matched.expDrop       >= 0)  expDrop       = matched.expDrop;
    }

    public void ApplyWaveScaling(int wave) { }

    // ── 스폰 연출 ────────────────────────────────────────────────
    public Vector3 SpawnScale { get; private set; } = Vector3.one;

    public void PlaySpawnBounce()
    {
        SpawnScale = transform.localScale; // BounceIn이 스케일을 바꾸기 전에 저장
        StartCoroutine(BounceIn());
    }

    private IEnumerator BounceIn()
    {
        enabled = false;
        if (rb != null) rb.constraints = RigidbodyConstraints2D.FreezeAll;

        (float scale, float time)[] keys =
        {
            (0.0f, 0.00f), (1.3f, 0.20f), (0.85f, 0.32f), (1.1f, 0.40f), (1.0f, 0.48f),
        };

        for (int i = 0; i < keys.Length - 1; i++)
        {
            float fromScale = keys[i].scale;
            float toScale   = keys[i + 1].scale;
            float duration  = keys[i + 1].time - keys[i].time;
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.one * Mathf.Lerp(fromScale, toScale, t / duration);
                yield return null;
            }
        }

        transform.localScale = Vector3.one;
        if (rb != null) rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        enabled = true;
    }

    // ── FXManager 없을 때 파편 인라인 스폰 ──────────────────────
    private static void SpawnDebrisFallback(Vector3 pos)
    {
        // 임시 호스트 GO에서 코루틴 실행 (적 오브젝트 파괴 후에도 유지)
        var host = new GameObject("__DebrisHost");
        host.AddComponent<DebrisHostComponent>().Run(pos);
    }

    private class DebrisHostComponent : MonoBehaviour
    {
        public void Run(Vector3 pos) => StartCoroutine(Routine(pos));

        private static Sprite _spr;
        private static Sprite GetSprite()
        {
            if (_spr != null) return _spr;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color32[16];
            for (int i = 0; i < 16; i++) pixels[i] = new Color32(160, 160, 160, 255);
            tex.SetPixels32(pixels);
            tex.Apply(false);
            _spr = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 16f);
            return _spr;
        }

        private IEnumerator Routine(Vector3 pos)
        {
            const int count = 8;
            var gos  = new GameObject[count];
            var srs  = new SpriteRenderer[count];
            var vels = new Vector2[count];
            var spr  = GetSprite();

            for (int i = 0; i < count; i++)
            {
                gos[i] = new GameObject("Debris");
                gos[i].transform.position   = pos;
                gos[i].transform.localScale = Vector3.one * Random.Range(0.06f, 0.18f);
                srs[i] = gos[i].AddComponent<SpriteRenderer>();
                srs[i].sprite       = spr;
                srs[i].color        = new Color(0.55f, 0.55f, 0.55f, 1f);
                srs[i].sortingOrder = 5;
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float speed = Random.Range(2.5f, 6f);
                vels[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
            }

            float duration = 0.5f, elapsed = 0f;
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                for (int i = 0; i < count; i++)
                {
                    if (gos[i] == null) continue;
                    gos[i].transform.position += (Vector3)(vels[i] * Time.deltaTime);
                    vels[i] *= 1f - 5f * Time.deltaTime;
                    if (srs[i]) srs[i].color = new Color(0.55f, 0.55f, 0.55f, 1f - t);
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            for (int i = 0; i < count; i++)
                if (gos[i] != null) Destroy(gos[i]);
            Destroy(gameObject);
        }
    }
}
