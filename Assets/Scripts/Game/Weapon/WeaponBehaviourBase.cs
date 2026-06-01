using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// WeaponPoint에 [SerializeReference]로 인라인 직렬화되는 무기 행동 기반 추상 클래스.
/// Inspector 드롭다운으로 타입을 선택해 하나의 WeaponPoint 컴포넌트에서 모든 타입을 설정합니다.
/// </summary>
[System.Serializable]
public abstract class WeaponBehaviourBase
{
    protected WeaponPoint Host { get; private set; }

    // ── 웨이브 인트로 (공통) ──────────────────────────────────────────────
    protected bool      _inWaveIntro;
    private   Coroutine _introCo;

    /// <summary>WeaponPoint.Init(data) 호출 시 주입됩니다.</summary>
    public virtual void Init(WeaponPoint host) => Host = host;

    /// <summary>DB 무기 데이터 적용 후 타입별 추가 초기화. WeaponPoint.Init() 내에서 호출됩니다.</summary>
    public virtual void OnInitData(WeaponData data) { }

    /// <summary>WeaponPoint.Start() 에서 호출됩니다.</summary>
    public virtual void OnStart()
    {
        WeaponPoint.OnWaveStarted += OnWaveStart;

        if (GameManager.Shared != null && GameManager.Shared.CurrentWave == 0)
        {
            Host.transform.localPosition = (Vector3)AnchorDir() * IntroOffset;
        }
    }

    /// <summary>WeaponPoint.Update() 에서 호출됩니다.</summary>
    public abstract void OnUpdate();

    /// <summary>WeaponPoint.OnDestroy() 에서 호출됩니다.</summary>
    public virtual void OnDestroy()
    {
        WeaponPoint.OnWaveStarted -= OnWaveStart;
    }

    /// <summary>true면 WeaponPoint의 UpdateRotation()을 이번 프레임 스킵합니다.</summary>
    public virtual bool OverridesRotation => false;

    /// <summary>true이면 이 프레임에 localPosition을 직접 제어 중이므로 BonusOrbitBehaviour를 멈춥니다.</summary>
    public virtual bool IsManagingLocalPos => _inWaveIntro;

    /// <summary>공격 시퀀스 중 유지할 시선 방향을 반환합니다. null이면 WeaponPoint가 자체 계산합니다.</summary>
    public virtual Vector2? GetAttackLookDir() => null;

    /// <summary>WeaponPoint.OnTriggerStay2D에서 전달됩니다 (Spin 타입 전용).</summary>
    public virtual void OnTriggerHit(Collider2D col) { }

    /// <summary>플레이어 공격력 보너스 중 이 무기 타입에 해당하는 값.</summary>
    public virtual float GetPlayerAttackBonus(PlayerController player) => 0f;

    /// <summary>유물(Relic) 공격 보너스 퍼센트. 0이면 적용 안 함.</summary>
    public virtual float GetRelicBonusPct(RelicManager relic) => 0f;

    /// <summary>클론·서브 투사체 생성에 사용할 프리팹. 투사체가 없는 타입은 null 반환.</summary>
    public virtual GameObject GetProjectilePrefab() => null;

    /// <summary>투사체 속도. 투사체가 없는 타입은 0 반환.</summary>
    public virtual float GetProjectileSpeed() => 0f;

    /// <summary>웨이브 시작 시 입장 연출. 서브클래스에서 조건 추가 후 base 호출.</summary>
    public virtual void OnWaveStart()
    {
        if (_introCo != null) StopCoroutine(_introCo);
        _introCo = StartCoroutine(WaveIntroRoutine());
    }

    // ── 인트로 헬퍼 (서브클래스 공용) ────────────────────────────────────

    protected Vector2 AnchorDir()
    {
        Vector3 lp = Host.transform.parent.localPosition;
        return lp.sqrMagnitude > 0.001f ? ((Vector2)lp).normalized : Vector2.up;
    }

    protected void CancelIntro()
    {
        if (_introCo != null) { StopCoroutine(_introCo); _introCo = null; }
        _inWaveIntro = false;
        Host.transform.localPosition = Vector3.zero;
    }

    private const float IntroOffset   = 2.0f;
    public  const float IntroDuration = 1.0f; // GameManager의 WaveIntroWaitRoutine 대기 시간과 일치

    private IEnumerator WaveIntroRoutine()
    {
        _inWaveIntro = true;

        Host.transform.localPosition = (Vector3)AnchorDir() * IntroOffset;

        var tween = Host.transform.DOLocalMove(Vector3.zero, IntroDuration)
            .SetEase(Ease.OutBack, 1.4f)
            .SetLink(Host.gameObject);

        yield return tween.WaitForCompletion();

        Host.transform.localPosition = Vector3.zero;
        _inWaveIntro = false;
        _introCo     = null;
    }

    // ── 코루틴 헬퍼 (Host.StartCoroutine 위임) ──────────────────────────
    protected Coroutine StartCoroutine(IEnumerator routine) => Host.StartCoroutine(routine);
    protected void StopCoroutine(Coroutine co) { if (co != null) Host.StopCoroutine(co); }

#if UNITY_EDITOR
    /// <summary>WeaponPoint.OnDrawGizmosSelected()에서 호출됩니다. 타입별 기즈모를 추가로 그립니다.</summary>
    public virtual void DrawGizmos(Vector3 playerCenter, Vector3 weaponPos) { }
#endif
}
