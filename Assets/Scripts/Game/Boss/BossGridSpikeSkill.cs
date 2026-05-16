using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 보스 스킬: 그리드 스파이크.
/// 맵을 cols × rows 그리드로 분할하고, FillMode에 따라
/// 선택된 셀에 경고 표시 후 스파이크를 떨어뜨립니다.
///
/// FillMode:
///   All, Checkerboard, Random, Ring, Cross, WaveRow, WaveCol
/// </summary>
[System.Serializable]
public class BossGridSpikeSkill : BossSkillBase
{
    public enum FillMode { All, Checkerboard, Random, Ring, Cross, WaveRow, WaveCol }

    [Header("그리드")]
    [SerializeField] private int      cols        = 6;
    [SerializeField] private int      rows        = 5;
    [SerializeField] private FillMode fillMode    = FillMode.Checkerboard;
    [Tooltip("Random 모드에서 선택할 셀 수")]
    [SerializeField] private int      randomCount = 12;

    [Header("스파이크")]
    [Tooltip("null이면 데미지 판정만 (원 충돌)")]
    [SerializeField] private GameObject spikePrefab;
    [Tooltip("0이면 Owner.ContactDamage 사용")]
    [SerializeField] private float      spikeDamage   = 0f;
    [SerializeField] private float      spikeRadius   = 0.45f;
    [SerializeField] private float      spikeDuration = 1.5f;

    [Header("타이밍")]
    [SerializeField] private float warningDuration = 1.2f;
    [Tooltip("WaveRow/WaveCol: 각 줄 낙하 후 다음 줄까지 대기 시간")]
    [SerializeField] private float waveDelay       = 0.25f;

    [Header("경고 표시")]
    [Tooltip("null이면 흰 사각형 자동 생성")]
    [SerializeField] private GameObject warningMarkerPrefab;
    [SerializeField] private Color      warningColor = new Color(1f, 0.45f, 0f, 0.45f);

    [Header("낙하 연출")]
    [SerializeField] private float dropHeight   = 8f;
    [SerializeField] private float dropDuration = 0.25f;

    [Header("착지 VFX")]
    [SerializeField] private GameObject spawnVfxPrefab;
    [Tooltip("0이면 자동 제거 안 함")]
    [SerializeField] private float      spawnVfxDuration = 1f;

    protected override async UniTask OnExecuteAsync(CancellationToken ct)
    {
        if (Boss == null) { this.DLog("[GridSpike] Owner null — skip"); return; }

        GetMapBounds(out Vector2 bMin, out Vector2 bMax);
        Vector2 mapSize  = bMax - bMin;
        Vector2 cellSize = new(mapSize.x / cols, mapSize.y / rows);
        Vector2 origin   = bMin + cellSize * 0.5f;

        List<Vector2Int> cells = SelectCells();

        // 낙하 태스크는 boss 파괴 토큰과 연결 — 패턴 그로기 취소(ct)와는 독립
        var destroyCt = Boss.GetCancellationTokenOnDestroy();

        if (fillMode == FillMode.WaveRow || fillMode == FillMode.WaveCol)
            WaveDropAsync(cells, origin, cellSize, destroyCt).Forget();
        else
            SimultaneousDropAsync(cells, origin, cellSize, destroyCt).Forget();

        // 패턴 루프 측은 경고+낙하+지속 시간 대기 후 다음 스킬로 진행
        await UniTask.Delay(
            System.TimeSpan.FromSeconds(warningDuration + dropDuration + spikeDuration),
            cancellationToken: ct)
            .SuppressCancellationThrow();
    }

    // ── 동시 낙하 ────────────────────────────────────────────────

    private async UniTaskVoid SimultaneousDropAsync(
        List<Vector2Int> cells, Vector2 origin, Vector2 cellSize, CancellationToken ct)
    {
        var markers = SpawnMarkers(cells, origin, cellSize);
        await WarnAnimationAsync(markers, ct);
        CleanupAll(markers);
        if (Boss == null || ct.IsCancellationRequested) return;

        foreach (var cell in cells)
            SpikeFallAsync(CellToWorld(cell, origin, cellSize), ct).Forget();
    }

