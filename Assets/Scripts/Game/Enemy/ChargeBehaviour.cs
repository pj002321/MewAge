using UnityEngine;
/// <summary>
/// 돌진형 — 2초 이동 → 2초 정지(Idle) → 돌진 → 반복
/// 접촉 피해는 EnemyController의 OnTrigger에서 처리됩니다.
/// </summary>
public class ChargeBehaviour : EnemyBehaviourBase
{
    [Header("돌진 설정")]
    [SerializeField] private float chargeDistance        = 6f;
    [SerializeField] private float chargeSpeedMultiplier = 3.5f;

    [Header("이동/정지 사이클")]
    [SerializeField] private float moveDuration  = 2f;
    [SerializeField] private float idleDuration  = 2f;
    [Tooltip("이 거리 이하에 도달하면 이동 타이머 무관하게 즉시 Idle(돌진 준비)로 전환")]
    [SerializeField] private float chargeRange   = 4f;

    [Header("돌진 준비 시각")]
    [SerializeField] private Color chargeColor   = Color.red;
    [SerializeField] private float chargeScale   = 0.85f;
    [SerializeField] private float ghostInterval = 0.06f;

    [Header("경고 라인 (Idle 페이즈)")]
    [SerializeField] private float    warningLineLength = 5f;
    [SerializeField] private float    warningLineWidth  = 0.06f;
    [SerializeField] private Color    warningColor      = Color.red;
    [Tooltip("Sprites/Default 또는 URP Unlit 머티리얼 할당. null이면 자동 생성")]
    [SerializeField] private Material warningMaterial;
    [Tooltip("경고 라인 시작점. 미할당 시 적 중심에서 발사")]
    [SerializeField] private Transform warningLineOrigin;
    [SerializeField] private bool      showWarningLine = true;

    private enum Phase { Moving, Idle, Charging }
    private Phase   _phase;
    private float   _phaseTimer;

    private float           chargeRemaining;
    private Vector2         chargeDir;
    private Vector2         _lockedChargeDir;
    private float           ghostTimer;
    private Vector3         originalScale;
    private SpriteRenderer  _cachedSr;
    private LineRenderer    _warnLine;
    private float           _origSepStrength;

    public override void Init(IEnemyHost host)
    {
        base.Init(host);
        _phase      = Phase.Moving;
        _phaseTimer = moveDuration;
        _cachedSr   = Owner.SelfTransform.GetComponent<SpriteRenderer>();
        DestroyWarningLine();
        if (EC != null) EC.KnockbackImmune = true;
    }

    private void OnDisable() => DestroyWarningLine();

    public override void OnAttackUpdate()
    {
        if (Owner.PlayerTransform == null || _phase == Phase.Charging) return;

        _phaseTimer -= Time.deltaTime;

        if (_phase == Phase.Moving)
        {
            float dist = Vector2.Distance(Owner.SelfTransform.position, Owner.PlayerTransform.position);
            if (_phaseTimer <= 0f || dist <= chargeRange)
                BeginIdle();
        }
        else if (_phase == Phase.Idle)
        {
            float t = 1f - Mathf.Clamp01(_phaseTimer / idleDuration);
            if (_cachedSr != null)
                _cachedSr.color = Color.Lerp(Color.white, chargeColor, t);
            var s = Owner.SelfTransform.localScale;
            s.y = Mathf.Lerp(originalScale.y, originalScale.y * chargeScale, t);
            Owner.SelfTransform.localScale = s;

            UpdateWarningLine();
            if (_phaseTimer <= 0f)
                BeginCharge();
        }
    }

    private void BeginIdle()
    {
        _phase      = Phase.Idle;
        _phaseTimer = idleDuration;

        originalScale    = Owner.SelfTransform.localScale;
        var chargeEC     = EC;
        _origSepStrength = chargeEC != null ? chargeEC.separationStrength : 0f;
        if (chargeEC != null) chargeEC.separationStrength = 0f;

        Vector2 origin = warningLineOrigin != null
            ? (Vector2)warningLineOrigin.position
            : (Vector2)Owner.SelfTransform.position;
        _lockedChargeDir = Owner.PlayerTransform != null
            ? ((Vector2)Owner.PlayerTransform.position - origin).normalized
            : Vector2.right;

        SpawnWarningLine();
    }

    private void SpawnWarningLine()
    {
        if (_warnLine != null) return;
        if (!showWarningLine || !WarningLineSettings.Enabled) return;
        var go = new GameObject("ChargeWarnLine");
        go.transform.rotation    = Quaternion.Euler(60f, 0f, 0f);
        _warnLine = go.AddComponent<LineRenderer>();
        _warnLine.useWorldSpace  = false;
        _warnLine.positionCount  = 2;
        _warnLine.startWidth     = warningLineWidth;
        _warnLine.endWidth       = warningLineWidth;
        _warnLine.sortingOrder   = 10;
        _warnLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _warnLine.material = warningMaterial != null
            ? warningMaterial
            : new Material(Shader.Find("Sprites/Default"));
        _warnLine.startColor = warningColor;
        _warnLine.endColor   = warningColor;
        _warnLine.SetPosition(0, Vector3.zero);
        _warnLine.SetPosition(1, Vector3.zero);
        Destroy(go, idleDuration + 0.5f);
    }

