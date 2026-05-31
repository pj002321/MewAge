using System.Collections;
using UnityEngine;

/// <summary>
/// Pullback → Striking → Overshooting → Returning 상태머신을 공유하는
/// Melee / Boomerang / Charge / Magic(None) 의 공통 추상 기반.
/// </summary>
[System.Serializable]
public abstract class WeaponMeleeBaseBehaviour : WeaponBehaviourBase
{
    // ── 수치 (코드에서 통일 관리, Override 미사용 시 이 기본값 적용) ─────────
    protected float pullbackDist      = 0.8f;
    protected float pullbackSpeed     = 5f;
    protected float strikeSpeed       = 300f;
    protected float returnSpeed       = 70f;
    protected float hitDistance       = 0.4f;
    protected float overshootDistance = 0.5f;
    protected float overshootSpeed    = 60f;

    [Header("속도 오버라이드")]
    [SerializeField] private bool  _overrideSpeed          = false;
    [SerializeField] private float _overridePullbackSpeed  = 5f;
    [SerializeField] private float _overrideStrikeSpeed    = 300f;
    [SerializeField] private float _overrideReturnSpeed    = 70f;

    // ── 런타임 상태 ──────────────────────────────────────────────────────
    protected enum State { Idle, Pullback, Striking, Overshooting, Returning }
    protected State _state = State.Idle;

    protected WeaponPoint.CombatTarget _target;
    protected Vector3 _pullbackLocalPos;
    protected Vector3 _overshootTarget;

    private float _stateTimer;
    private float _returningTimer;
    private Vector2 _attackLookDir;

    private const float MaxStateTime     = 4f;
    private const float MaxReturnTime    = 3f;
    private const float HitScaleAmount   = 1.15f;
    private const float HitScaleDuration = 0.15f;

    [System.NonSerialized] private Vector3   _restScale;
    [System.NonSerialized] private Coroutine _hitScaleCo;

    // ── WeaponBehaviourBase override ─────────────────────────────────────

    public override void OnStart()
    {
        base.OnStart();
        _restScale = Host.transform.localScale;
        if (_overrideSpeed)
        {
            pullbackSpeed = _overridePullbackSpeed;
            strikeSpeed   = _overrideStrikeSpeed;
            returnSpeed   = _overrideReturnSpeed;
        }
    }

    public override bool IsManagingLocalPos => _inWaveIntro || _state != State.Idle;

    public override Vector2? GetAttackLookDir()
    {
        // 인트로 또는 풀백 중에는 앵커 방향(바깥쪽)을 바라봄
        if (_inWaveIntro || _state == State.Pullback)
            return AnchorDir();

        if (_state == State.Idle) return null;

        // Striking 구간에서 타겟 방향으로 전환
        if (_state == State.Striking)
        {
            var active = GetActiveTarget();
            if (active.IsValid)
            {
                Vector2 dir = ((Vector2)active.Position - (Vector2)Host.transform.position).normalized;
                if (dir.sqrMagnitude > 0.001f) _attackLookDir = dir;
            }
        }
        return _attackLookDir.sqrMagnitude > 0.001f ? _attackLookDir : (Vector2?)null;
    }

    public override void OnUpdate()
    {
        HandleTimeout();

        switch (_state)
        {
            case State.Idle:         OnUpdateIdle();       break;
            case State.Pullback:     UpdatePullback();     break;
            case State.Striking:     UpdateStriking();     break;
            case State.Overshooting: UpdateOvershooting(); break;
            case State.Returning:    UpdateReturning();    break;
        }
    }

    // ── 서브클래스 훅 ────────────────────────────────────────────────────

    /// <summary>Idle 상태에서 공격 대상을 찾아 공격을 시작합니다.</summary>
    protected virtual void OnUpdateIdle()
    {
        if (Host.cooldownTimer > 0f) return;
        if (PlayerController.Instance != null && PlayerController.Instance.InputLocked) return;

        var best = Host.FindBestTarget();
        if (best.IsNone) return;

        BeginAttack(best);
    }

    /// <summary>공격 시작 시 Pullback 진입 전 처리 (Melee/Boomerang/Charge/Magic이 오버라이드).</summary>
    protected abstract void BeginAttack(WeaponPoint.CombatTarget best);

    /// <summary>Striking 상태에서 무기가 타겟에 닿았을 때 호출 (타격 판정 위임).</summary>
    protected abstract void OnStrikeHit(WeaponPoint.CombatTarget target, Vector2 hitDir);

    /// <summary>BeginStrike() 직전 추가 처리 (애니메이션 트리거 등). 기본값: 애니메이션 트리거.</summary>
    protected virtual void OnBeginStrikeSetup() => Host.TriggerAttackAnimation();

    /// <summary>BeginReturn(resetCooldown=true) 이후 추가 처리 (당기기 등).</summary>
    protected virtual void OnReturnBegin() { }

    /// <summary>현재 Striking 목표 (Boomerang은 _boomerang을 반환하도록 오버라이드).</summary>
    protected virtual WeaponPoint.CombatTarget GetActiveTarget() => _target;

    // ── 상태머신 공통 ────────────────────────────────────────────────────

    public override void OnWaveStart()
    {
        if (_state != State.Idle) return;
        base.OnWaveStart();
    }

