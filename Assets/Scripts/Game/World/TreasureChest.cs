using System.Collections;
using System.Collections.Generic;
using Mewage.DB;
using UnityEngine;

/// <summary>
/// 웨이브 중 위에서 낙하하는 보물 상자.
/// 플레이어 무기로 파괴하면 보상 Orb(RewardOrb)를 2~3개 뿌립니다.
///
/// 프리팹 구성:
///   - TreasureChest (스크립트 부착), Enemy 레이어, BoxCollider2D(trigger)
///   - Animator: bool 파라미터 "bDestroy" — true 시 파괴 애니메이션 재생
///   - 자식: SpriteRenderer
///
/// 스폰: TreasureChestSpawner → StartFall() 호출.
/// 보상 Orb 프리팹: Resources/World/reward_orb
/// </summary>
public class TreasureChest : MonoBehaviour
{
    /// <summary>씬 내 활성 상자 목록. 웨이브 클리어 시 TreasureChestSpawner.ClearAll() 에서 정리.</summary>
    public static readonly List<TreasureChest> All = new();

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => All.Clear();

    [Header("낙하")]
    [SerializeField] private float fallGravity = 35f;

    [Header("HP 배율")]
    [Tooltip("HP = 플레이어 현재 ATK × 이 값")]
    [SerializeField] private float hpAtkMultiplier = 6f;

    [Header("파괴 애니메이션 대기 시간 (초)")]
    [Tooltip("bDestroy = true 세팅 후 오브 스폰 전까지 대기. Animator 파괴 클립 길이에 맞게 조정.")]
    [SerializeField] private float breakAnimDuration = 0.45f;

    private const string OrbPrefabPath = "World/reward_orb";

    private float      _hp;
    private bool       _landed;
    private bool       _broken;
    private GameObject _orbPrefab;

    public bool IsLanded => _landed;
    public bool IsBroken => _broken;

    // ── Unity 생명주기 ─────────────────────────────────────────────
    private void OnEnable()  => All.Add(this);
    private void OnDisable() => All.Remove(this);
    private void OnDestroy() => All.Remove(this);

    // ── 공개 API ───────────────────────────────────────────────────

    /// <summary>낙하 연출 시작. TreasureChestSpawner에서 호출합니다.</summary>
    public void StartFall(Vector2 landPos, float spawnTopY)
    {
        float atk = PlayerStatManager.Shared?.GetFinal(StatType.ATK) ?? 50f;
        _hp      = Mathf.Max(20f, atk * hpAtkMultiplier);
        _landed  = false;
        _broken  = false;
        transform.position = new Vector3(landPos.x, spawnTopY, 0f);
        StartCoroutine(FallRoutine(landPos));
    }

    /// <summary>무기에서 호출하는 피해 적용.</summary>
    public void TakeDamage(float damage)
    {
        if (_broken || !_landed) return;
        _hp -= damage;
        SoundManager.Shared?.Play(SoundKey.Enemy_Hit);
        FXManager.Shared?.SpawnHitEffect(transform.position);
        if (_hp <= 0f) Break();
    }

    // ── 낙하 연출 ─────────────────────────────────────────────────

    private IEnumerator FallRoutine(Vector2 landPos)
    {
        float fallSpeed = 0f;
        while (true)
        {
            yield return new WaitForFixedUpdate();
            fallSpeed += fallGravity * Time.fixedDeltaTime;
            Vector2 cur  = transform.position;
            Vector2 next = cur + Vector2.down * fallSpeed * Time.fixedDeltaTime;
            if (next.y <= landPos.y)
            {
                transform.position = new Vector3(landPos.x, landPos.y, 0f);
                break;
            }
            transform.position = new Vector3(next.x, next.y, 0f);
        }

        yield return StartCoroutine(LandingSquish());
        FXManager.Shared?.SpawnLandingVFX(transform.position);
        _landed = true;
        StartCoroutine(ActivateDecal());
    }

