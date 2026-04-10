using System.Collections;
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
    [SerializeField] private float hitInvincibleDuration = 0.3f;  // 피격 후 무적 시간(초)

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

    public float CritRateBonus    => critRateBonus    + _synergyCrit;
    public float CritDamageBonus  => critDamageBonus;
    public float BleedDamageBonus => bleedDamageBonus + _synergyBleed;
    public float HealOnKillChance => healOnKillChance;
    public float HealOnKillFactor => healOnKillFactor;
    public float HealOrbChance    => healOrbChance;
    public float HealOrbFactor    => healOrbFactor;
    public float KnockbackPower   => _knockbackPower;
    public float MagnetRadius     => _magnetRadius;
    public float CurrentHP        => currentHP;
    public float MaxHPValue       => maxHP;

    // 시너지 보너스 (SynergyEffectManager가 재설정 — 재계산 가능)
    private float _synergyAtk;
    private float _synergyDef;
    private float _synergySpeed;
    private float _synergyMaxHp;
    private float _synergyBleed;
    private float _synergyCrit;

    public float Attack            => attack; // 기초 스탯 + 증강
    public float SynergyAtkPct     => _synergyAtk; // 시너지 공격력(%)
    public float Defense           => defense + _synergyDef;
    public float MoveSpeedBase     => moveSpeed; // 기초값 (시너지/캐릭터 보너스 제외)
    public float Dodge             => dodge;
    public float RangedAttackBonus => rangedAttackBonus;
    public float MeleeAttackBonus  => meleeAttackBonus;
    public float WaveHealAmount    => waveHealAmount;

    [Header("플레이어 위 HP 슬라이더 (World Space)")]
    [SerializeField] private Slider playerHpSlider;

    [Header("먼지 트레일")]
    [SerializeField] private ParticleSystem dustTrailEffect;

    private Vector2        moveDirection;
    private Rigidbody2D    rb;
    private SpriteRenderer spriteRenderer;
    private HitFlash       hitFlash;
    private HitShake       hitShake;
    private bool           isInvincible;

    private void Awake()
    {
        Instance      = this;
        rb            = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        hitFlash      = GetComponent<HitFlash>();
        hitShake      = GetComponent<HitShake>();
        currentHP     = maxHP;
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        Debug.Log($"[PlayerController] Start — localScale={transform.localScale}, lossyScale={transform.lossyScale}, parent={transform.parent?.name ?? "none"}");
        SyncHPUI();
        StartCoroutine(HPRegenRoutine());
    }

    private void Update()
    {
        if (GameManager.Shared != null && GameManager.Shared.IsGameOver) return;
        HandleInput();
        FlipSprite();
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

        Vector2 desired = rb.position + moveDirection * (moveSpeed + _synergySpeed) * Time.fixedDeltaTime;
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
        if (spriteRenderer == null) return;
        if (moveDirection.x > 0.05f)
        {
            spriteRenderer.flipX = false;
            if (dustTrailEffect != null) dustTrailEffect.transform.localScale = Vector3.one;
        }
        else if (moveDirection.x < -0.05f)
        {
            spriteRenderer.flipX = true;
            if (dustTrailEffect != null) dustTrailEffect.transform.localScale = new Vector3(-1f, 1f, 1f);
        }
    }

    public void TakeDamage(float damage)
    {
        if (isInvincible) return;
        if (dodge > 0f && Random.value < dodge)
        {
            FXManager.Shared?.SpawnPlayerText("회피", new Color(1f, 0.95f, 0.4f));
            return;
        }

        // defense = 피해 감소율(%). defense=10 → 10% 감소, defense=70 → 70% 감소
        float reduction = Mathf.Clamp01(defense / 100f);
        float reduced   = damage * (1f - reduction);
        currentHP -= reduced;
        currentHP  = Mathf.Max(0f, currentHP);
        SyncHPUI();
        SoundManager.Shared?.Play(SoundKey.Player_Hit);
        hitFlash?.Flash();
        hitShake?.Shake();
        CameraFollow.Shared?.Shake();
        TriggerHaptic();

        // 피격 후 짧은 무적 (여러 적에 동시에 맞는 것 방지)
        if (hitInvincibleDuration > 0f)
            StartCoroutine(HitInvincibleRoutine(hitInvincibleDuration));

        if (currentHP <= 0f)
            GameManager.Shared?.TriggerGameOver();
    }

    private void SyncHPUI()
    {
        GameManager.Shared?.UpdateHP(currentHP, maxHP);
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
            maxHP     = Mathf.Max(1f, newMaxHP);
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

    /// <summary>시너지 보너스를 재설정합니다. (SynergyEffectManager가 장착 변경 시 호출)</summary>
    public void SetSynergyBonus(float atkAdd, float defAdd, float speedAdd, float maxHpAdd,
                                float bleedAdd = 0f, float critAdd = 0f)
    {
        _synergyAtk   = atkAdd;
        _synergyDef   = defAdd;
        _synergySpeed = speedAdd;
        _synergyBleed = bleedAdd;
        _synergyCrit  = critAdd;

        float delta = maxHpAdd - _synergyMaxHp;
        _synergyMaxHp = maxHpAdd;
        if (!Mathf.Approximately(delta, 0f))
        {
            maxHP     = Mathf.Max(1f, maxHP + delta);
            currentHP = Mathf.Min(currentHP, maxHP);
            SyncHPUI();
        }
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
        float newScale = Mathf.Min(transform.localScale.x * scaleMultiplier, maxScale);
        transform.localScale = Vector3.one * newScale;
        maxHP     += hpBonus;
        currentHP  = Mathf.Min(currentHP + hpBonus, maxHP);
        SyncHPUI();
    }

    /// <summary>몸집을 scaleMultiplier배 줄이고 최대 체력 hpPenalty만큼 감소.</summary>
    public void DecreaseSizeHP(float scaleMultiplier, float hpPenalty)
    {
        float newScale = Mathf.Max(transform.localScale.x / scaleMultiplier, minScale);
        transform.localScale = Vector3.one * newScale;
        // 한 번 적용 시 maxHP가 현재의 20% 미만으로 떨어지지 않도록 하한선 적용
        // (예: maxHP=50, penalty=50 → 기존 로직이면 maxHP=1 → currentHP=1 버그 발생)
        float newMaxHP = Mathf.Max(maxHP * 0.2f, maxHP - hpPenalty);
        maxHP     = Mathf.Max(1f, newMaxHP);
        currentHP = Mathf.Min(currentHP, maxHP);
        SyncHPUI();
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

    private IEnumerator HitInvincibleRoutine(float duration)
    {
        isInvincible = true;
        yield return new WaitForSeconds(duration);
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

    /// <summary>GameManager.Revive()에서 호출 — HP를 가득 채우고 3초 무적 부여</summary>
    public void Revive()
    {
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
                using var effect  = veClass.CallStatic<AndroidJavaObject>("createOneShot", 40L, 180);
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
