using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VirtualJoystick으로부터 방향을 받아 캐릭터를 이동합니다.
/// </summary>
public class PlayerController : MonoBehaviour
{
    public static PlayerController Instance { get; private set; }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Stats")]
    [SerializeField] private float  maxHP = 100f;
    [SerializeField] private float  currentHP;

    [Header("증강 스탯")]
    [SerializeField] private float attack  = 0f;  // 공격력 보너스
    [SerializeField] private float defense = 0f;  // 방어력 (받는 피해 감소)
    [SerializeField, Range(0f, 0.9f)] private float dodge = 0f;  // 회피 확률 0~0.9

    [Header("피격 무적 시간")]
    [SerializeField] private float hitInvincibleDuration = 0.2f;

    [Header("플로팅 텍스트 앵커")]
    [SerializeField] private Transform floatingTextAnchor;  // 플레이어 자식 오브젝트로 배치

    /// <summary>플로팅 텍스트 앵커 Transform. 없으면 null.</summary>
    public Transform FloatingTextAnchor => floatingTextAnchor;

    /// <summary>플로팅 텍스트 스폰 위치. 앵커가 없으면 플레이어 머리 위 기본값.</summary>
    public Vector3 FloatingTextPoint => floatingTextAnchor != null
        ? floatingTextAnchor.position
        : (Vector3)Position + Vector3.up * 0.8f;

    // 무기 타입별 보너스 (런타임 전용)
    private float rangedAttackBonus = 0f;
    private float meleeAttackBonus  = 0f;
    private float waveHealAmount    = 0f;
    private Coroutine knockbackRoutine;

    // 증강 추가 전투 스탯
    private float critRateBonus;    // 치명타 확률 추가 (fraction, 예: 0.1 = 10%)
    private float critDamageBonus;  // 치명타 데미지 배율 추가 (예: 0.5 = +50%)
    private float bleedDamageBonus; // 출혈 데미지 추가 (flat)
    private float healOnKillChance; // 적 처치 시 회복 확률 (fraction)
    private float healOnKillFactor; // 적 처치 시 회복량 — MaxHP 기준 비율 (fraction)
    private float healOrbChance;    // 회복 구슬 스폰 확률 (fraction)
    private float healOrbFactor;    // 회복 구슬 1개가 회복하는 양 — MaxHP 기준 비율 (fraction)
    private float _hpRegenRate;     // 초당 자동 회복량 (PlayerStatManager 설정)
    private float _knockbackPower;  // 넉백 지수 (PlayerStatManager 설정)
    private float _magnetRadius;    // 흡수 반경 (PlayerStatManager 설정)

    // ── 크기 모드 (빅/스몰) ───────────────────────────────────────────
    private bool      _isBigMode;
    private bool      _isSmallMode;
    private float     _sizeSpeedMultiplier = 1f;
    private Vector3   _originalVisualScale;
    private float     _bigWalkShakeTimer;
    private float     _bigContactTimer;
    private Coroutine _sizeEffectCoroutine;
    private bool      _enforceSizeScale;   // true이면 LateUpdate에서 매 프레임 스케일 유지
    public  bool      IsSizeEffectActive => _sizeEffectCoroutine != null;

    // 빅 모드 걷기 hop 연출
    private float    _bigHopYOffset  = 0f;  // LateUpdate에서 Y 점프 오프셋 적용 (SlimeBob 우회)
    private Sequence _bigHopSequence;
    private Vector3  _visualSlotOrigLocalPos;

    private const float BigScaleMultiplier   = 4f;
    private const float SmallScaleMultiplier = 2f;    // 1/2 배율
    private const float BigSpeedFactor       = 0.3f;
    private const float SmallSpeedFactor     = 0.8f;
    private const float BigDamageReduction   = 0.5f;
    private const float SmallDamageMult      = 1.5f;
    private const float SmallEvasionBonus    = 30f;   // +30% 회피
    private const float SmallCameraZoom      = 0.65f; // 65% 오소그래픽 (명확한 확대)
    private const float BigContactKnockback  = 30f;
    private const float BigContactInterval   = 0.5f;
    private const float BigWalkShakeInterval = 0.22f;

    public float CritRateBonus    => critRateBonus;
    public float CritDamageBonus  => critDamageBonus;
    public float BleedDamageBonus => bleedDamageBonus;
    public float HealOnKillChance => healOnKillChance;
    public float HealOnKillFactor => healOnKillFactor;
    public float HealOrbChance    => healOrbChance;
    public float HealOrbFactor    => healOrbFactor;
    public float KnockbackPower   => _knockbackPower;
    public float MagnetRadius     => _magnetRadius;
    public float CurrentHP        => currentHP;
    public float MaxHPValue       => maxHP;

    public float Attack            => attack; // 기초 스탯 + 증강
    public float Defense           => defense;
    public float MoveSpeedBase     => moveSpeed; // 기초값 (시너지/캐릭터 보너스 제외)
    public float Dodge             => dodge;
    public float RangedAttackBonus => rangedAttackBonus;
    public float MeleeAttackBonus  => meleeAttackBonus;
    public float WaveHealAmount    => waveHealAmount;

