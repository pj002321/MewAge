using UnityEngine;

/// <summary>
/// 테스트씬 전용 부트스트랩. GameManager 없이 플레이어·무기·조이스틱만 초기화합니다.
/// GameManager 대신 이 컴포넌트를 씬에 배치하세요.
/// </summary>
public class TestSceneBootstrap : MonoBehaviour
{
    private void Start()
    {
        // 조이스틱 활성화
        if (VirtualJoystick.Instance != null)
            VirtualJoystick.Instance.gameObject.SetActive(true);

        // 플레이어 스탯 초기화 (Inspector 기본값 그대로 적용)
        var sm = PlayerStatManager.Shared;
        if (sm != null)
        {
            sm.ResetMods();
            PlayerController.Instance?.ApplyStatSheet(sm);
        }

        // Time.timeScale 보정 (다른 씬에서 0으로 남아있을 경우 대비)
        Time.timeScale = 1f;
    }
}