    protected void StartPullback(WeaponPoint.CombatTarget target, Vector3 pullbackLocalPos)
    {
        CancelIntro();

        _target           = target;
        _pullbackLocalPos = pullbackLocalPos;
        _state            = State.Pullback;
    }

    protected void BeginStrike()
    {
        SoundManager.Shared?.Play(SoundKey.Weapon_Shoot);
        OnBeginStrikeSetup();
        _state = State.Striking;
        Host.StartTrail();
    }

    protected void BeginReturn(bool resetCooldown = true)
    {
        _state       = State.Returning;
        _stateTimer  = 0f;
        Host.StopTrail();
        if (resetCooldown)
        {
            Host.cooldownTimer = Host.attackCooldown;
            OnReturnBegin();
        }
    }

    private void UpdatePullback()
    {
        if (!GetActiveTarget().IsValid)
        {
            _target = WeaponPoint.CombatTarget.None;
            BeginReturn(false);
            return;
        }
        Host.transform.localPosition = Vector3.MoveTowards(
            Host.transform.localPosition, _pullbackLocalPos, pullbackSpeed * Time.deltaTime);

        if ((Host.transform.localPosition - _pullbackLocalPos).sqrMagnitude <= 0.001f)
        {
            Host.transform.localPosition = _pullbackLocalPos;
            BeginStrike();
        }
    }

    protected virtual void UpdateStriking()
    {
        if (!_target.IsValid)
        {
            _target = WeaponPoint.CombatTarget.None;
            BeginReturn(false);
            return;
        }

        Vector3 targetPos = _target.Position;
        if (_target.Boss != null &&
            ((Vector2)targetPos - (Vector2)Host.transform.parent.position).sqrMagnitude
            > Host.attackRange * Host.attackRange * 4f)
        {
            _target = WeaponPoint.CombatTarget.None;
            BeginReturn(false);
            return;
        }

        Host.transform.position = Vector3.MoveTowards(
            Host.transform.position, targetPos, strikeSpeed * Time.deltaTime);

        if (Vector3.Distance(Host.transform.position, targetPos) <= hitDistance)
        {
            Vector2 hitDir = ((Vector2)targetPos - (Vector2)Host.transform.parent.position).normalized;
            OnStrikeHit(_target, hitDir);
            _overshootTarget = targetPos + (Vector3)(hitDir * overshootDistance);
            _target = WeaponPoint.CombatTarget.None;
            EnterOvershooting();
        }
    }

    protected void EnterOvershooting()
    {
        _state = State.Overshooting;
        if (_hitScaleCo != null) StopCoroutine(_hitScaleCo);
        _hitScaleCo = StartCoroutine(HitScalePunchRoutine());
    }

    private IEnumerator HitScalePunchRoutine()
    {
        var tf   = Host.transform;
        var orig = _restScale.sqrMagnitude > 0.001f ? _restScale : tf.localScale;
        tf.localScale = orig * HitScaleAmount;

        for (float t = 0f; t < HitScaleDuration; t += Time.deltaTime)
        {
            tf.localScale = Vector3.LerpUnclamped(orig * HitScaleAmount, orig, t / HitScaleDuration);
            yield return null;
        }
        tf.localScale = orig;
        _hitScaleCo   = null;
    }

    protected virtual void UpdateOvershooting()
    {
        Host.transform.position = Vector3.MoveTowards(
            Host.transform.position, _overshootTarget, overshootSpeed * Time.deltaTime);
        if (Vector3.Distance(Host.transform.position, _overshootTarget) <= 0.05f)
            BeginReturn();
    }

    private void UpdateReturning()
    {
        Host.transform.localPosition = Vector3.MoveTowards(
            Host.transform.localPosition, Vector3.zero, returnSpeed * Time.deltaTime);

        OnReturnUpdate();

        if (Host.transform.localPosition.sqrMagnitude <= 0.001f)
        {
            Host.transform.localPosition = Vector3.zero;
            _state = State.Idle;
        }
    }

    protected virtual void OnReturnUpdate() { }

    // ── 미아 방지 타임아웃 ───────────────────────────────────────────────
    private void HandleTimeout()
    {
        if (_state != State.Idle && _state != State.Returning)
        {
            _stateTimer += Time.unscaledDeltaTime;
            if (_stateTimer >= MaxStateTime)
            {
                _target = WeaponPoint.CombatTarget.None;
                Host.transform.localPosition = Vector3.zero;
                Host.transform.rotation      = Quaternion.identity;
                if (_hitScaleCo != null) { StopCoroutine(_hitScaleCo); _hitScaleCo = null; }
                if (_restScale.sqrMagnitude > 0.001f) Host.transform.localScale = _restScale;
                RestoreWeaponAlpha();
                Host.cooldownTimer = Host.attackCooldown;
                _state             = State.Idle;
                _stateTimer        = 0f;
                _returningTimer    = 0f;
                return;
            }
        }
        else { _stateTimer = 0f; }

        if (_state == State.Returning)
        {
            _returningTimer += Time.unscaledDeltaTime;
            if (_returningTimer >= MaxReturnTime)
            {
                Host.transform.localPosition = Vector3.zero;
                _state          = State.Idle;
                _returningTimer = 0f;
            }
        }
        else { _returningTimer = 0f; }
    }

    private void RestoreWeaponAlpha()
    {
        var sr = Host.WeaponSr;
        if (sr != null) { var c = sr.color; c.a = 1f; sr.color = c; }
    }
}
