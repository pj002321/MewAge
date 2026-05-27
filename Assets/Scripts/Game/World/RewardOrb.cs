using System.Collections;
using Mewage.DB;
using UnityEngine;

/// <summary>
/// 보물 상자 파괴 시 뿌려지는 아웃게임 보상 Orb.
/// 단일 프리팹을 재사용하며 Init()으로 보상 타입에 따른 스프라이트와 이름을 설정합니다.
/// 포물선 낙하 → 플로팅 → 자석 반경 진입 시 흡수 → 플레이어 터치 즉시 수집.
///
/// 프리팹 구성:
///   - RewardOrb (스크립트 부착), 루트에 BoxCollider2D(trigger)
///   - 자식: SpriteRenderer
/// </summary>
public class RewardOrb : MonoBehaviour
{
    [Header("자석 흡수")]
    [SerializeField] private float magnetRadius    = 3f;
    [SerializeField] private float attractAccel    = 22f;
    [SerializeField] private float attractMaxSpeed = 18f;

    [Header("플로팅")]
    [SerializeField] private float floatAmplitude = 0.1f;
    [SerializeField] private float floatFrequency = 1.0f;

    private RewardItem     _item;
    private SpriteRenderer _sr;

    // ── 이동 상태 ──────────────────────────────────────────────────
    private Vector2 _burstVelocity;
    private float   _gravity;
    private bool    _useGravity;
    private float   _spawnY;

    private bool  _floating;
    private float _floatBaseY;
    private float _floatTime;

    private bool  _attracting;
    private float _attractSpeed;

    private float _timer;
    private const float MagnetDelay = 0.5f;

    // ── Unity ─────────────────────────────────────────────────────
    private void Awake() => _sr = GetComponentInChildren<SpriteRenderer>();

    // ── 공개 API ───────────────────────────────────────────────────

    /// <summary>
    /// 보상 타입과 포물선 초기 속도를 설정합니다.
    /// TreasureChest.SpawnRewardOrbs()에서 위치 설정 후 호출합니다.
    /// </summary>
    public void Init(RewardItem item, Vector2 burstVelocity, float gravity = 14f)
    {
        _item          = item;
        _burstVelocity = burstVelocity;
        _gravity       = gravity;
        _useGravity    = true;
        _spawnY        = transform.position.y;

        // 보상 타입에 따른 스프라이트 교체
        if (_sr != null)
        {
            Sprite icon = RewardVisualConfig.Shared?.GetIcon(item.type);
            if (icon != null) _sr.sprite = icon;
        }

        // 디버그 식별용 이름
        gameObject.name = $"RewardOrb_{item.type}";

        // 스폰 팝인 연출
        Vector3 origScale   = transform.localScale;
        transform.localScale = Vector3.zero;
        StartCoroutine(SpawnPopRoutine(origScale));
    }

    // ── 이동 ───────────────────────────────────────────────────────

    private void Update()
    {
        _timer += Time.deltaTime;

        if (_useGravity)
        {
            _burstVelocity.y -= _gravity * Time.deltaTime;
            _burstVelocity.x  = Mathf.MoveTowards(_burstVelocity.x, 0f, 3f * Time.deltaTime);
            transform.position += (Vector3)(_burstVelocity * Time.deltaTime);

            // 착지 판정: 하강 중 스폰 높이 이하
            if (_burstVelocity.y <= 0f && transform.position.y <= _spawnY + 0.05f)
            {
                var p = transform.position;
                transform.position = new Vector3(p.x, _spawnY, p.z);
                _burstVelocity = Vector2.zero;
                _useGravity    = false;
                BeginFloat();
            }
            return;
        }

        if (_attracting)
        {
            UpdateAttract();
            return;
        }

        if (_timer < MagnetDelay) return;

        // 플로팅 bob
        if (_floating)
        {
            _floatTime += Time.deltaTime;
            float y = _floatBaseY + Mathf.Sin(_floatTime * floatFrequency * Mathf.PI * 2f) * floatAmplitude;
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
        }

        // 자석 반경 진입 시 흡수 시작
        var player = PlayerController.Instance;
        if (player == null) return;
        float effectiveRadius = magnetRadius + (PlayerStats.Shared?.MagnetRadiusBonus ?? 0f);
        if (Vector2.Distance(transform.position, player.Position) <= effectiveRadius)
            BeginAttract();
    }

    private void UpdateAttract()
    {
        var player = PlayerController.Instance;
        if (player == null) return;

        _attractSpeed = Mathf.Min(_attractSpeed + attractAccel * Time.deltaTime, attractMaxSpeed);
        Vector3 dir   = (Vector3)player.Position - transform.position;
        float   dist  = dir.magnitude;
        if (dist > 0.01f)
            transform.position += dir.normalized * _attractSpeed * Time.deltaTime;

        // 가까워질수록 축소 (DropItem 패턴)
        float scaleFactor     = Mathf.Lerp(0.15f, 1f, Mathf.Clamp01(dist / 1.5f));
        transform.localScale  = Vector3.one * scaleFactor;
    }

    private void BeginFloat()
    {
        _floating   = true;
        _floatBaseY = transform.position.y;
        _floatTime  = Random.Range(0f, 1f);
    }

    private void BeginAttract()
    {
        _floating     = false;
        _attracting   = true;
        _attractSpeed = 4f;
    }

    // ── 수집 ───────────────────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent<PlayerController>(out _)) return;
        Collect();
    }

    private void Collect()
    {
        RunChestRewardTracker.Track(_item);
        RewardManager.Shared?.GiveReward(_item);
        PopUpInventory.Shared?.AddReward(_item, _sr?.sprite);
        FXManager.Shared?.SpawnPlayerText(RewardLabel(), Color.yellow);
        Destroy(gameObject);
    }

    private string RewardLabel()
    {
        string name = DB_상점.FindEntity(e => e.f_보상ID == _item.rewardId)?.f_보상이름;
        if (string.IsNullOrEmpty(name)) name = _item.rewardId;
        return $"{name} +{_item.amount}";
    }

    // ── 연출 ───────────────────────────────────────────────────────

    private IEnumerator SpawnPopRoutine(Vector3 target)
    {
        for (float t = 0f; t < 0.2f; t += Time.deltaTime)
        {
            transform.localScale = target * Mathf.Lerp(0f, 1.15f, t / 0.2f);
            yield return null;
        }
        for (float t = 0f; t < 0.1f; t += Time.deltaTime)
        {
            transform.localScale = target * Mathf.Lerp(1.15f, 1f, t / 0.1f);
            yield return null;
        }
        transform.localScale = target;
    }
}
