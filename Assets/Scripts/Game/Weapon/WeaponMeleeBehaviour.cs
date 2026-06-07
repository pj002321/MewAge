using UnityEngine;

/// <summary>
/// 근거리(Melee) 타격 무기 행동.
/// Pullback(느리게 수축) → 적 머리로 고속 직선 타격 → Embedded(꽂혀서 정지) → Returning.
/// 돌진 도중(HitArea)과 귀환 시작 시점 모두 범위 내 적에게 피해를 줍니다.
/// </summary>
[System.Serializable]
public class WeaponMeleeBehaviour : WeaponMeleeBaseBehaviour
{
    public override float GetPlayerAttackBonus(PlayerController player) => player.MeleeAttackBonus;
    public override float GetRelicBonusPct(RelicManager relic)         => relic.MeleeAtkBonusPct;

    [Header("박힘 대기")]
    [SerializeField] private float embedDuration  = 0.2f;
    [SerializeField] private bool  useFadeWarp    = false;
    [SerializeField] private bool  useEmbedPulse  = false;

    private const float EmbedPulseInterval = 0.06f;
    private const float EmbedPulseFadeDur  = 0.18f;

    // 타격 목표 (적 머리) — BeginStrikeSetup에서 고정
    private Vector3 _headTarget;
    private bool    _headTargetReady;
    private Vector2 _toTarget;
    private Vector2 _lastHitDir;

    // 임베드 타이머 & 귀환 타격 여부
    private float     _embedTimer;
    private bool      _returnHitFired;
    private bool      _warping;
    private bool      _embedStarted;
    private Coroutine _embedPulseCo;

    protected override void BeginAttack(WeaponPoint.CombatTarget best)
    {
        overshootDistance = 0.3f;
        _target          = best;
        _toTarget        = ((Vector2)best.Position - (Vector2)Host.transform.parent.position).normalized;
        if (_toTarget.sqrMagnitude < 0.001f) _toTarget = Vector2.up;

        _headTargetReady = false;
        _returnHitFired  = false;
        _embedStarted    = false;
        StopEmbedPulse();
        StartPullback(best, -(Vector3)_toTarget * pullbackDist);
    }

    // 풀백 완료 시 타격 목표(적 머리)를 확정
    protected override void OnBeginStrikeSetup()
    {
        base.OnBeginStrikeSetup();
        _headTarget      = GetEnemyHeadPosition();
        _headTargetReady = true;
    }

    // 적 머리를 향해 직선 고속 타격 — 명중 시 임베드 대기 진입
    protected override void UpdateStriking()
    {
        if (!_target.IsValid)
        {
            _target          = WeaponPoint.CombatTarget.None;
            _headTargetReady = false;
            BeginReturn(false);
            return;
        }

        // 보스가 사정거리 초과하면 취소
        if (_target.Boss != null &&
            ((Vector2)_target.Position - (Vector2)Host.transform.parent.position).sqrMagnitude
            > Host.attackRange * Host.attackRange * 4f)
        {
            _target          = WeaponPoint.CombatTarget.None;
            _headTargetReady = false;
            BeginReturn(false);
            return;
        }

        Vector3 dest = _headTargetReady ? _headTarget : _target.Position;

        Host.transform.position = Vector3.MoveTowards(
            Host.transform.position, dest, strikeSpeed * Time.deltaTime);

        if (Vector3.Distance(Host.transform.position, dest) <= hitDistance)
        {
            Vector2 hitDir = ((Vector2)(dest - (Vector3)Host.transform.parent.position)).normalized;
            if (hitDir.sqrMagnitude < 0.001f) hitDir = _toTarget;
            _lastHitDir = hitDir;

            OnStrikeHit(_target, hitDir);
            _overshootTarget = dest + (Vector3)(hitDir * overshootDistance);
            _target          = WeaponPoint.CombatTarget.None;
            _headTargetReady = false;
            _embedTimer      = embedDuration;
            EnterOvershooting();
        }
    }

    // 오버슈트 목표까지 이동 → 도달 후 임베드 대기 → 페이드아웃 후 원위치 워프
    protected override void UpdateOvershooting()
    {
        if (_warping) return;

        if (Vector3.Distance(Host.transform.position, _overshootTarget) > 0.05f)
        {
            Host.transform.position = Vector3.MoveTowards(
                Host.transform.position, _overshootTarget, strikeSpeed * Time.deltaTime);
            return;
        }
        if (!_embedStarted)
        {
            _embedStarted = true;
            if (useEmbedPulse) _embedPulseCo = StartCoroutine(EmbedPulseRoutine());
        }

        _embedTimer -= Time.deltaTime;
        if (_embedTimer <= 0f)
        {
            StopEmbedPulse();
            if (useFadeWarp) { _warping = true; StartCoroutine(FadeWarpRoutine()); }
            else BeginReturn();
        }
    }