    private IEnumerator LandingSquish()
    {
        Vector3 orig    = transform.localScale;
        Vector3 squish  = new Vector3(orig.x * 1.3f,  orig.y * 0.55f, orig.z);
        Vector3 stretch = new Vector3(orig.x * 0.85f, orig.y * 1.2f,  orig.z);

        for (float e = 0f; e < 0.07f; e += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(orig, squish, e / 0.07f);
            yield return null;
        }
        transform.localScale = squish;
        for (float e = 0f; e < 0.10f; e += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(squish, stretch, e / 0.10f);
            yield return null;
        }
        transform.localScale = stretch;
        for (float e = 0f; e < 0.14f; e += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(stretch, orig, e / 0.14f);
            yield return null;
        }
        transform.localScale = orig;
    }

    // ── 데칼 ──────────────────────────────────────────────────────

    private IEnumerator ActivateDecal()
    {
        // 비활성화된 자식 SpriteRenderer 탐색
        SpriteRenderer decalSr = null;
        foreach (Transform child in transform)
        {
            if (child.gameObject.activeSelf) continue;
            var sr = child.GetComponent<SpriteRenderer>();
            if (sr != null) { decalSr = sr; break; }
        }
        if (decalSr == null) yield break;

        decalSr.gameObject.SetActive(true);
        Color c = decalSr.color;
        c.a = 1f;
        decalSr.color = c;

        const float fadeDuration = 1.2f;
        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            c.a = 1f - t / fadeDuration;
            decalSr.color = c;
            yield return null;
        }

        c.a = 0f;
        decalSr.color = c;
        decalSr.gameObject.SetActive(false);
    }

    // ── 파괴 ──────────────────────────────────────────────────────

    private void Break()
    {
        if (_broken) return;
        _broken = true;
        StopAllCoroutines();
        StartCoroutine(BreakRoutine());
    }

    private IEnumerator BreakRoutine()
    {
        // 파괴 애니메이션 트리거
        var anim = GetComponent<Animator>();
        if (anim != null) anim.SetBool("bDestroy", true);

        yield return new WaitForSeconds(breakAnimDuration);

        // 보상 Orb 스폰
        SpawnRewardOrbs();

        SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
        FXManager.Shared?.SpawnLandingVFX(transform.position);
        Destroy(gameObject);
    }

    private void SpawnRewardOrbs()
    {
        if (_orbPrefab == null) _orbPrefab = Resources.Load<GameObject>(OrbPrefabPath);
        if (_orbPrefab == null)
        {
            this.DLog("[TreasureChest] reward_orb 프리팹 없음 — Resources/World/reward_orb 확인");
            return;
        }

        // DB_상점에서 MAT_ 보상 행 수집
        var pool = new List<DB_상점>();
        DB_상점.ForEachEntity(row =>
        {
            if (!string.IsNullOrEmpty(row.f_보상ID) && row.f_보상ID.StartsWith("MAT_"))
                pool.Add(row);
        });

        if (pool.Count == 0)
        {
            this.DLog("[TreasureChest] DB_상점에 MAT_ 보상 행 없음");
            return;
        }

        int count = Random.Range(2, 4); // 2 또는 3개
        for (int i = 0; i < count; i++)
        {
            var row  = pool[Random.Range(0, pool.Count)];
            var item = RewardItem.FromDB(row);

            float angleDeg = Random.Range(30f, 150f);
            float rad      = angleDeg * Mathf.Deg2Rad;
            float speed    = Random.Range(5f, 10f);
            var   vel      = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * speed;

            Vector3 offset = new(Random.Range(-0.2f, 0.2f), Random.Range(-0.1f, 0.1f), 0f);
            var go  = Instantiate(_orbPrefab, transform.position + offset, Quaternion.identity);
            go.GetComponent<RewardOrb>()?.Init(item, vel);
        }
    }
}
