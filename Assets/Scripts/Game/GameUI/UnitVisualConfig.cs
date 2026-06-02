using System;
using UnityEngine;

/// <summary>
/// 챕터별 유닛 아이콘 스프라이트만 등록하는 ScriptableObject.
/// 유닛 ID는 DB_유닛에서 읽어오고, 이 에셋에는 Sprite만 등록합니다.
/// 에디터 Inspector에서 DB 유닛 이름이 레이블로 표시됩니다.
/// Resources/Unit/UnitVisualConfig 에 저장하면 Shared로 전역 접근합니다.
/// </summary>
[CreateAssetMenu(menuName = "Mewage/Unit Visual Config", fileName = "UnitVisualConfig")]
public class UnitVisualConfig : ScriptableObject
{
    [Serializable]
    public struct ChapterEntry
    {
        public int      chapter;
        public Sprite[] unitIcons;   // DB_유닛 ForEach 순서와 동일
    }

    public ChapterEntry[] chapters;

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