    [Header("플레이어 위 HP 슬라이더 (World Space)")]
    [SerializeField] private Slider playerHpSlider;

    [Header("먼지 트레일")]
    [SerializeField] private ParticleSystem dustTrailEffect;

    [Header("애니메이션")]
    [SerializeField] private Animator playerAnimator;

    private static readonly int MoveValueHash = Animator.StringToHash("moveValue");

    private Vector2        moveDirection;
    private Rigidbody2D    rb;
    [Header("비주얼")]
    [SerializeField] private Transform visualSlot;   // 미할당 시 Awake에서 "VisualSlot" 자동 탐색

    public Transform VisualSlot => _visualSlot;

    private SpriteRenderer spriteRenderer;
    private Transform      _visualSlot;
    private HitFlash       hitFlash;
    private HitShake       hitShake;
    private Canvas         _hpBarCanvas;
    private const float    HpBarSortMultiplier = 100f;
    private const int      HpBarSortOffset     = 200; // 항상 캐릭터·적 위에 표시

    private Coroutine _hitInvincibleCoroutine;
    private bool           isInvincible;
    private bool           _skillInvincible;

    private void Awake()
    {
        Instance      = this;
        rb            = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _visualSlot             = visualSlot != null ? visualSlot : transform.Find("VisualSlot");
        _originalVisualScale    = _visualSlot != null ? _visualSlot.localScale : Vector3.one;
        _visualSlotOrigLocalPos = _visualSlot != null ? _visualSlot.localPosition : Vector3.zero;
        hitFlash      = GetComponentInChildren<HitFlash>();
        hitShake      = GetComponent<HitShake>();
        currentHP     = maxHP;

        if (playerHpSlider != null)
        {
            _hpBarCanvas = playerHpSlider.GetComponentInParent<Canvas>();
        }
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        if (Instance == this) Instance = null;
    }

    /// <summary>모델 교체 후 PlayerCharacterManager에서 호출 — 새 Animator 연결.</summary>
    public void SetAnimator(Animator anim)
    {
        playerAnimator = anim;
    }

