using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 일시정지 버튼 컴포넌트. 이 스크립트를 일시정지 버튼 GameObject에 붙입니다.
///
/// Inspector 연결:
///   pausePanel    — 일시정지 시 활성화할 패널 GameObject
///   resumeButton  — 게임 재개 버튼
///   retreatButton — 홈 씬으로 후퇴 버튼
/// </summary>
[RequireComponent(typeof(Button))]
public class PauseButton : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button     resumeButton;
    [SerializeField] private Button     retreatButton;

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(OpenPause);
        resumeButton ?.onClick.AddListener(ClosePause);
        retreatButton?.onClick.AddListener(Retreat);

        pausePanel?.SetActive(false);
    }

    public void OpenPause()
    {
        pausePanel?.SetActive(true);
        Time.timeScale = 0f;
    }

    public void ClosePause()
    {
        pausePanel?.SetActive(false);
        Time.timeScale = 1f;
    }

    public void Retreat()
    {
        Time.timeScale = 1f;
        if (GameManager.Shared != null)
            GameManager.Shared.GoHome();
        else
            SceneLoadManager.LoadScene("home@scene");
    }
}
