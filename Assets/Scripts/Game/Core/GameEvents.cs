using System;

/// <summary>
/// 게임 데이터 변경 이벤트 허브.
///
/// 사용 규칙:
///   - 데이터 소유자: 값이 바뀔 때 GameEvents.Notify*(...)  호출
///   - UI 소비자:    OnEnable 에서 GameEvents.On* += Handler
///                   OnDisable에서 GameEvents.On* -= Handler
///
/// 이 패턴으로 어느 곳에서 데이터를 바꿔도 구독한 모든 UI가 자동 갱신됩니다.
/// </summary>
public static class GameEvents
{
    // ── 골드 ──────────────────────────────────────────────────────
    /// <param name="gold">변경 후 골드 총량</param>
    public static event Action<int> OnGoldChanged;
    public static void GoldChanged(int gold) => OnGoldChanged?.Invoke(gold);

    // ── 경험치 / 레벨 ──────────────────────────────────────────────
    /// <param name="exp">현재 경험치</param>
    /// <param name="expMax">다음 레벨까지 필요 경험치</param>
    /// <param name="level">현재 레벨</param>
    public static event Action<int, int, int> OnExpChanged;
    public static void ExpChanged(int exp, int expMax, int level)
        => OnExpChanged?.Invoke(exp, expMax, level);

    // ── 플레이어 HP ───────────────────────────────────────────────
    /// <param name="current">현재 HP</param>
    /// <param name="max">최대 HP</param>
    public static event Action<float, float> OnHPChanged;
    public static void HPChanged(float current, float max)
        => OnHPChanged?.Invoke(current, max);

    // ── 웨이브 ────────────────────────────────────────────────────
    /// <param name="wave">현재 웨이브 번호</param>
    /// <param name="isBoss">보스 웨이브 여부</param>
    public static event Action<int, bool> OnWaveChanged;
    public static void WaveChanged(int wave, bool isBoss)
        => OnWaveChanged?.Invoke(wave, isBoss);
}
