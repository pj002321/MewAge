using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 디버그 전용 패널.
/// 토글: 화면 우상단 모서리(tapZoneRatio 비율 영역)를 tapCount회 이내 tapWindow초 안에 탭
/// </summary>
public class DebugPanel : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private GameObject      panel;
    [SerializeField] private Button          skipWaveButton;
    [SerializeField] private Button          bossWaveButton;
    [SerializeField] private TextMeshProUGUI waveLabel;

    [Header("시크릿 탭 설정")]
    [Tooltip("화면 우상단 기준 탭 인식 영역 비율 (0.15 = 화면 15%)")]
    [SerializeField] private float tapZoneRatio = 0.15f;
    [Tooltip("연속 탭 횟수")]
    [SerializeField] private int   tapCount     = 5;
    [Tooltip("연속 탭 인식 시간 창 (초)")]
    [SerializeField] private float tapWindow    = 1.5f;

    private int   currentTaps;
    private float lastTapTime;

    private void Awake()
    {
        if (panel) panel.SetActive(false);

        skipWaveButton?.onClick.AddListener(OnSkipWave);
        bossWaveButton?.onClick.AddListener(OnBossWave);
    }

    private void Update()
    {
        // 에디터에서는 F1도 함께 지원
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F1))
            TogglePanel();
#endif

        DetectSecretTap();

        if (panel != null && panel.activeSelf)
            RefreshLabel();
    }

    private void DetectSecretTap()
    {
        bool tapped = false;

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 pos = Input.mousePosition;
            tapped = IsInTapZone(pos);
        }
#else
        if (Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
                tapped = IsInTapZone(touch.position);
        }
#endif

        if (!tapped) return;

        float now = Time.unscaledTime;
        if (now - lastTapTime > tapWindow)
            currentTaps = 0;

        currentTaps++;
        lastTapTime = now;

        if (currentTaps >= tapCount)
        {
            currentTaps = 0;
            TogglePanel();
        }
    }

    private bool IsInTapZone(Vector2 screenPos)
    {
        float w = Screen.width;
        float h = Screen.height;
        // 우상단 모서리
        return screenPos.x >= w * (1f - tapZoneRatio) &&
               screenPos.y >= h * (1f - tapZoneRatio);
    }

    // ── 버튼 핸들러 ─────────────────────────────────────────────
    private void OnSkipWave()  => GameManager.Shared?.DebugSkipWave();
    private void OnBossWave()  => GameManager.Shared?.DebugJumpToBossWave();

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