    // ── 순차 낙하 (행/열) ────────────────────────────────────────

    private async UniTaskVoid WaveDropAsync(
        List<Vector2Int> cells, Vector2 origin, Vector2 cellSize, CancellationToken ct)
    {
        bool byRow = fillMode == FillMode.WaveRow;

        var groups = new Dictionary<int, List<Vector2Int>>();
        foreach (var cell in cells)
        {
            int key = byRow ? cell.y : cell.x;
            if (!groups.TryGetValue(key, out var list)) { list = new List<Vector2Int>(); groups[key] = list; }
            list.Add(cell);
        }

        var keys = new List<int>(groups.Keys);
        keys.Sort();

        foreach (int key in keys)
        {
            if (Boss == null || ct.IsCancellationRequested) return;

            var group   = groups[key];
            var markers = SpawnMarkers(group, origin, cellSize);

            await WarnAnimationAsync(markers, ct);
            CleanupAll(markers);
            if (Boss == null || ct.IsCancellationRequested) return;

            foreach (var cell in group)
                SpikeFallAsync(CellToWorld(cell, origin, cellSize), ct).Forget();

            if (waveDelay > 0f)
                await UniTask.Delay(System.TimeSpan.FromSeconds(waveDelay), cancellationToken: ct)
                             .SuppressCancellationThrow();
        }
    }

    // ── 경고 애니메이션 ──────────────────────────────────────────

