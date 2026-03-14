using UnityEngine;

/// <summary>
/// 증강 항목 하나의 데이터.
/// description에 "{0}" 포함 시 value로 치환됩니다.
/// 예) "체력 {0} 회복" → "체력 20 회복"
/// </summary>
[System.Serializable]
public class ImprovData
{
    public string    id;
    public string    improvName;
    [TextArea]
    public string    description;
    public ImprovType type;
    public float     value;
    public Sprite    icon;
}
