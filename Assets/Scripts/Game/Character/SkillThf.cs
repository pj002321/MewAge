using System.Collections;
using UnityEngine;

/// <summary>
/// 시프냥 캐릭터 고유 스킬.
///
/// [발동 즉시] 시프냥과 동일한 외형의 분신을 소환합니다.
/// 분신은 웨이브 진행 시간으로만 duration을 소모하며,
/// 빠르게 적에게 이동해 연속 히트를 반복합니다.
/// 분신이 사라진 후에만 스킬 게이지를 다시 쌓을 수 있습니다.
/// </summary>
public class SkillThf : CharacterSkillBase
{
    [Header("분신")]
    [SerializeField] private GameObject doppelgangerPrefab;

    [Header("분신 행동")]
    [SerializeField] private int   hitsPerEnemy   = 5;
    [SerializeField] private float hitInterval    = 0.07f;
    [SerializeField] private float moveSpeed      = 18f;

    [Header("분신 전투")]
    [SerializeField] private float damageMult     = 0.4f;
    [SerializeField] private float knockbackForce = 14f;

    public override string SkillId => "SKL_THF";
    public override float  Duration => Mathf.Infinity;

    private ThfDoppelganger _doppelganger;
    private Coroutine       _lifetimeRoutine;

    // ── 발동 / 종료 ───────────────────────────────────────────────────

    public override void Activate()
    {
        var player = PlayerController.Instance;
        if (player == null) return;

        if (_doppelganger != null)
            Destroy(_doppelganger.gameObject);

        var go = doppelgangerPrefab != null
            ? Instantiate(doppelgangerPrefab, player.transform.position, Quaternion.identity)
            : new GameObject("ThfDoppelganger");

        _doppelganger = go.GetComponent<ThfDoppelganger>() ?? go.AddComponent<ThfDoppelganger>();
        _doppelganger.Init(hitsPerEnemy, hitInterval, damageMult, knockbackForce, moveSpeed);

        if (_lifetimeRoutine != null) StopCoroutine(_lifetimeRoutine);
        _lifetimeRoutine = StartCoroutine(WaveTimeLifetime(duration));
    }

    public override void Deactivate()
    {
        if (_lifetimeRoutine != null) { StopCoroutine(_lifetimeRoutine); _lifetimeRoutine = null; }

        if (_doppelganger == null) return;
        Destroy(_doppelganger.gameObject);
        _doppelganger = null;
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