    private System.Collections.IEnumerator FadeWarpRoutine()
    {
        const float popInDur     = 0.12f;
        const float ghostHoldDur = 0.45f;
        const float ghostFadeDur = 0.22f;

        var sr = Host.WeaponSr;

        // 박힌 자리에 고스트 생성 — 꽂혀있는 채로 유지
        if (sr != null && sr.sprite != null)
        {
            var ghost              = new GameObject("EmbedGhost");
            ghost.transform.position   = Host.transform.position;
            ghost.transform.rotation   = Host.transform.rotation;
            ghost.transform.localScale = Host.transform.lossyScale;
            var ghostSr            = ghost.AddComponent<SpriteRenderer>();
            ghostSr.sprite         = sr.sprite;
            ghostSr.color          = sr.color;
            ghostSr.sortingLayerID = sr.sortingLayerID;
            ghostSr.sortingOrder   = sr.sortingOrder;
            StartCoroutine(FadeAndDestroyGhost(ghost, ghostSr, ghostHoldDur, ghostFadeDur));
        }

        // 실제 무기는 숨기고 즉시 원위치로 이동
        if (sr != null) { var c = sr.color; c.a = 0f; sr.color = c; }
        Host.transform.localPosition = Vector3.zero;
        Host.StopTrail();

        // 새 무기처럼 페이드인
        for (float t = 0f; t < popInDur; t += Time.deltaTime)
        {
            if (sr != null) { var c = sr.color; c.a = t / popInDur; sr.color = c; }
            yield return null;
        }
        if (sr != null) { var c = sr.color; c.a = 1f; sr.color = c; }

        Host.cooldownTimer = Host.attackCooldown;
        _returnHitFired    = false;
        _warping           = false;
        _state             = State.Idle;
    }

    private System.Collections.IEnumerator FadeAndDestroyGhost(
        GameObject go, SpriteRenderer ghostSr, float holdDur, float fadeDur)
    {
        yield return new WaitForSeconds(holdDur);
        float startAlpha = ghostSr != null ? ghostSr.color.a : 1f;
        for (float t = 0f; t < fadeDur; t += Time.deltaTime)
        {
            if (ghostSr == null) yield break;
            var c = ghostSr.color;
            c.a = Mathf.Lerp(startAlpha, 0f, t / fadeDur);
            ghostSr.color = c;
            yield return null;
        }
        if (go != null) Object.Destroy(go);
    }

    // 귀환 시작 시 범위 내 적에게 1회 타격
    protected override void OnReturnUpdate()
    {
        if (_returnHitFired) return;
        _returnHitFired = true;

        Vector2 returnDir = ((Vector2)Host.transform.parent.position
                            - (Vector2)Host.transform.position).normalized;
        Host.HitArea(returnDir, null);
    }

    protected override float HitScaleHoldDuration => embedDuration;

    protected override void OnStrikeHit(WeaponPoint.CombatTarget target, Vector2 hitDir)
        => Host.HitArea(hitDir, target.Enemy);

    public override Vector2? GetAttackLookDir()
    {
        if (_inWaveIntro || _state == State.Pullback) return _toTarget;
        if (_state == State.Idle)                     return null;
        if (_state == State.Striking)                 return _toTarget;
        if (_lastHitDir.sqrMagnitude > 0.001f)        return _lastHitDir;
        return _toTarget;
    }

    private void StopEmbedPulse()
    {
        if (_embedPulseCo == null) return;
        StopCoroutine(_embedPulseCo);
        _embedPulseCo = null;
    }

    private System.Collections.IEnumerator EmbedPulseRoutine()
    {
        while (true)
        {
            SpawnEmbedGhost();
            yield return new WaitForSeconds(EmbedPulseInterval);
        }
    }

    private void SpawnEmbedGhost()
    {
        var sr = Host.WeaponSr;
        if (sr == null || sr.sprite == null) return;

        Color pulseColor = Host.Grade == WeaponGrade.Common
            ? Color.white : WeaponGradeColors.Get(Host.Grade);
        pulseColor.a = 0.7f;

        var go = new GameObject("EmbedGhost");
        go.transform.position   = Host.transform.position;
        go.transform.rotation   = Host.transform.rotation;
        go.transform.localScale = Host.transform.lossyScale;

        var ghostSr            = go.AddComponent<SpriteRenderer>();
        ghostSr.sprite         = sr.sprite;
        ghostSr.color          = pulseColor;
        ghostSr.sortingLayerID = sr.sortingLayerID;
        ghostSr.sortingOrder   = sr.sortingOrder - 1;

        StartCoroutine(FadeOutGhost(go, ghostSr, Host.transform.lossyScale));
    }

    private System.Collections.IEnumerator FadeOutGhost(GameObject go, SpriteRenderer ghostSr, Vector3 startScale)
    {
        const float endScaleMult = 3.5f;
        float startAlpha = ghostSr.color.a;
        for (float t = 0f; t < EmbedPulseFadeDur; t += Time.deltaTime)
        {
            if (ghostSr == null) yield break;
            float p = t / EmbedPulseFadeDur;
            var c = ghostSr.color;
            c.a = Mathf.Lerp(startAlpha, 0f, p);
            ghostSr.color    = c;
            go.transform.localScale = Vector3.Lerp(startScale, startScale * endScaleMult, p);
            yield return null;
        }
        Object.Destroy(go);
    }

    // 적 콜라이더 상단(머리) 좌표
    private Vector3 GetEnemyHeadPosition()
    {
        if (_target.Enemy != null)
        {
            var col = _target.Enemy.GetComponentInChildren<Collider2D>();
            if (col != null)
                return new Vector3(col.bounds.center.x, col.bounds.max.y, 0f);
            return (Vector3)_target.Enemy.transform.position + Vector3.up * 0.4f;
        }
        if (_target.Boss != null)
        {
            var col = _target.Boss.GetComponentInChildren<Collider2D>();
            if (col != null)
                return new Vector3(col.bounds.center.x, col.bounds.max.y, 0f);
            return (Vector3)_target.Boss.transform.position + Vector3.up * 0.8f;
        }
        return _target.Position;
    }
}