    private async UniTask WarnAnimationAsync(GameObject[] markers, CancellationToken ct)
    {
        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            if (Boss == null) return;
            elapsed += Time.deltaTime;
            bool cancelled = await UniTask.Yield(ct).SuppressCancellationThrow();
            if (cancelled) return;
        }
    }

    // ── 스파이크 낙하 ────────────────────────────────────────────

    private async UniTaskVoid SpikeFallAsync(Vector2 targetPos, CancellationToken ct)
    {
        float dmg = spikeDamage > 0f ? spikeDamage : (Boss != null ? Boss.ContactDamage : 0f);

        Vector2    startPos = targetPos + Vector2.up * dropHeight;
        GameObject spikeGo  = null;

        if (spikePrefab != null)
            spikeGo = Object.Instantiate(spikePrefab,
                new Vector3(startPos.x, startPos.y, 0f), Quaternion.identity);

        // 낙하 (ease-in quad)
        float elapsed = 0f;
        while (elapsed < dropDuration)
        {
            elapsed += Time.deltaTime;
            float   t     = Mathf.Clamp01(elapsed / dropDuration);
            float   eased = t * t;
            Vector2 cur   = Vector2.LerpUnclamped(startPos, targetPos, eased);
            if (spikeGo != null)
                spikeGo.transform.position = new Vector3(cur.x, cur.y, 0f);

            bool cancelled = await UniTask.Yield(ct).SuppressCancellationThrow();
            if (cancelled) { if (spikeGo != null) Object.Destroy(spikeGo); return; }
        }

        if (spikeGo != null)
            spikeGo.transform.position = new Vector3(targetPos.x, targetPos.y, 0f);

        // 착지 VFX
        if (spawnVfxPrefab != null)
        {
            float vfxY = targetPos.y;
            if (spikeGo != null)
            {
                var col = spikeGo.GetComponent<Collider2D>()
                       ?? spikeGo.GetComponentInChildren<Collider2D>();
                if (col != null) vfxY = col.bounds.min.y;
            }
            var vfx = Object.Instantiate(spawnVfxPrefab,
                new Vector3(targetPos.x, vfxY, 0f), Quaternion.identity);
            if (spawnVfxDuration > 0f) Object.Destroy(vfx, spawnVfxDuration);
        }

        if (spikeGo != null) Object.Destroy(spikeGo, spikeDuration);

        await SpikeHitAsync(targetPos, dmg, ct);
    }

    private async UniTask SpikeHitAsync(Vector2 pos, float dmg, CancellationToken ct)
    {
        float elapsed     = 0f;
        float hitCooldown = 0f;
        while (elapsed < spikeDuration)
        {
            elapsed     += Time.deltaTime;
            hitCooldown -= Time.deltaTime;

            var player = PlayerController.Instance;
            if (hitCooldown <= 0f && player != null &&
                Vector2.Distance(pos, player.transform.position) < spikeRadius)
            {
                player.TakeDamage(dmg, sourceName: Boss?.gameObject.name);
                hitCooldown = 0.5f;
            }

            bool cancelled = await UniTask.Yield(ct).SuppressCancellationThrow();
            if (cancelled) return;
        }
    }

    // ── 셀 선택 ──────────────────────────────────────────────────

    private List<Vector2Int> SelectCells()
    {
        var result = new List<Vector2Int>();

        switch (fillMode)
        {
            case FillMode.All:
            case FillMode.WaveRow:
            case FillMode.WaveCol:
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < cols; x++)
                        result.Add(new Vector2Int(x, y));
                break;

            case FillMode.Checkerboard:
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < cols; x++)
                        if ((x + y) % 2 == 0) result.Add(new Vector2Int(x, y));
                break;

            case FillMode.Random:
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < cols; x++)
                        result.Add(new Vector2Int(x, y));
                Shuffle(result);
                if (result.Count > randomCount)
                    result.RemoveRange(randomCount, result.Count - randomCount);
                break;

            case FillMode.Ring:
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < cols; x++)
                        if (x == 0 || x == cols - 1 || y == 0 || y == rows - 1)
                            result.Add(new Vector2Int(x, y));
                break;

            case FillMode.Cross:
                int midX = cols / 2, midY = rows / 2;
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < cols; x++)
                        if (x == midX || y == midY) result.Add(new Vector2Int(x, y));
                break;
        }
        return result;
    }

    // ── 마커 스폰 ────────────────────────────────────────────────

    private GameObject[] SpawnMarkers(List<Vector2Int> cells, Vector2 origin, Vector2 cellSize)
    {
        var markers = new GameObject[cells.Count];
        for (int i = 0; i < cells.Count; i++)
            markers[i] = SpawnMarker(CellToWorld(cells[i], origin, cellSize), cellSize);
        return markers;
    }

    private GameObject SpawnMarker(Vector2 pos, Vector2 cellSize)
    {
        if (!WarningLineSettings.Enabled) return null;

        if (warningMarkerPrefab != null)
            return Object.Instantiate(warningMarkerPrefab, new Vector3(pos.x, pos.y, 0f), Quaternion.identity);

        var go = new GameObject("SpikeWarn");
        go.transform.position   = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = new Vector3(cellSize.x * 0.88f, cellSize.y * 0.88f, 1f);
        var sr       = go.AddComponent<SpriteRenderer>();
        sr.sprite       = GetWhiteSprite();
        sr.color        = warningColor;
        sr.sortingOrder = 5;
        return go;
    }

    // ── 유틸 ─────────────────────────────────────────────────────

    private static Vector2 CellToWorld(Vector2Int cell, Vector2 origin, Vector2 cellSize)
        => origin + new Vector2(cell.x * cellSize.x, cell.y * cellSize.y);

    private static void Shuffle(List<Vector2Int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void CleanupAll(GameObject[] gos)
    {
        foreach (var go in gos)
            if (go != null) Object.Destroy(go);
    }

    private static void GetMapBounds(out Vector2 min, out Vector2 max)
    {
        var mb = MovementBounds.Shared;
        if (mb != null) { mb.GetPlayerBoundsMinMax(out min, out max); return; }

        var spawner = EnemySpawner.Shared;
        if (spawner != null)
        {
            min = spawner.AreaCenter - spawner.AreaSize * 0.5f;
            max = spawner.AreaCenter + spawner.AreaSize * 0.5f;
            return;
        }

        var cam = Camera.main;
        if (cam != null)
        {
            float h = cam.orthographicSize;
            float w = h * cam.aspect;
            Vector2 c = cam.transform.position;
            min = c - new Vector2(w, h);
            max = c + new Vector2(w, h);
            return;
        }

        min = new Vector2(-10f, -6f);
        max = new Vector2( 10f,  6f);
    }

    private static Sprite _whiteSprite;
    private static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;
        var tex    = new Texture2D(4, 4);
        var pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return _whiteSprite;
    }
}
