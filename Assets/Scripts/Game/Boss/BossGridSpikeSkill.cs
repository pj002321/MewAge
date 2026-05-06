using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 스킬: 그리드 스파이크.
///
/// 맵을 cols × rows 그리드로 분할하고, FillMode에 따라
/// 선택된 셀에 경고 표시 후 스파이크를 떨어뜨립니다.
///
/// FillMode:
///   All         — 전체 셀
///   Checkerboard — 체커보드 (흑/백 교차)
///   Random      — randomCount개 무작위
///   Ring        — 테두리만
///   Cross       — 가운데 십자
///   WaveRow     — 행 순서로 순차 낙하
///   WaveCol     — 열 순서로 순차 낙하
/// </summary>
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
    [Tooltip("그리드 셀에 스폰할 경고 마커 프리팹. null이면 흰 사각형 자동 생성")]
    [SerializeField] private GameObject warningMarkerPrefab;
    [SerializeField] private Color warningColor = new Color(1f, 0.45f, 0f, 0.45f);

    [Header("낙하 VFX")]
    [Tooltip("스파이크 낙하 위치에 스폰할 VFX 프리팹")]
    [SerializeField] private GameObject spawnVfxPrefab;
    [Tooltip("0이면 VFX를 자동으로 제거하지 않음")]
    [SerializeField] private float spawnVfxDuration = 1f;

    // ── BossSkillBase ────────────────────────────────────────────

    protected override IEnumerator OnExecute()
    {
        if (IsInvalid()) yield break;

        GetMapBounds(out Vector2 bMin, out Vector2 bMax);
        Vector2 mapSize  = bMax - bMin;
        Vector2 cellSize = new Vector2(mapSize.x / cols, mapSize.y / rows);
        // 셀 중심 원점: 맵 좌하단 + 셀 반칸
        Vector2 origin   = bMin + cellSize * 0.5f;

        List<Vector2Int> cells = SelectCells();

        if (fillMode == FillMode.WaveRow || fillMode == FillMode.WaveCol)
            yield return WaveDrop(cells, origin, cellSize);
        else
            yield return SimultaneousDrop(cells, origin, cellSize);
    }

    // ── 동시 낙하 ────────────────────────────────────────────────

    private IEnumerator SimultaneousDrop(List<Vector2Int> cells, Vector2 origin, Vector2 cellSize)
    {
        var markers = SpawnMarkers(cells, origin, cellSize);

        yield return WarnAnimation(markers);
        CleanupAll(markers);
        if (IsInvalid()) yield break;

        foreach (var cell in cells)
            SpawnSpike(CellToWorld(cell, origin, cellSize));
    }

    // ── 순차 낙하 (행/열) ────────────────────────────────────────

    private IEnumerator WaveDrop(List<Vector2Int> cells, Vector2 origin, Vector2 cellSize)
    {
        bool byRow = fillMode == FillMode.WaveRow;

        // 행/열 별 그룹화
        var groups = new Dictionary<int, List<Vector2Int>>();
        foreach (var cell in cells)
        {
            int key = byRow ? cell.y : cell.x;
            if (!groups.TryGetValue(key, out var list))
            {
                list = new List<Vector2Int>();
                groups[key] = list;
            }
            list.Add(cell);
        }

        var keys = new List<int>(groups.Keys);
        keys.Sort();

        foreach (int key in keys)
        {
            if (IsInvalid()) yield break;

            var group   = groups[key];
            var markers = SpawnMarkers(group, origin, cellSize);

            yield return WarnAnimation(markers);
            CleanupAll(markers);
            if (IsInvalid()) yield break;

            foreach (var cell in group)
                SpawnSpike(CellToWorld(cell, origin, cellSize));

            if (waveDelay > 0f)
                yield return new WaitForSeconds(waveDelay);
        }
    }

    // ── 경고 애니메이션 ──────────────────────────────────────────

    private IEnumerator WarnAnimation(GameObject[] markers)
    {
        float elapsed = 0f;
        while (elapsed < warningDuration)
        {
            if (IsInvalid()) { CleanupAll(markers); yield break; }
            elapsed += Time.deltaTime;
            yield return null;
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
                        if ((x + y) % 2 == 0)
                            result.Add(new Vector2Int(x, y));
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
                        if (x == midX || y == midY)
                            result.Add(new Vector2Int(x, y));
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

        GameObject go;
        if (warningMarkerPrefab != null)
        {
            go = Instantiate(warningMarkerPrefab, new Vector3(pos.x, pos.y, 0f), Quaternion.identity);
        }
        else
        {
            go = new GameObject("SpikeWarn");
            go.transform.position   = new Vector3(pos.x, pos.y, 0f);
            go.transform.localScale = new Vector3(cellSize.x * 0.88f, cellSize.y * 0.88f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = GetWhiteSprite();
            sr.color        = warningColor;
            sr.sortingOrder = 5;
        }

        return go;
    }

    // ── 스파이크 낙하 ────────────────────────────────────────────

    private void SpawnSpike(Vector2 pos)
    {
        // VFX
        if (spawnVfxPrefab != null)
        {
            var vfx = Instantiate(spawnVfxPrefab, new Vector3(pos.x, pos.y, 0f), Quaternion.identity);
            if (spawnVfxDuration > 0f) Destroy(vfx, spawnVfxDuration);
        }

        // 스파이크 (프리팹은 시각적 역할만)
        if (spikePrefab != null)
        {
            var go = Instantiate(spikePrefab, new Vector3(pos.x, pos.y, 0f), Quaternion.identity);
            Destroy(go, spikeDuration);
        }

        // spikeDuration 동안 주기적으로 범위 데미지 판정
        float dmg = spikeDamage > 0f ? spikeDamage : Owner.ContactDamage;
        StartCoroutine(SpikeHitRoutine(pos, dmg));
    }

    private IEnumerator SpikeHitRoutine(Vector2 pos, float dmg)
    {
        float elapsed  = 0f;
        float cooldown = 0f;
        while (elapsed < spikeDuration)
        {
            elapsed  += Time.deltaTime;
            cooldown -= Time.deltaTime;

            var player = PlayerController.Instance;
            if (cooldown <= 0f && player != null &&
                Vector2.Distance(pos, player.transform.position) < spikeRadius)
            {
                player.TakeDamage(dmg);
                cooldown = 0.5f; // 0.5초마다 최대 1회 데미지
            }

            yield return null;
        }
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

    private static void CleanupAll(GameObject[] gos)
    {
        foreach (var go in gos)
            if (go != null) Destroy(go);
    }

    private static void GetMapBounds(out Vector2 min, out Vector2 max)
    {
        var mb = MovementBounds.Shared;
        if (mb != null)
        {
            mb.GetPlayerBoundsMinMax(out min, out max);
            return;
        }

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
    // alertColor 제거됨 — 경고 마커 색상 변화 없음

    private bool IsInvalid() => Owner == null;

    // ── 에디터 기즈모 ────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        GetMapBounds(out Vector2 bMin, out Vector2 bMax);
        Vector2 mapSize  = bMax - bMin;
        Vector2 cellSize = new Vector2(mapSize.x / cols, mapSize.y / rows);
        Vector2 origin   = bMin + cellSize * 0.5f;

        var cells = SelectCells();
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.25f);
        foreach (var cell in cells)
        {
            Vector2 pos = CellToWorld(cell, origin, cellSize);
            Gizmos.DrawCube(new Vector3(pos.x, pos.y, 0f),
                            new Vector3(cellSize.x * 0.85f, cellSize.y * 0.85f, 0.01f));
        }

        // 그리드 외곽선
        Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
        for (int x = 0; x <= cols; x++)
        {
            float wx = bMin.x + x * cellSize.x;
            Gizmos.DrawLine(new Vector3(wx, bMin.y, 0f), new Vector3(wx, bMax.y, 0f));
        }
        for (int y = 0; y <= rows; y++)
        {
            float wy = bMin.y + y * cellSize.y;
            Gizmos.DrawLine(new Vector3(bMin.x, wy, 0f), new Vector3(bMax.x, wy, 0f));
        }
    }
#endif
}
