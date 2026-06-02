using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Mewage/Unit Visual Config", fileName = "UnitVisualConfig")]
public class UnitVisualConfig : ScriptableObject
{
    [Serializable]
    public struct ChapterEntry
    {
        public int      chapter;
        public Sprite[] unitIcons;   // DB_유닛 ForEach 순서와 동일
    }

    [Serializable]
    public struct BossIconEntry
    {
        public string bossId;
        public Sprite icon;
    }

    public Sprite normalEnemyFrame;
    public Sprite bossEnemyFrame;

    public ChapterEntry[]  chapters;
    public BossIconEntry[] bossIcons;  // DB_보스 f_보스ID 기준

    public Sprite GetSprite(int chapter, int indexInChapter)
    {
        foreach (var ch in chapters)
        {
            if (ch.chapter != chapter) continue;
            if (ch.unitIcons == null || indexInChapter >= ch.unitIcons.Length) return null;
            return ch.unitIcons[indexInChapter];
        }
        return null;
    }

    public Sprite GetBossIcon(string bossId)
    {
        if (bossIcons == null) return null;
        foreach (var e in bossIcons)
            if (e.bossId == bossId) return e.icon;
        return null;
    }

    private static UnitVisualConfig _shared;
    public static UnitVisualConfig Shared
    {
        get
        {
            if (_shared == null)
                _shared = Resources.Load<UnitVisualConfig>("Unit/UnitVisualConfig");
            return _shared;
        }
    }
}