    /// <summary>모델 교체 후 PlayerCharacterManager에서 호출 — SpriteRenderer 참조 갱신.</summary>
    public void RefreshVisualReferences()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _visualSlot    = visualSlot != null ? visualSlot : transform.Find("VisualSlot");
        hitFlash       = GetComponentInChildren<HitFlash>();
        hitFlash?.RefreshRenderers();
    }

    private void Start()
    {
        Debug.Log($"[PlayerController] Start — localScale={transform.localScale}, lossyScale={transform.lossyScale}, parent={transform.parent?.name ?? "none"}");
        if (floatingTextAnchor == null)
        {
            floatingTextAnchor = transform.Find("FloatingTextAnchor");
            if (floatingTextAnchor == null)
            {
                Debug.LogWarning("[PlayerController] FloatingTextAnchor 자식 오브젝트를 찾을 수 없습니다. 플레이어 머리 위 기본 위치를 사용합니다.");
            }
        }
        SyncHPUI();
        StartCoroutine(HPRegenRoutine());
    }

    private void Update()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;
        HandleInput();
        FlipSprite();
        UpdateAnimator();
    }

    private void LateUpdate()
    {
        if (_hpBarCanvas != null)
            _hpBarCanvas.sortingOrder = -Mathf.RoundToInt(transform.position.y * HpBarSortMultiplier) + HpBarSortOffset;

        // 크기 모드 스케일 강제 유지: Animator 등 다른 컴포넌트의 덮어쓰기 방지
        if (_enforceSizeScale && _visualSlot != null)
        {
            float curSign = Mathf.Sign(_visualSlot.localScale.x);
            float baseMag;
            if (_isBigMode)
                baseMag = Mathf.Abs(_originalVisualScale.x) * BigScaleMultiplier;
            else if (_isSmallMode)
                baseMag = Mathf.Abs(_originalVisualScale.x) / SmallScaleMultiplier;
            else
            {
                _enforceSizeScale = false;
                return;
            }
            _visualSlot.localScale = new Vector3(curSign * baseMag, baseMag, baseMag);
        }

        // 빅 모드 Y 점프 오프셋 — SlimeBobAnimation.Update() 이후 LateUpdate에서 덮어씀
        if (_isBigMode && _enforceSizeScale && _visualSlot != null)
        {
            _visualSlot.localPosition = new Vector3(
                _visualSlotOrigLocalPos.x,
                _visualSlotOrigLocalPos.y + _bigHopYOffset,
                _visualSlotOrigLocalPos.z);
        }
    }

    private void UpdateAnimator()
    {
        if (playerAnimator == null) return;
        playerAnimator.SetFloat(MoveValueHash, moveDirection.magnitude);
    }

    private void FixedUpdate()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // InputLocked 중(컷씬 등)에는 외부 코드가 rb를 직접 제어하므로 물리 이동을 중단
        if (InputLocked)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 desired = rb.position + moveDirection * moveSpeed * _sizeSpeedMultiplier * Time.fixedDeltaTime;
        desired = ClampToBounds(desired);

        rb.linearVelocity = Vector2.zero;
        rb.MovePosition(desired);

        // 이동 중 먼지 트레일 재생
        if (dustTrailEffect != null)
        {
            bool moving = moveDirection.sqrMagnitude > 0.001f;
            if (moving && !dustTrailEffect.isPlaying) dustTrailEffect.Play();
            else if (!moving && dustTrailEffect.isPlaying) dustTrailEffect.Stop();
        }

        // 빅 모드: 걷기 카메라 흔들림 + 주변 적 넉백
        if (_isBigMode)
        {
            if (moveDirection.sqrMagnitude > 0.01f)
            {
                _bigWalkShakeTimer -= Time.fixedDeltaTime;
                if (_bigWalkShakeTimer <= 0f)
                {
                    CameraFollow.Shared?.Shake(0.15f, 0.07f);
                    _bigWalkShakeTimer = BigWalkShakeInterval;
                    TriggerBigHop();
                }
            }

            _bigContactTimer -= Time.fixedDeltaTime;
            if (_bigContactTimer <= 0f)
            {
                ApplyBigModeContact();
                _bigContactTimer = BigContactInterval;
            }
        }
    }

    private Vector2 ClampToBounds(Vector2 desired)
    {
        var bounds = MovementBounds.Shared;
        if (bounds == null) return desired;
        if (bounds.IsInside(desired)) return desired;
        return bounds.ClosestPointOnBoundary(desired);
    }

    /// <summary>true이면 조이스틱 입력을 무시 (컷씬용).</summary>
    public bool InputLocked { get; set; }

    private void HandleInput()
    {
        moveDirection = (!InputLocked && VirtualJoystick.Instance != null)
            ? VirtualJoystick.Instance.Direction
            : Vector2.zero;
    }

    private void FlipSprite()
    {
        if (moveDirection.x > 0.05f)
        {
            SetFacingRight(true);
            if (dustTrailEffect != null) dustTrailEffect.transform.localScale = Vector3.one;
        }
        else if (moveDirection.x < -0.05f)
        {
            SetFacingRight(false);
            if (dustTrailEffect != null) dustTrailEffect.transform.localScale = new Vector3(-1f, 1f, 1f);
        }
    }

    // VisualSlot 전체 반전 → 자식 SpriteRenderer 전부 적용. 없으면 단일 SR flipX 폴백
    private void SetFacingRight(bool right)
    {
        if (_visualSlot != null)
        {
            Vector3 s = _visualSlot.localScale;
            s.x = right ? Mathf.Abs(s.x) : -Mathf.Abs(s.x);
            _visualSlot.localScale = s;
        }
        else if (spriteRenderer != null)
        {
            spriteRenderer.flipX = !right;
        }
    }

    public void TakeDamage(float damage, bool isFromBoss = false, bool isFromElite = false,
                            string sourceName = null, Vector2 knockbackDir = default, float knockbackForce = 0f)
    {
        this.DLog($"[피해] {damage:0.#}  ← {sourceName ?? "unknown"}");

        if (_debugInvincible) return;
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;
        if (isInvincible || _skillInvincible)
            return;

        if (dodge > 0f && Random.value < dodge)
        {
            FXManager.Shared?.SpawnPlayerText("MISS", new Color(1f, 0.95f, 0.4f));
            return;
        }

        // defense = 피해 감소율(%). 최대 75% 상한 (DEF≥100이어도 완전 무적 방지)
        float reduction = Mathf.Clamp(defense / 100f, 0f, 0.75f);
        float reduced   = damage * (1f - reduction);
        if (_isBigMode)   reduced *= (1f - BigDamageReduction); // 빅 모드: 피해 50% 감소
        if (_isSmallMode) reduced *= SmallDamageMult;            // 스몰 모드: 피해 150%
        var relic = RelicManager.Shared;
        if (isFromBoss  && relic != null) reduced *= (1f - relic.BossDmgReduceTotal);
        if (isFromBoss) reduced *= (1f - WeaponHighGradeSkill.ActiveDamageReduction);
        if (isFromBoss) reduced *= (1f - WeaponSpecialEffectState.BossDamageReductionPct);
        if (isFromElite && relic != null) reduced *= (1f - relic.EliteDmgReduceTotal);
        currentHP -= reduced;
        currentHP  = Mathf.Max(0f, currentHP);
        SyncHPUI();
        // 피격 데미지 표시
        FXManager.Shared?.SpawnPlayerText(Mathf.RoundToInt(reduced).ToString(), Color.red);
        SoundManager.Shared?.Play(SoundKey.Player_Hit);
        hitFlash?.Flash();
        hitShake?.Shake();
        CameraFollow.Shared?.Shake();
        TriggerHaptic();

        // 피격 후 짧은 무적 (여러 적에 동시에 맞는 것 방지)
        if (hitInvincibleDuration > 0f)
            RestartHitInvincible();

        if (knockbackForce > 0f && knockbackDir != Vector2.zero)
        {
            if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
            knockbackRoutine = StartCoroutine(PlayerKnockbackRoutine(knockbackDir.normalized, knockbackForce));
        }

        if (currentHP <= 0f)
        {
            // SYN_ALL_MYTH_REVIVE: 신화 6종 시너지 — 사망 시 1회 즉시 부활
            if (WeaponSynergyFlags.MythReviveAvailable)
            {
                WeaponSynergyFlags.MythReviveAvailable = false;
                Revive();
                FXManager.Shared?.SpawnPlayerText("부활!", new Color(1f, 0.85f, 0f));
            }
            else
            {
                GameManager.Shared?.TriggerGameOver();
            }
        }
    }

    private IEnumerator PlayerKnockbackRoutine(Vector2 dir, float force)
    {
        InputLocked = true;
        float duration = 0.25f;
        float elapsed  = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            if (rb != null)
                rb.linearVelocity = Vector2.Lerp(dir * force, Vector2.zero, elapsed / duration);
            yield return new WaitForFixedUpdate();
        }
        if (rb != null) rb.linearVelocity = Vector2.zero;
        InputLocked = false;
        knockbackRoutine = null;
    }

    private void SyncHPUI()
    {
        GameEvents.HPChanged(currentHP, maxHP);
        if (playerHpSlider)
        {
            playerHpSlider.minValue = 0f;
            playerHpSlider.maxValue = Mathf.Max(1f, maxHP);
            playerHpSlider.value    = currentHP;
        }
    }

    // ────────────────────────────────────────────────────────────────
    // PlayerStatManager 연동 — 스탯 시트 일괄 적용
    // ────────────────────────────────────────────────────────────────

    /// <summary>
    /// PlayerStatManager가 변경될 때 호출 — 모든 기본 스탯을 최종값으로 갱신합니다.
    /// 시너지·처치 회복 등 별도 보너스는 그대로 유지됩니다.
    /// </summary>
    public void ApplyStatSheet(PlayerStatManager sm)
    {
        // 기본 전투 스탯
        attack           = sm.GetFinal(StatType.ATK);
        defense          = sm.GetFinal(StatType.DEF);
        moveSpeed        = sm.GetFinal(StatType.MoveSpeed);
        dodge            = Mathf.Clamp(sm.GetFinal(StatType.Evasion), 0f, 0.75f);
        critRateBonus    = sm.GetFinal(StatType.CritChance) / 100f;
        critDamageBonus  = sm.GetFinal(StatType.CritDmg)    / 100f;
        bleedDamageBonus = sm.GetFinal(StatType.BleedPower);
        healOnKillChance = Mathf.Clamp(sm.GetFinal(StatType.HealOnKill) / 100f, 0f, 1f);
        _hpRegenRate     = sm.GetFinal(StatType.HPRegen);
        _knockbackPower  = sm.GetFinal(StatType.Knockback);
        _magnetRadius    = sm.GetFinal(StatType.MagnetRadius);

        // 최대 체력 — 증가분만큼 현재 체력도 함께 올림
        float newMaxHP = sm.GetFinal(StatType.MaxHP);
        if (!Mathf.Approximately(newMaxHP, maxHP))
        {
            float delta = newMaxHP - maxHP;
            maxHP     = Mathf.Max(10f, newMaxHP);
            currentHP = delta > 0f
                ? Mathf.Min(currentHP + delta, maxHP)
                : Mathf.Min(currentHP, maxHP);
            SyncHPUI();
        }

        // 공격속도(AtkSpeed) — 모든 WeaponPoint에 SM 배율 적용
        float atkSpeedPct = sm.GetFinal(StatType.AtkSpeed);
        foreach (var wp in GetComponentsInChildren<WeaponPoint>())
            wp.SetStatSpeedPct(atkSpeedPct);

        // 기물 쿨타임(StructureCooldown) — StructureInventory에서 직접 참조
    }

    // ────────────────────────────────────────────────────────────────
    // 체력 자동 회복 (HPRegen)
    // ────────────────────────────────────────────────────────────────

    private IEnumerator HPRegenRoutine()
    {
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            yield return wait;
            if (_hpRegenRate > 0f && currentHP < maxHP
                && (GameManager.Shared == null || !GameManager.Shared.IsGameOver))
            {
                HealHP(_hpRegenRate);
            }
        }
    }

    // ────────────────────────────────────────────────────────────────
    // 증강 메서드 (ImprovManager에서 호출)
    // ────────────────────────────────────────────────────────────────
    public void HealHP(float amount)
    {
        currentHP = Mathf.Min(currentHP + amount, maxHP);
        SyncHPUI();
    }

    public void IncreaseMaxHP(float amount)
    {
        maxHP     += amount;
        currentHP  = Mathf.Min(currentHP + amount, maxHP);
        SyncHPUI();
    }

    public void IncreaseAttack(float amount)
    {
        attack += amount;
    }

    public void IncreaseDefense(float amount)
    {
        defense += amount;
    }

    public void IncreaseMoveSpeed(float amount)
    {
        moveSpeed += amount;
    }

    [Header("몸집 한계")]
    [SerializeField] private float minScale = 0.4f;
    [SerializeField] private float maxScale = 3.0f;

    /// <summary>몸집을 scaleMultiplier배 키우고 최대 체력 hpBonus만큼 증가.</summary>
    public void IncreaseSizeHP(float scaleMultiplier, float hpBonus)
    {
        if (_visualSlot != null)
        {
            Vector3 s      = _visualSlot.localScale;
            float   newMag = Mathf.Min(Mathf.Abs(s.x) * scaleMultiplier, maxScale);
            _visualSlot.localScale = new Vector3(Mathf.Sign(s.x) * newMag, newMag, newMag);
        }
        maxHP     += hpBonus;
        currentHP  = Mathf.Min(currentHP + hpBonus, maxHP);
        SyncHPUI();
    }

    /// <summary>몸집을 scaleMultiplier배 줄이고 최대 체력 hpPenalty만큼 감소.</summary>
    public void DecreaseSizeHP(float scaleMultiplier, float hpPenalty)
    {
        if (_visualSlot != null)
        {
            Vector3 s      = _visualSlot.localScale;
            float   newMag = Mathf.Max(Mathf.Abs(s.x) / scaleMultiplier, minScale);
            _visualSlot.localScale = new Vector3(Mathf.Sign(s.x) * newMag, newMag, newMag);
        }
        float newMaxHP = Mathf.Max(maxHP * 0.5f, maxHP - hpPenalty);
        maxHP     = Mathf.Max(10f, newMaxHP);
        currentHP = Mathf.Min(currentHP, maxHP);
        SyncHPUI();
    }

    // ── 크기 모드 (빅 / 스몰) ─────────────────────────────────────────

    /// <summary>빅 모드 활성화. 한 웨이브 동안 4배 크기로 거대화.</summary>
    public void ActivateBigMode()
    {
        if (_isBigMode) return;
        if (_isSmallMode) DeactivateSmallModeImmediate();
        _isBigMode           = true;
        _sizeSpeedMultiplier = BigSpeedFactor;
        _bigContactTimer     = BigContactInterval;
        _bigWalkShakeTimer   = 0f;
        _bigHopYOffset       = 0f;
        WeaponManager.Instance?.SetWeaponVisualScale(1.5f);
        WeaponManager.Instance?.SetWeaponAnchorSpread(2.5f);
        if (_sizeEffectCoroutine != null) StopCoroutine(_sizeEffectCoroutine);
        _sizeEffectCoroutine = StartCoroutine(BigModeEntryEffect());
    }

    /// <summary>스몰 모드 활성화. 한 웨이브 동안 1/2 크기로 축소.</summary>
    public void ActivateSmallMode()
    {
        if (_isSmallMode) return;
        if (_isBigMode) DeactivateBigModeImmediate();
        _isSmallMode         = true;
        _sizeSpeedMultiplier = SmallSpeedFactor;
        PlayerStatManager.Shared?.AddPctMod(StatType.Evasion, SmallEvasionBonus);
        CameraFollow.Shared?.SetZoomMultiplier(SmallCameraZoom);
        WeaponManager.Instance?.SetWeaponVisualScale(1f / SmallScaleMultiplier);
        if (_sizeEffectCoroutine != null) StopCoroutine(_sizeEffectCoroutine);
        _sizeEffectCoroutine = StartCoroutine(SmallModeEntryEffect());
    }

    // 스몰 모드 진입 연출: 마리오 파워다운처럼 큰크기↔작은크기 교대 후 작은 크기로 정착
    private IEnumerator SmallModeEntryEffect()
    {
        yield return new WaitUntil(() => Time.timeScale > 0f);
        if (_visualSlot == null) yield break;

        float sign    = Mathf.Sign(_originalVisualScale.x);
        float fromMag = Mathf.Abs(_originalVisualScale.x);
        float toMag   = fromMag / SmallScaleMultiplier;

        var   srs        = GetComponentsInChildren<SpriteRenderer>();
        Color flashColor = new Color(0.6f, 1f, 0.65f);

        // 마지막(홀수 인덱스)이 small로 끝남
        float[] intervals = { 0.05f, 0.05f, 0.06f, 0.07f, 0.08f, 0.09f };

        for (int i = 0; i < intervals.Length; i++)
        {
            bool   isSmall  = (i % 2 == 1);
            float  mag      = isSmall ? toMag : fromMag;
            float  curSign  = _visualSlot != null ? Mathf.Sign(_visualSlot.localScale.x) : sign;
            if (_visualSlot != null)
                _visualSlot.localScale = new Vector3(curSign * mag, mag, mag);
            foreach (var sr in srs) sr.color = isSmall ? flashColor : Color.white;
            yield return new WaitForSecondsRealtime(intervals[i]);
        }

        if (_visualSlot != null)
        {
            float curSign = Mathf.Sign(_originalVisualScale.x);
            float mag     = Mathf.Abs(_originalVisualScale.x) / SmallScaleMultiplier;
            _visualSlot.localScale = new Vector3(curSign * mag, mag, mag);
        }
        foreach (var sr in srs) sr.color = Color.white;
        _sizeEffectCoroutine = null;
        _enforceSizeScale = true;
    }

    /// <summary>활성 중인 크기 모드를 웨이브 종료 시 해제합니다. 스몰 모드는 마리오 귀환 연출 포함.</summary>
    public void DeactivateSizeEffects()
    {
        if (_isBigMode) DeactivateBigModeImmediate();
        if (_isSmallMode)
        {
            _isSmallMode         = false;
            _enforceSizeScale    = false; // 종료 연출 중 LateUpdate 덮어쓰기 방지
            _sizeSpeedMultiplier = 1f;
            PlayerStatManager.Shared?.AddPctMod(StatType.Evasion, -SmallEvasionBonus);
            CameraFollow.Shared?.ResetZoom();
            // 무기 스케일은 SmallModeExitEffect 완료 시 복구 (연출 중 일관성 유지)
            if (_sizeEffectCoroutine != null) StopCoroutine(_sizeEffectCoroutine);
            _sizeEffectCoroutine = StartCoroutine(SmallModeExitEffect());
        }
    }

    private void DeactivateBigModeImmediate()
    {
        if (!_isBigMode) return;
        _isBigMode           = false;
        _enforceSizeScale    = false;
        _sizeSpeedMultiplier = 1f;
        WeaponManager.Instance?.ResetWeaponVisualScale();
        WeaponManager.Instance?.SetWeaponAnchorSpread(1f);
        if (_sizeEffectCoroutine != null) { StopCoroutine(_sizeEffectCoroutine); _sizeEffectCoroutine = null; }
        _bigHopSequence?.Kill();
        _bigHopSequence = null;
        _bigHopYOffset  = 0f;
        if (_visualSlot != null)
        {
            _visualSlot.localScale    = _originalVisualScale;
            _visualSlot.localPosition = _visualSlotOrigLocalPos;
        }
        var srs = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in srs) sr.color = Color.white;
    }

    // 빅 모드 한 걸음 hop: 위로 띄웠다 착지 (Y position만, scale 변경 없음)
    private void TriggerBigHop()
    {
        // 이전 hop이 아직 진행 중이면 건너뜀 (부유 방지)
        if (_visualSlot == null || !_enforceSizeScale || _bigHopSequence != null) return;

        float hopH = Mathf.Abs(_originalVisualScale.y) * BigScaleMultiplier * 0.18f;

        _bigHopSequence = DOTween.Sequence().SetLink(gameObject);
        _bigHopSequence.Append(DOTween.To(() => _bigHopYOffset, v => _bigHopYOffset = v, hopH, 0.09f).SetEase(Ease.OutCubic));
        _bigHopSequence.Append(DOTween.To(() => _bigHopYOffset, v => _bigHopYOffset = v, 0f,   0.13f).SetEase(Ease.InCubic));
        _bigHopSequence.OnComplete(() => { _bigHopYOffset = 0f; _bigHopSequence = null; });
    }

    // 착지 충격으로 맵의 모든 적 공중으로 튀겨 올리기
    private void BounceNearbyEnemies()
    {
        if (EnemyController.All == null) return;
        for (int i = EnemyController.All.Count - 1; i >= 0; i--)
        {
            var enemy = EnemyController.All[i];
            if (enemy != null)
                enemy.ApplyLaunchArc(1.8f, 1.0f);
        }
    }

    private void DeactivateSmallModeImmediate()
    {
        if (!_isSmallMode) return;
        _isSmallMode         = false;
        _enforceSizeScale    = false;
        _sizeSpeedMultiplier = 1f;
        PlayerStatManager.Shared?.AddPctMod(StatType.Evasion, -SmallEvasionBonus);
        CameraFollow.Shared?.ResetZoom();
        WeaponManager.Instance?.ResetWeaponVisualScale();
        if (_visualSlot != null) _visualSlot.localScale = _originalVisualScale;
    }

    // 주변 적에게 넉백 + 피해 (빅 모드 FixedUpdate 틱)
    private void ApplyBigModeContact()
    {
        if (EnemyController.All == null) return;
        float radius   = Mathf.Abs(_originalVisualScale.x) * BigScaleMultiplier * 0.65f;
        float radiusSq = radius * radius;
        Vector2 center = rb.position;
        float dmg      = Mathf.Max(attack * 0.3f, 5f);

        for (int i = EnemyController.All.Count - 1; i >= 0; i--)
        {
            var enemy = EnemyController.All[i];
            if (enemy == null) continue;
            Vector2 diff = (Vector2)enemy.transform.position - center;
            if (diff.sqrMagnitude > radiusSq) continue;
            Vector2 dir = diff.sqrMagnitude > 0.001f ? diff.normalized : Vector2.up;
            enemy.TakeDamage(dmg, false, dir, BigContactKnockback);
            enemy.ApplyLaunchArc(1.2f, 0.5f);
        }
    }

    // 빅 모드 진입 연출: 마리오 버섯처럼 작은크기↔큰크기를 교대하다 큰 크기로 정착
    private IEnumerator BigModeEntryEffect()
    {
        yield return new WaitUntil(() => Time.timeScale > 0f);
        if (_visualSlot == null) yield break;

        float sign    = Mathf.Sign(_originalVisualScale.x);
        float fromMag = Mathf.Abs(_originalVisualScale.x);
        float toMag   = fromMag * BigScaleMultiplier;

        var   srs        = GetComponentsInChildren<SpriteRenderer>();
        Color flashColor = new Color(1f, 0.85f, 0.2f);

        // 간격: 빠르게 시작 → 점점 느려지며 정착 / 마지막(홀수 인덱스)이 big으로 끝남
        float[] intervals = { 0.05f, 0.05f, 0.06f, 0.07f, 0.07f, 0.09f, 0.10f, 0.10f };

        for (int i = 0; i < intervals.Length; i++)
        {
            bool   isBig    = (i % 2 == 1);
            float  mag      = isBig ? toMag : fromMag;
            float  curSign  = _visualSlot != null ? Mathf.Sign(_visualSlot.localScale.x) : sign;
            if (_visualSlot != null)
                _visualSlot.localScale = new Vector3(curSign * mag, mag, mag);
            foreach (var sr in srs) sr.color = isBig ? flashColor : Color.white;
            yield return new WaitForSecondsRealtime(intervals[i]);
        }

        if (_visualSlot != null)
        {
            float curSign = Mathf.Sign(_visualSlot.localScale.x);
            _visualSlot.localScale = new Vector3(curSign * toMag, toMag, toMag);
        }
        foreach (var sr in srs) sr.color = Color.white;
        _sizeEffectCoroutine = null;
        _enforceSizeScale = true;
    }

    // 스몰 모드 종료 연출: 마리오 파워다운처럼 크기 펄스 루프 후 원래 크기로
    private IEnumerator SmallModeExitEffect()
    {
        if (_visualSlot == null) yield break;
        float normalMag = Mathf.Abs(_originalVisualScale.x);
        float smallMag  = normalMag / SmallScaleMultiplier;
        var   srs       = GetComponentsInChildren<SpriteRenderer>();
        Color smallCol  = new Color(0.6f, 1f, 0.65f);

        for (int i = 0; i < 8; i++)
        {
            bool showNormal = (i % 2 == 0);
            float mag       = showNormal ? normalMag : smallMag;
            float curSign   = _visualSlot != null ? Mathf.Sign(_visualSlot.localScale.x) : 1f;
            if (_visualSlot != null)
                _visualSlot.localScale = new Vector3(curSign * mag, mag, mag);
            foreach (var sr in srs) sr.color = showNormal ? Color.white : smallCol;
            yield return new WaitForSecondsRealtime(0.09f);
        }

        if (_visualSlot != null)
        {
            float curSign = Mathf.Sign(_visualSlot.localScale.x);
            float finalX  = curSign * Mathf.Abs(_originalVisualScale.x);
            _visualSlot.localScale = new Vector3(finalX, _originalVisualScale.y, _originalVisualScale.z);
        }
        foreach (var sr in srs) sr.color = Color.white;
        // 연출 완료 후 무기 스케일 복구 — 플레이어가 정상 크기로 돌아온 시점에 맞춤
        WeaponManager.Instance?.ResetWeaponVisualScale();
        _sizeEffectCoroutine = null;
    }

    /// <summary>회피 확률 증가. 최대 0.75(75%)로 제한합니다.</summary>
    public void IncreaseDodge(float amount)
    {
        dodge = Mathf.Clamp(dodge + amount, 0f, 0.75f);
    }

    public void IncreaseCritRate(float amount)   => critRateBonus    = Mathf.Clamp(critRateBonus   + amount, 0f, 0.95f);
    public void IncreaseCritDamage(float amount) => critDamageBonus  += amount;
    public void IncreaseBleedDamage(float amount)=> bleedDamageBonus += amount;

    /// <summary>
    /// 적 처치 시 회복량 비율(MaxHP 기준) 추가.
    /// 발동 확률은 PlayerStatManager.Shared.GetFinal(StatType.HealOnKill) 자동 동기화됨.
    /// </summary>
    public void IncreaseHealOnKillFactor(float healFactor)
    {
        healOnKillFactor += healFactor;
    }

    /// <summary>
    /// 회복 구슬 스폰 설정.
    /// chance = 0~1 확률 / healFactor = MaxHP 대비 회복 비율 (예: 0.10 = MaxHP의 10%)
    /// </summary>
    public void IncreaseHealOrbChance(float chance, float healFactor)
    {
        healOrbChance += chance;
        healOrbFactor += healFactor;
    }

    /// <summary>적 처치 시 healOnKillChance 확률로 MaxHP의 healOnKillFactor 비율만큼 회복. (EnemyController.OnDeath에서 호출)</summary>
    public void TryHealOnKill()
    {
        if (healOnKillChance <= 0f || healOnKillFactor <= 0f) return;
        if (Random.value <= healOnKillChance)
        {
            float amount = maxHP * healOnKillFactor;
            HealHP(amount);
            FXManager.Shared?.SpawnPlayerText($"+{Mathf.RoundToInt(amount)}", Color.green);
        }
    }

    /// <summary>회복 구슬 수집 시 MaxHP의 healOrbFactor 비율만큼 회복. (FXManager/HealOrb에서 호출)</summary>
    public void HealFromOrb()
    {
        if (healOrbFactor <= 0f) return;
        HealHP(maxHP * healOrbFactor);
    }

    /// <summary>회복 구슬 스폰 확률. (FXManager 또는 HealOrb 스포너에서 참조)</summary>
    public float HealOrbSpawnChance => healOrbChance;

    private void RestartHitInvincible()
    {
        if (_hitInvincibleCoroutine != null) StopCoroutine(_hitInvincibleCoroutine);
        _hitInvincibleCoroutine = StartCoroutine(HitInvincibleRoutine(hitInvincibleDuration));
    }

    private IEnumerator HitInvincibleRoutine(float duration)
    {
        isInvincible = true;

        float elapsed    = 0f;
        float blinkInterval = 0.05f;
        bool  yellowOn   = false;
        Color yellow     = new Color(1f, 0.92f, 0.2f, 1f);

        while (elapsed < duration)
        {
            elapsed += blinkInterval;
            yellowOn = !yellowOn;
            if (spriteRenderer != null)
                spriteRenderer.color = yellowOn ? yellow : Color.white;
            yield return new WaitForSeconds(blinkInterval);
        }

        if (spriteRenderer != null) spriteRenderer.color = Color.white;
        isInvincible = false;
    }

    /// <summary>공격속도를 amount% 증가 (쿨타임 단축).</summary>
    public void IncreaseAttackSpeed(float amount)
    {
        float multiplier = 1f + amount / 100f;
        foreach (var wp in GetComponentsInChildren<WeaponPoint>())
            wp.MultiplyAttackSpeed(multiplier);
    }

    /// <summary>원거리 무기 전용 추가 공격력.</summary>
    public void IncreaseRangedAttackBonus(float amount) => rangedAttackBonus += amount;

    /// <summary>근거리 무기 전용 추가 공격력.</summary>
    public void IncreaseMeleeAttackBonus(float amount) => meleeAttackBonus += amount;

    /// <summary>웨이브 시작 시 자동 회복량을 늘립니다. (GameManager가 웨이브 시작 시 호출)</summary>
    public void IncreaseWaveHeal(float amount) => waveHealAmount += amount;

    /// <summary>주기적으로 주변 적을 밀어냅니다. 중복 호출 시 기존 루틴을 교체합니다.</summary>
    public void StartPeriodicKnockback(float interval, float force, float radius = 4f)
    {
        if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
        knockbackRoutine = StartCoroutine(PeriodicKnockbackRoutine(interval, force, radius));
    }

    private System.Collections.IEnumerator PeriodicKnockbackRoutine(float interval, float force, float radius)
    {
        var wait = new WaitForSeconds(interval);
        while (true)
        {
            yield return wait;
            foreach (var enemy in EnemyController.All)
            {
                Vector2 dir = (enemy.transform.position - transform.position);
                if (dir.magnitude <= radius)
                    enemy.Knockback(dir.normalized, force);
            }
        }
    }

    private bool _debugInvincible;

    /// <summary>디버그 전용 — 무적 영구 토글. isInvincible과 완전히 독립적으로 동작합니다.</summary>
    public void SetDebugInvincible(bool on) => _debugInvincible = on;

    /// <summary>스킬 등 외부에서 무적 on/off. HitInvincible과 독립적으로 동작합니다.</summary>
    public void SetSkillInvincible(bool on) => _skillInvincible = on;

    /// <summary>GameManager.Revive()에서 호출 — HP를 가득 채우고 3초 무적 부여</summary>
    public void Revive()
    {
        // 진행 중인 피격 무적 코루틴이 isInvincible을 false로 덮어쓰지 못하게 취소
        if (_hitInvincibleCoroutine != null)
        {
            StopCoroutine(_hitInvincibleCoroutine);
            _hitInvincibleCoroutine = null;
        }
        currentHP = maxHP;
        SyncHPUI();
        StartCoroutine(InvincibleRoutine(3f));
    }

    private IEnumerator InvincibleRoutine(float duration)
    {
        isInvincible = true;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // 0.15초 주기로 깜빡임
            if (spriteRenderer) spriteRenderer.enabled = !spriteRenderer.enabled;
            yield return new WaitForSeconds(0.15f);
            elapsed += 0.15f;
        }
        if (spriteRenderer) spriteRenderer.enabled = true;
        isInvincible = false;
    }

    public Vector2 Position => transform.position;
    public Vector2 MoveDirection => moveDirection;


    /// <summary>피격 시 짧은 햅틱 진동 (모바일 전용).</summary>
    private static void TriggerHaptic()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var player   = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            int sdk = new AndroidJavaClass("android.os.Build$VERSION").GetStatic<int>("SDK_INT");
            if (sdk >= 26)
            {
                using var veClass = new AndroidJavaClass("android.os.VibrationEffect");
                using var effect  = veClass.CallStatic<AndroidJavaObject>("createOneShot", 25L, 120);
                vibrator.Call("vibrate", effect);
            }
            else
            {
                vibrator.Call("vibrate", 40L);
            }
        }
        catch { /* 진동 실패 시 무시 */ }
#elif UNITY_IOS && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }
}
