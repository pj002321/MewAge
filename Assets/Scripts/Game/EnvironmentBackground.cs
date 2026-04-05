using UnityEngine;

/// <summary>
/// 배경 프리팹 루트에 부착합니다.
/// MovementBounds · SpawnZone · FitToScreen 등은 자식 오브젝트로 포함하고,
/// 이 컴포넌트에서 Enter/Exit 포인트 Transform을 지정합니다.
/// </summary>
public class EnvironmentBackground : MonoBehaviour
{
    [Tooltip("일반→보스 전환 시 플레이어가 걸어 나갈 출구 위치 (일반 배경에 설정)")]
    public Transform bossExitPoint;

    [Tooltip("보스 맵 플레이어 입장 위치 (보스 배경에 설정)")]
    public Transform bossEnterPoint;
}
