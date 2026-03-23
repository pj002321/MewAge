using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EnemySpawnEntry
{
    [Tooltip("에디터 표시용 이름")]
    public string     displayName = "새 적";
    public GameObject prefab;
    [Range(1, 100), Tooltip("스폰 가중치 (높을수록 자주 등장)")]
    public int        weight   = 10;
    [Tooltip("이 적이 등장하기 시작하는 최소 웨이브")]
    public int        minWave  = 1;
    [Tooltip("0 = 제한 없음")]
    public int        maxWave  = 0;
}

/// <summary>
/// 특정 웨이브부터 적용되는 스폰 규칙.
/// fromWave 이하의 가장 큰 값이 선택됩니다.
/// </summary>
[System.Serializable]
public class WaveSpawnRule
{
    [Tooltip("이 웨이브부터 규칙 적용")]
    public int   fromWave   = 1;
    [Tooltip("스폰 간격 (초)")]
    public float interval   = 3f;
    [Tooltip("한 번에 스폰할 최대 수")]
    public int   spawnCount = 3;
    [Tooltip("이 웨이브가 보스 웨이브인지 여부")]
    public bool  isBossWave = false;
}

/// <summary>
/// 챕터 1개의 스폰 구성을 담는 ScriptableObject.
/// Assets/Data 폴더에 인스턴스를 저장합니다.
/// MuAge/Enemy Preset Editor 에서 편집하세요.
/// </summary>
[CreateAssetMenu(menuName = "MuAge/Chapter Roster", fileName = "Chapter_1_Roster")]
public class ChapterRosterSO : ScriptableObject
{
    public int    chapter     = 1;
    public string chapterName = "챕터 1";

    [Tooltip("일반 적 스폰 목록")]
    public List<EnemySpawnEntry> normalEnemies = new();

    [Tooltip("보스 프리팹")]
    public GameObject bossPrefab;

    [Tooltip("웨이브별 스폰 규칙 (fromWave 오름차순 정렬 권장)")]
    public List<WaveSpawnRule> waveRules = new();

    /// <summary>현재 웨이브에 해당하는 스폰 규칙 반환. 없으면 null.</summary>
    public WaveSpawnRule GetRuleForWave(int wave)
    {
        WaveSpawnRule matched = null;
        foreach (var r in waveRules)
            if (r.fromWave <= wave && (matched == null || r.fromWave > matched.fromWave))
                matched = r;
        return matched;
    }
}
