using UnityEngine;

/// <summary>
/// 증강 테이블 ScriptableObject.
/// Assets 우클릭 → Create → MuAge → 증강 테이블 로 생성 후
/// Inspector에서 entries 배열에 항목을 작성합니다.
/// </summary>
[CreateAssetMenu(menuName = "MuAge/증강 테이블", fileName = "ImprovTable")]
public class ImprovTable : ScriptableObject
{
    public ImprovData[] entries;
}
