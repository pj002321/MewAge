using UnityEngine;

[CreateAssetMenu(fileName = "StageVisualConfig", menuName = "Mewage/Stage Visual Config")]
public class StageVisualConfig : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public Sprite     icon;
        public GameObject symbolPrefab;
    }

    // DB_챕터 정렬 순서(챕터↑ → 스테이지↑)와 동일한 인덱스
    public Entry[] entries;

    public Entry Get(int index)
    {
        if (entries == null || index < 0 || index >= entries.Length) return null;
        return entries[index];
    }
}
