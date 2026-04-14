using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 디버그 전용 패널.
/// Inspector에서 skipWaveButton, bossWaveButton, waveLabel 연결.
/// jumpButtons[i] 는 jumpWaves[i] 웨이브로 즉시 점프합니다.
/// </summary>
public class DebugPanel : MonoBehaviour
{
    [Header("가시성 토글 (선택)")]
    [SerializeField] private Button          toggleVisibilityButton;
    [SerializeField] private GameObject      buttonContainer;

    [Header("UI 연결")]
    [SerializeField] private Button          skipWaveButton;
    [SerializeField] private Button          bossWaveButton;
    [SerializeField] private TextMeshProUGUI waveLabel;

    private bool isVisible = true;

    [Header("웨이브 점프 버튼 (jumpWaves 와 1:1 대응)")]
    [SerializeField] private Button[] jumpButtons;
    [SerializeField] private int[]    jumpWaves = { 9, 10, 19, 20, 29, 30, 40 };

    private void Awake()
    {
        toggleVisibilityButton?.onClick.AddListener(OnToggleVisibility);

        skipWaveButton?.onClick.AddListener(OnSkipWave);
        bossWaveButton?.onClick.AddListener(OnBossWave);

        for (int i = 0; i < jumpButtons.Length; i++)
        {
            int wave = i < jumpWaves.Length ? jumpWaves[i] : (i + 1) * 10;
            jumpButtons[i]?.onClick.AddListener(() => OnJumpToWave(wave));
        }
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

    public void OnJumpToWave(int wave)
    {
        Debug.Log($"[Debug] JumpToWave {wave} 버튼 클릭");
        GameManager.Shared?.DebugJumpToWave(wave);
    }

    private void OnToggleVisibility()
    {
        isVisible = !isVisible;
        if (buttonContainer != null)
        {
            buttonContainer.SetActive(isVisible);
        }
        else
        {
            if (skipWaveButton) skipWaveButton.gameObject.SetActive(isVisible);
            if (bossWaveButton) bossWaveButton.gameObject.SetActive(isVisible);
            if (jumpButtons != null)
            {
                foreach (var btn in jumpButtons)
                {
                    if (btn) btn.gameObject.SetActive(isVisible);
                }
            }
        }
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
