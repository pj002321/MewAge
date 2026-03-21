using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 디버그 전용 패널.
/// Inspector에서 skipWaveButton, bossWaveButton, waveLabel 연결.
/// </summary>
public class DebugPanel : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private Button          skipWaveButton;
    [SerializeField] private Button          bossWaveButton;
    [SerializeField] private TextMeshProUGUI waveLabel;

    private void Awake()
    {
        skipWaveButton?.onClick.AddListener(OnSkipWave);
        bossWaveButton?.onClick.AddListener(OnBossWave);
    }

    private void Update()
    {
        RefreshLabel();
        // 키보드 단축키로 버튼 우회 테스트 (F1=SkipWave, F2=BossWave)
        if (Input.GetKeyDown(KeyCode.F1)) OnSkipWave();
        if (Input.GetKeyDown(KeyCode.F2)) OnBossWave();
    }

    public void OnSkipWave()
    {
        Debug.Log($"[Debug] SkipWave 버튼 클릭 — GameManager={GameManager.Shared}");
        GameManager.Shared?.DebugSkipWave();
    }

    public void OnBossWave()
    {
        Debug.Log($"[Debug] BossWave 버튼 클릭 — GameManager={GameManager.Shared}");
        GameManager.Shared?.DebugJumpToBossWave();
    }

    private void RefreshLabel()
    {
        if (waveLabel == null || GameManager.Shared == null) return;
        int cur      = GameManager.Shared.CurrentWave;
        int nextBoss = GameManager.Shared.GetNextBossWave(cur);
        string bossStr = nextBoss > 0 ? nextBoss.ToString() : "없음";
        waveLabel.text = $"Wave {cur}  |  Next Boss: {bossStr}";
    }
}
