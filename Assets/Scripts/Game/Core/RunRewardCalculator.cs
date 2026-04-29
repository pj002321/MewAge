using UnityEngine;

/// <summary>
/// 런 종료 시 지급할 아웃게임 보상을 계산합니다.
///
/// ─ 골드: 인게임에서 획득한 골드 전액 승계
/// ─ 경험치: 웨이브 기반 소량 지급 (인게임 EXP와 별개 경제)
///     accountEXP = wave × 8 × chapterMult
///     챕터 배율: C1=1.0, C2=1.5, C3=2.0 …
/// ─ 루비: 5·10·15·20·25·30 웨이브 달성마다 랜덤 지급
///     Wave  5: 1~2   Wave 10: 1~3   Wave 15: 2~4
///     Wave 20: 3~5   Wave 25: 4~7   Wave 30: 6~10
/// </summary>
public struct RunReward
{
    public int gold;
    public int exp;
    public int ruby;
}

public static class RunRewardCalculator
{
    private static readonly int[]   RubyMilestones = { 5, 10, 15, 20, 25, 30 };
    private static readonly int[][] RubyRange      =
    {
        new[] { 1, 2 },   // wave 5
        new[] { 1, 3 },   // wave 10
        new[] { 2, 4 },   // wave 15
        new[] { 3, 5 },   // wave 20
        new[] { 4, 7 },   // wave 25
        new[] { 6, 10 },  // wave 30
    };

    /// <param name="sessionGold">인게임에서 획득한 골드 (GameManager.sessionGoldGained)</param>
    /// <param name="wave">도달 웨이브</param>
    public static RunReward Calculate(int sessionGold, int wave)
    {
        string chapterId   = PlayerPrefs.GetString("SelectedChapterID", "C1_S1");
        float  chapterMult = ParseChapterMult(chapterId);

        return new RunReward
        {
            gold = sessionGold,
            exp  = Mathf.RoundToInt(wave * 8 * chapterMult),
            ruby = CalcRuby(wave),
        };
    }

    private static int CalcRuby(int wave)
    {
        int total = 0;
        for (int i = 0; i < RubyMilestones.Length; i++)
        {
            if (wave >= RubyMilestones[i])
                total += Random.Range(RubyRange[i][0], RubyRange[i][1] + 1);
        }
        return total;
    }

    private static float ParseChapterMult(string chapterId)
    {
        if (string.IsNullOrEmpty(chapterId)) return 1f;
        string[] parts = chapterId.Split('_');
        if (parts.Length < 1 || parts[0].Length < 2) return 1f;
        if (!int.TryParse(parts[0].Substring(1), out int ch)) return 1f;
        return 1f + (ch - 1) * 0.5f;
    }
}
