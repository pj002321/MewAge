using System.Collections;
using UnityEngine;

/// <summary>
/// 듀크냥 캐릭터 고유 스킬. (SKL_DUKE)
///
/// [상시] 박쥐 2마리가 플레이어 좌우를 따라다닙니다.
/// [발동] 웨이브 진행 시간 동안 박쥐들이 적에게 이동해
///        총 공격력 1/4을 3회 히트 후, 다음 적으로 이동합니다.
///        3타를 입은 적은 3초간 보라색 실루엣 + 총 공격력 1/10 DoT (1초 간격).
/// </summary>
public class SkillDuke : CharacterSkillBase
{
    [Header("박쥐 프리팹")]
    [SerializeField] private GameObject batPrefab;

    [Header("박쥐 이동")]
    [SerializeField] private float moveSpeed = 14f;

    [Header("박쥐 전투")]
    [SerializeField] private float damageMult    = 0.25f;
    [SerializeField] private float dotDamageMult = 0.1f;
    [SerializeField] private float dotDuration   = 3f;

    public override string SkillId => "SKL_DUKE";
    public override float  Duration => Mathf.Infinity;

    // 궤도 설정 (인스펙터 불필요)
    private const float OrbitRadius = 1.0f;
    private const float OrbitSpeed  = 2.0f; // rad/s
    private static readonly float[] BatStartAngles = { 0f, Mathf.PI }; // 두 박쥐 반대편 시작

    private DukeBat[] _bats;
    private Coroutine _lifetimeRoutine;

    // ── 생명주기 ──────────────────────────────────────────────────────

    private void Start()
    {
        SpawnBats();
    }

    private void OnDestroy()
    {
        if (_bats == null) return;
        foreach (var bat in _bats)
            if (bat != null) Destroy(bat.gameObject);
    }

    // ── 박쥐 소환 ─────────────────────────────────────────────────────

    private void SpawnBats()
    {
        if (_bats != null) return;

        var player = PlayerController.Instance;
        if (player == null) return;

        if (batPrefab == null)
        {
            this.DError("박쥐 프리팹이 인스펙터에 할당되지 않았습니다.");
            return;
        }

        _bats = new DukeBat[BatStartAngles.Length];
        for (int i = 0; i < BatStartAngles.Length; i++)
        {
            float startAngle = BatStartAngles[i];
            var spawnPos = (Vector2)player.transform.position
                + new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle)) * OrbitRadius;
            var go  = Instantiate(batPrefab, spawnPos, Quaternion.identity, transform);
            var bat = go.GetComponent<DukeBat>() ?? go.AddComponent<DukeBat>();
            bat.InitFollow(player.transform, startAngle, OrbitRadius, OrbitSpeed, moveSpeed);
            _bats[i] = bat;
        }
    }

    // ── 발동 / 종료 ───────────────────────────────────────────────────

    public override void Activate()
    {
        if (_bats == null) return;

        float dmgMult = damageMult / DukeBat.PecksPerCombo;

        foreach (var bat in _bats)
            if (bat != null)
                bat.StartAttacking(dmgMult, dotDamageMult, dotDuration);

        if (_lifetimeRoutine != null) StopCoroutine(_lifetimeRoutine);
        _lifetimeRoutine = StartCoroutine(WaveTimeLifetime(Mathf.Max(duration, 3f)));
    }

    public override void Deactivate()
    {
        if (_lifetimeRoutine != null) { StopCoroutine(_lifetimeRoutine); _lifetimeRoutine = null; }
        if (_bats == null) return;
        foreach (var bat in _bats)
            if (bat != null) bat.StopAttacking();
    }

    // ── 웨이브 시간 전용 카운트다운 ───────────────────────────────────

    private IEnumerator WaveTimeLifetime(float waveDuration)
    {
        float elapsed = 0f;
        while (elapsed < waveDuration)
        {
            yield return null;
            if (GameManager.Shared != null && GameManager.Shared.IsWaveActive)
                elapsed += Time.deltaTime;
        }
        _lifetimeRoutine = null;
        CharacterSkillManager.Shared?.ForceStop();
    }
}
