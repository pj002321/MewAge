using System.Collections;
using UnityEngine;

/// <summary>
/// 듀크냥 캐릭터 고유 스킬. (SKL_DUKE)
///
/// [상시] 박쥐 2마리가 anchors 위치(플레이어 좌우)를 집으로 삼아 따라다닙니다.
/// [발동] 박쥐들이 적을 자율적으로 공격합니다.
///        웨이브 활성 시간(IsWaveActive)만 카운트 — 웨이브 사이 시간은 차감되지 않음.
/// </summary>
public class SkillDuke : CharacterSkillBase
{
    [Header("박쥐 프리팹")]
    [SerializeField] private GameObject batPrefab;

    [Header("박쥐 집 앵커 (Inspector에서 자식 Transform 연결)")]
    [SerializeField] private Transform[] anchors;

    [Header("박쥐 이동")]
    [SerializeField] private float moveSpeed = 14f;

    [Header("박쥐 전투")]
    [SerializeField] private float damageMult    = 0.25f;
    [SerializeField] private float skillDuration = 10f;   // 웨이브 활성 시간 기준

    public override string SkillId => "SKL_DUKE";
    public override float  Duration => Mathf.Infinity;   // 내부 WaveTimeLifetime이 관리

    private DukeBat[]  _bats;
    private bool       _isPlayerInstance;
    private Coroutine  _lifetimeRoutine;

    // ── 라이프사이클 ──────────────────────────────────────────────────

    private void Start()
    {
        var player = PlayerController.Instance;
        if (player == null || !transform.IsChildOf(player.transform)) return;

        _isPlayerInstance = true;
        SpawnBats();
    }

    // ── 박쥐 소환 ─────────────────────────────────────────────────────

    private void SpawnBats()
    {
        if (_bats != null) return;
        if (batPrefab == null) { this.DError("batPrefab이 없습니다."); return; }
        if (anchors == null || anchors.Length == 0) { this.DError("anchors가 설정되지 않았습니다."); return; }

        var player = PlayerController.Instance;
        if (player == null) return;

        int count = Mathf.Min(anchors.Length, 2);
        _bats = new DukeBat[count];

        for (int i = 0; i < count; i++)
        {
            var spawnPos = anchors[i] != null ? (Vector3)anchors[i].position : player.transform.position;
            var go = Instantiate(batPrefab, spawnPos, Quaternion.identity);
            _bats[i] = go.GetComponent<DukeBat>() ?? go.AddComponent<DukeBat>();
        }

        for (int i = 0; i < count; i++)
        {
            int other = (i + 1) % count;
            _bats[i].Init(anchors[i], moveSpeed, () => _bats[other]?.CurrentTarget, i * Mathf.PI);
        }
    }

    private void OnDestroy()
    {
        if (!_isPlayerInstance || _bats == null) return;
        foreach (var bat in _bats)
            if (bat != null) Destroy(bat.gameObject);
    }

    // ── 발동 / 종료 ───────────────────────────────────────────────────

    public override void Activate()
    {
        if (!_isPlayerInstance || _bats == null) return;
        float dmgMult = damageMult / DukeBat.HitsPerPeck;
        foreach (var bat in _bats)
            if (bat != null) bat.StartAttacking(dmgMult);

        if (_lifetimeRoutine != null) StopCoroutine(_lifetimeRoutine);
        _lifetimeRoutine = StartCoroutine(WaveTimeLifetime(skillDuration));
    }

    public override void Deactivate()
    {
        if (_lifetimeRoutine != null) { StopCoroutine(_lifetimeRoutine); _lifetimeRoutine = null; }
        if (!_isPlayerInstance || _bats == null) return;
        foreach (var bat in _bats)
            if (bat != null) bat.StopAttacking();
    }

    // ── 웨이브 시간 카운트다운 ────────────────────────────────────────

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
