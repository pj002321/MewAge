using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 자식으로 부착하는 고스트 트레일 컴포넌트.
///
/// · customSprite가 비어있으면 부모의 SpriteRenderer 스프라이트를 복사
/// · customSprite를 지정하면 해당 스프라이트로 고스트를 생성 (캐릭터별 커스텀)
/// · 부모가 일정 거리 이상 이동할 때마다 고스트를 스폰, 시간이 지나면 페이드아웃
/// </summary>
public class GhostTrail : MonoBehaviour
{
    [Header("스프라이트 / 프리팹")]
    [Tooltip("연결하면 해당 프리팹을 풀로 사용. 비워두면 단일 스프라이트 모드.")]
    [SerializeField] private GameObject ghostPrefab;
    [Tooltip("ghostPrefab 사용 시 — 체크하면 캐릭터 현재 스프라이트를 프리팹 SpriteRenderer에 덮어씀.\n" +
             "체크 해제 시 프리팹 자체 스프라이트/애니메이션을 그대로 사용.")]
    [SerializeField] private bool copyCharacterSprite = true;
    [Tooltip("ghostPrefab이 없을 때만 사용. 비워두면 부모 SpriteRenderer 스프라이트를 자동으로 복사합니다.")]
    [SerializeField] private Sprite customSprite;

    [Header("트레일 설정")]
    [SerializeField] private float spawnInterval   = 0.06f;  // 생성 간격 (초)
    [SerializeField] private float ghostLifetime   = 0.3f;   // 고스트 지속 시간 (초)
    [SerializeField] private float minMoveDist     = 0.05f;  // 이 거리 미만이면 생성 안 함
    [SerializeField] private Color ghostColor      = new Color(0.6f, 0.85f, 1f, 0.5f);

    private SpriteRenderer _parentSR;
    private Transform      _parentTr;
    private Vector3        _lastSpawnPos;
    private float          _timer;

    // 고스트 풀
    private readonly Queue<GhostItem> _pool   = new();
    private readonly List<GhostItem>  _active = new();

    private class GhostItem
    {
        public GameObject      go;
        public SpriteRenderer  sr;
        public float           elapsed;
        public float           lifetime;
        public Color           startColor;
    }

    // ── 초기화 ──────────────────────────────────────────────────────

    private void Awake()
    {
        _parentTr = transform.parent;
        if (_parentTr != null)
            _parentSR = _parentTr.GetComponentInChildren<SpriteRenderer>();
        _lastSpawnPos = _parentTr != null ? _parentTr.position : Vector3.zero;
    }

    // ── 업데이트 ────────────────────────────────────────────────────

    private void Update()
    {
        UpdateActiveGhosts();

        if (_parentTr == null) return;

        // 이동 거리 체크
        float dist = Vector3.Distance(_parentTr.position, _lastSpawnPos);
        if (dist < minMoveDist) return;

        // 생성 간격 체크
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = spawnInterval;

        SpawnGhost();
        _lastSpawnPos = _parentTr.position;
    }

    // ── 고스트 생성 ──────────────────────────────────────────────────

    private void SpawnGhost()
    {
        GhostItem g;

        if (ghostPrefab != null)
        {
            // ── 프리팹 모드 ──────────────────────────────────────────
            if (_pool.Count > 0)
            {
                g = _pool.Dequeue();
                g.go.SetActive(true);
            }
            else
            {
                var go = Instantiate(ghostPrefab);
                var sr = go.GetComponentInChildren<SpriteRenderer>();
                g = new GhostItem { go = go, sr = sr };
            }

            if (g.sr != null && _parentSR != null)
            {
                if (copyCharacterSprite)
                    g.sr.sprite = _parentSR.sprite; // 캐릭터 스프라이트 덮어쓰기
                g.sr.flipX = _parentSR.flipX;
            }
        }
        else
        {
            // ── 단일 스프라이트 모드 ─────────────────────────────────
            Sprite sprite = customSprite;
            bool   flipX  = false;

            if (sprite == null)
            {
                if (_parentSR == null) return;
                sprite = _parentSR.sprite;
                flipX  = _parentSR.flipX;
            }
            if (sprite == null) return;

            if (_pool.Count > 0)
            {
                g = _pool.Dequeue();
                g.go.SetActive(true);
            }
            else
            {
                var go = new GameObject("Ghost");
                var sr = go.AddComponent<SpriteRenderer>();
                g = new GhostItem { go = go, sr = sr };
            }

            g.sr.sprite = sprite;
            g.sr.flipX  = flipX;
        }

        // 정렬 순서
        if (g.sr != null && _parentSR != null)
        {
            g.sr.sortingLayerName = _parentSR.sortingLayerName;
            g.sr.sortingOrder     = _parentSR.sortingOrder - 1;
        }

        // 위치·크기
        g.go.transform.position   = _parentTr.position;
        g.go.transform.rotation   = _parentTr.rotation;
        g.go.transform.localScale = _parentTr.lossyScale;

        // 색상 초기화
        g.startColor = ghostColor;
        if (g.sr != null) g.sr.color = ghostColor;
        g.elapsed  = 0f;
        g.lifetime = ghostLifetime;

        _active.Add(g);
    }

    // ── 활성 고스트 페이드아웃 ───────────────────────────────────────

    private void UpdateActiveGhosts()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            var g = _active[i];
            g.elapsed += Time.deltaTime;

            float t = Mathf.Clamp01(g.elapsed / g.lifetime);
            var c = g.startColor;
            c.a = g.startColor.a * (1f - t);
            g.sr.color = c;

            if (g.elapsed >= g.lifetime)
            {
                g.go.SetActive(false);
                _pool.Enqueue(g);
                // 마지막 요소와 교환 후 제거 — O(1)
                _active[i] = _active[_active.Count - 1];
                _active.RemoveAt(_active.Count - 1);
            }
        }
    }

    // ── 정리 ────────────────────────────────────────────────────────

    private void OnDisable()
    {
        // 비활성화 시 활성 고스트 즉시 숨김
        foreach (var g in _active)
        {
            if (g.go != null) g.go.SetActive(false);
            _pool.Enqueue(g);
        }
        _active.Clear();
        _timer = 0f;
    }

    private void OnDestroy()
    {
        // 풀 포함 모든 고스트 오브젝트 파괴
        foreach (var g in _active)
            if (g.go != null) Destroy(g.go);
        foreach (var g in _pool)
            if (g.go != null) Destroy(g.go);
        _active.Clear();
        _pool.Clear();
    }
}
