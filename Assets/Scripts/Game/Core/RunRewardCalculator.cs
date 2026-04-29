using UnityEngine;

/// <summary>
/// 런 종료 시 지급할 아웃게임 보상을 계산합니다.
///
/// 보상 공식:
///   챕터 배율: C1=×1.0, C2=×1.5, C3=×2.0 …
///   골드  = wave × 10 × chapterMult × clearBonus
///   경험치 = wave × 5  × chapterMult × clearBonus
///   루비  = 클리어: max(1, wave/5)  /  게임오버: wave>=15이면 1, 미만이면 0
///   clearBonus: 클리어=1.5, 게임오버=1.0
/// </summary>
public struct RunReward
{
    public int gold;
    public int exp;
    public int ruby;
}

public static class RunRewardCalculator
{
    public static RunReward Calculate(int wave, bool isClear)
    {
        string chapterId  = PlayerPrefs.GetString("SelectedChapterID", "C1_S1");
        float  chapterMult = ParseChapterMult(chapterId);
        float  clearBonus  = isClear ? 1.5f : 1.0f;

        return new RunReward
        {
            gold = Mathf.RoundToInt(wave * 10 * chapterMult * clearBonus),
            exp  = Mathf.RoundToInt(wave * 5  * chapterMult * clearBonus),
            ruby = CalcRuby(wave, isClear),
        };
    }

    private static int CalcRuby(int wave, bool isClear)
    {
        if (isClear)  return Mathf.Max(1, wave / 5);
        return wave >= 15 ? 1 : 0;
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
