using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 홈 씬 디버그 패널.
/// DebugMode PlayerPrefs가 0이면 비활성화.
/// toggleButton  → buttonContainer(디버그 버튼 묶음) 표시/숨김만 담당.
/// menuToggleButton → MainMenuPanel의 잠금 대상 버튼들 활성/비활성 토글.
/// </summary>
public class DebugHomePanel : MonoBehaviour
{
    [Header("가시성 토글 (디버그 버튼 묶음만 제어)")]
    [SerializeField] private Button     toggleButton;
    [SerializeField] private GameObject buttonContainer;

    [Header("MainMenuPanel 참조")]
    [SerializeField] private MainMenuPanel mainMenuPanel;

    [Header("메뉴 버튼 활성/비활성 토글")]
    [SerializeField] private Button          menuToggleButton;
    [SerializeField] private TextMeshProUGUI menuToggleLabel;

    // 씬에서 버튼들이 이미 활성 상태임을 기준으로 초기값 설정
    private bool _contentsVisible = false;
    private bool _menuUnlocked    = true;

    private void Awake()
    {
        if (PlayerPrefs.GetInt("DebugMode", 1) == 0)
        {
            gameObject.SetActive(false);
            return;
        }

        if (buttonContainer != null) buttonContainer.SetActive(_contentsVisible);
        toggleButton?.onClick.AddListener(OnToggleContents);
        menuToggleButton?.onClick.AddListener(OnToggleMenuButtons);
        RefreshMenuLabel();
    }

    private void OnToggleContents()
    {
        _contentsVisible = !_contentsVisible;
        // buttonContainer 자체만 껐다 켜므로 MainMenuPanel 버튼과 무관
        if (buttonContainer != null) buttonContainer.SetActive(_contentsVisible);
    }

    public void OnToggleMenuButtons()
    {
        _menuUnlocked = !_menuUnlocked;
        ApplyMenuButtons();
        RefreshMenuLabel();
    }

    private void ApplyMenuButtons()
    {
        if (mainMenuPanel == null) return;
        Button[] targets =
        {
            mainMenuPanel.storageButton,
            mainMenuPanel.specialGiftButton,
            mainMenuPanel.dailymissionButton,
            mainMenuPanel.achievementButton,
            mainMenuPanel.attendanceButton,
            mainMenuPanel.rankingButton,
            mainMenuPanel.inventoryButton,
        };
        foreach (var btn in targets)
        {
            if (btn != null) btn.gameObject.SetActive(_menuUnlocked);
        }
    }

    private void RefreshMenuLabel()
    {
        if (menuToggleLabel == null) return;
        menuToggleLabel.text = _menuUnlocked ? "메뉴 잠금" : "메뉴 해금";
    }
}
