using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 디버그 전용 패널.
/// Inspector: panel(루트 GameObject), skipWaveButton, bosswaveButton, waveLabel
/// 토글 단축키: F1 (에디터 / 개발 빌드에서만 표시)
/// </summary>
public class DebugPanel : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private GameObject      panel;
    [SerializeField] private Button          skipWaveButton;
    [SerializeField] private Button          bossWaveButton;
    [SerializeField] private TextMeshProUGUI waveLabel;

    [Header("토글 키")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;

    private void Awake()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        // 릴리즈 빌드에서는 패널 자체를 비활성화
        gameObject.SetActive(false);
        return;
#endif
        if (panel) panel.SetActive(false);

        skipWaveButton?.onClick.AddListener(OnSkipWave);
        bossWaveButton?.onClick.AddListener(OnBossWave);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            TogglePanel();

        if (panel != null && panel.activeSelf)
            RefreshLabel();
    }

    // ── 버튼 핸들러 ─────────────────────────────────────────────
    private void OnSkipWave()
    {
        GameManager.Shared?.DebugSkipWave();
    }

    private void OnBossWave()
    {
        GameManager.Shared?.DebugJumpToBossWave();
    }

    // ── UI ──────────────────────────────────────────────────────
    private void TogglePanel()
    {
        if (panel == null) return;
        panel.SetActive(!panel.activeSelf);
    }

    private void RefreshLabel()
    {
        if (waveLabel == null || GameManager.Shared == null) return;
        int cur      = GameManager.Shared.CurrentWave;
        int nextBoss = (Mathf.FloorToInt(cur / 10f) + 1) * 10;
        waveLabel.text = $"Wave {cur}  |  Next Boss: {nextBoss}";
    }
}