    private void UpdateWarningLine()
    {
        if (_warnLine == null) return;
        float t = 1f - Mathf.Clamp01(_phaseTimer / idleDuration);
        Vector2 origin = warningLineOrigin != null
            ? (Vector2)warningLineOrigin.position
            : (Vector2)Owner.SelfTransform.position;
        var tf = _warnLine.transform;
        tf.position = new Vector3(origin.x, origin.y, 0f);
        _warnLine.SetPosition(0, Vector3.zero);
        Vector3 worldEnd = new Vector3(origin.x + _lockedChargeDir.x * warningLineLength * t,
                                       origin.y + _lockedChargeDir.y * warningLineLength * t, 0f);
        _warnLine.SetPosition(1, tf.InverseTransformPoint(worldEnd));
    }

    private void DestroyWarningLine()
    {
        if (_warnLine == null) return;
        UnityEngine.Object.Destroy(_warnLine.gameObject);
        _warnLine = null;
        if (EC != null) EC.separationStrength = _origSepStrength;
    }

    private void BeginCharge()
    {
        DestroyWarningLine();

        // 즉시 원상복구 후 돌진
        if (_cachedSr != null) _cachedSr.color = Color.white;
        var s = Owner.SelfTransform.localScale;
        s.y = originalScale.y;
        Owner.SelfTransform.localScale = s;

        _phase          = Phase.Charging;
        chargeDir       = _lockedChargeDir.sqrMagnitude > 0f
            ? _lockedChargeDir
            : ((Vector2)Owner.PlayerTransform.position - Owner.Rb.position).normalized;
        chargeRemaining = chargeDistance;
        ghostTimer      = 0f;

        Owner.EnemyAnimator?.SetTrigger(EnemyAnim.TCharge);
    }

    private void EndCharge()
    {
        var sr = _cachedSr;
        if (sr != null)
        {
            sr.color = Color.white;
            var s = Owner.SelfTransform.localScale;
            s.y = originalScale.y;
            Owner.SelfTransform.localScale = s;
        }
        _phase      = Phase.Moving;
        _phaseTimer = moveDuration;
    }

    public override void OnMoveFixedUpdate()
    {
        if (_phase == Phase.Charging)
        {
            // 고스트 트레일
            ghostTimer -= Time.fixedDeltaTime;
            if (ghostTimer <= 0f)
            {
                SpawnGhostTrail();
                ghostTimer = ghostInterval;
            }

            float chargeSpeed = Owner.MoveSpeed * chargeSpeedMultiplier;
            Owner.MoveInDirection(chargeDir, chargeSpeed);
            chargeRemaining -= chargeSpeed * Time.fixedDeltaTime;

            // 맵 경계 도달 시 돌진 중단 + 위치 클램프
            if (IsOutOfBounds(Owner.SelfTransform.position))
            {
                ClampToBounds();
                EndCharge();
            }
            else if (chargeRemaining <= 0f)
            {
                EndCharge();
            }
        }
        else if (_phase == Phase.Moving)
        {
            Owner.MoveTowardPlayer(Owner.MoveSpeed);
        }
        else if (_phase == Phase.Idle)
        {
            Owner.FlipToward(_lockedChargeDir);
        }
    }

    private bool IsOutOfBounds(Vector2 pos)
    {
        GetBounds(out Vector2 c, out Vector2 half);
        return pos.x < c.x - half.x || pos.x > c.x + half.x ||
               pos.y < c.y - half.y || pos.y > c.y + half.y;
    }

    private void ClampToBounds()
    {
        GetBounds(out Vector2 c, out Vector2 half);
        Vector3 p = Owner.SelfTransform.position;
        Owner.SelfTransform.position = new Vector3(
            Mathf.Clamp(p.x, c.x - half.x, c.x + half.x),
            Mathf.Clamp(p.y, c.y - half.y, c.y + half.y),
            p.z);
    }

    private static void GetBounds(out Vector2 center, out Vector2 half)
    {
        var spawner = EnemySpawner.Shared;
        if (spawner != null)
        {
            center = spawner.AreaCenter;
            half   = spawner.AreaSize * 0.5f;
            return;
        }

        // 테스트씬 폴백 — 카메라 뷰포트 기준
        var cam = Camera.main;
        if (cam != null)
        {
            float h = cam.orthographicSize;
            float w = h * cam.aspect;
            center = (Vector2)cam.transform.position;
            half   = new Vector2(w, h);
            return;
        }

        center = Vector2.zero;
        half   = new Vector2(20f, 12f);
    }

    // ── 고스트 트레일 ───────────────────────────────────────────
    private void SpawnGhostTrail()
    {
        var sr = _cachedSr;
        if (sr == null) return;

        var sprite = sr.sprite;
        if (sprite == null) return;

        var go = new GameObject("ChargeGhost", typeof(SpriteRenderer));
        go.transform.position   = Owner.SelfTransform.position;
        go.transform.localScale = Owner.SelfTransform.localScale;

        var ghostSr = go.GetComponent<SpriteRenderer>();
        ghostSr.sprite         = sprite;
        ghostSr.color          = new Color(1f, 1f, 1f, 0.35f);
        ghostSr.sortingLayerID = sr.sortingLayerID;
        ghostSr.sortingOrder   = sr.sortingOrder - 1;

        Destroy(go, 0.12f);
    }
}
