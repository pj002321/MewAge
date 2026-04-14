using UnityEngine;

/// <summary>
/// 스테이지별 환경 데이터 ScriptableObject.
/// 배경은 프리팹(또는 씬 인스턴스)으로 관리하며,
/// MovementBounds · SpawnZone · FitToScreen · Enter/ExitPoint 는
/// 각 배경 프리팹 내부의 EnvironmentBackground 컴포넌트로 정의합니다.
/// </summary>
[CreateAssetMenu(fileName = "StageEnvironment", menuName = "Game/StageEnvironment")]
public class StageEnvironmentSO : ScriptableObject
{
    [Tooltip("웨이브별 배경 배열 (fromWave 오름차순 권장)")]
    public WaveBackgroundEntry[] waveBackgrounds;

    [Tooltip("보스 웨이브에서 활성화할 배경 오브젝트 (EnvironmentBackground 컴포넌트 포함)")]
    public GameObject bossBackground;

    [Tooltip("일반 환경 카메라가 플레이어를 따라갈지 여부")]
    public bool followPlayer = true;

    [Tooltip("보스 환경 카메라가 플레이어를 따라갈지 여부")]
    public bool bossFollowPlayer;

    [Tooltip("카메라 이동 경계를 MovementBounds에서 자동 적용할지 여부")]
    public bool useBounds = true;

    [System.Serializable]
    public struct WaveBackgroundEntry
    {
        [Tooltip("이 웨이브 이상부터 적용 (1 = 처음부터)")]
        public int        fromWave;
        [Tooltip("활성화할 배경 오브젝트 (EnvironmentBackground 컴포넌트 포함)")]
        public GameObject background;
    }
}
