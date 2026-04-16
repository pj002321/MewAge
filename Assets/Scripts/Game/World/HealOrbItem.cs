using System.Collections;
using UnityEngine;

/// <summary>
/// 체력 회복 구슬 픽업 아이템.
/// AUG_SPW_HP_SKL 증강 효과로 적 처치 시 스폰됩니다.
/// 플레이어와 접촉하면 MaxHP의 healPercent% 를 회복하고 파괴됩니다.
///
/// # Unity Setup:
///   1. 이 컴포넌트를 회복 구슬 프리팹(Resources/AUG/aug_spw_hp_skl)에 부착.
///   2. Collider2D IsTrigger = true 필요.
///   3. healPercent : 기본 20 (MaxHP의 20% 회복).
/// </summary>
public class HealOrbItem : MonoBehaviour
{
    [Header("회복량")]
    [Tooltip("MaxHP 대비 회복 비율 (%) — 기본 20%")]
    [SerializeField] private float healPercent = 20f;

    [Header("자석 이동 (DropItem 동일 방식)")]
    [SerializeField] private float magnetRadius   = 3f;
    [SerializeField] private float magnetDelay    = 0.3f;
    [SerializeField] private float attractAccel   = 30f;
    [SerializeField] private float attractMaxSpeed = 22f;

    [Header("팝인 연출")]
    [SerializeField] private float popDuration = 0.18f;

    private enum State { Idle, Attract }
    private State   _state     = State.Idle;
    private float   _timer;
    private float   _attractSpeed;
    private Vector3 _originScale;

    private void Start()
    {
        _originScale = transform.localScale;
        StartCoroutine(PopIn());
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < magnetDelay) return;

        var player = PlayerController.Instance;
        if (player == null) return;

        if (_state == State.Idle)
        {
            float dist           = Vector2.Distance(transform.position, player.Position);
            float effectiveRadius = magnetRadius + (PlayerStats.Shared?.MagnetRadiusBonus ?? 0f);
            if (dist <= effectiveRadius)
            {
                _state         = State.Attract;
                _attractSpeed  = 4f;
            }
            return;
        }

        // Attract
        _attractSpeed = Mathf.Min(_attractSpeed + attractAccel * Time.deltaTime, attractMaxSpeed);
        Vector3 dir  = (Vector3)player.Position - transform.position;
        float   d    = dir.magnitude;
        if (d > 0.01f)
            transform.position += dir.normalized * _attractSpeed * Time.deltaTime;

        // 가까워질수록 축소
        float scaleFactor = Mathf.Lerp(0.1f, 1f, Mathf.Clamp01(d / 1.5f));
        transform.localScale = _originScale * scaleFactor;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent<PlayerController>(out var player)) return;
        Collect(player);
    }

    private void Collect(PlayerController player)
    {
        float amount = player.MaxHPValue * (healPercent / 100f);
        player.HealHP(amount);
        FXManager.Shared?.SpawnPlayerText($"+{Mathf.RoundToInt(amount)}", Color.green);
        Destroy(gameObject);
    }

    private IEnumerator PopIn()
    {
        transform.localScale = Vector3.zero;
        float t = 0f;
        while (t < popDuration)
        {
            t += Time.deltaTime;
            float ratio = t / popDuration;
            transform.localScale = _originScale * Mathf.Lerp(0f, 1.15f, ratio);
            yield return null;
        }
        t = 0f;
        while (t < popDuration * 0.5f)
        {
            t += Time.deltaTime;
            transform.localScale = _originScale * Mathf.Lerp(1.15f, 1f, t / (popDuration * 0.5f));
            yield return null;
        }
        transform.localScale = _originScale;
    }
}
