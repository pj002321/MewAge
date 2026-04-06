using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class HomeUIManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject[] panels; // 0:상점, 1:대원, 2:전투, 3:장비, 4:기물

    [Header("Tab Buttons")]
    public Button[] tabButtons;

    [Header("User Info")]
    public HomeUserInfoBar userInfoBar;

    [Header("Tab Colors")]
    public Color activeColor = new Color(1f, 0.8f, 0.2f, 1f);
    public Color inactiveColor = new Color(0.5f, 0.5f, 0.5f, 1f);

    private int currentTab = -1;
    private Vector3[] originalScales;

    void Start()
    {
        // StructureWareManager를 명시적으로 로드 (홈 씬에서 필요함)
        var structwareMgr = StructureWareManager.Shared;
        Debug.Log($"[HomeUIManager] StructureWareManager 로드: {(structwareMgr != null ? "성공" : "실패")}");

        // Store original scales
        originalScales = new Vector3[tabButtons.Length];
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] != null)
                originalScales[i] = tabButtons[i].transform.localScale;
        }

        // Register button click events
        for (int i = 0; i < tabButtons.Length; i++)
        {
            int index = i;
            tabButtons[i].onClick.AddListener(() => ShowTab(index));
        }

        // Default: show 전투 (index 2)
        ShowTab(2);
    }

    public void ShowTab(int index)
    {
        if (index < 0 || index >= panels.Length) return;

        currentTab = index;

        // Hide all panels
        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null)
                panels[i].SetActive(i == index);
        }

        // Highlight active tab button and adjust scales
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] != null)
            {
                var img = tabButtons[i].GetComponent<Image>();
                if (img != null && i == index)
                    img.color = activeColor;

                // Scale up active button, restore others to original
                float scaleFactor = (i == index) ? 1.1f : 1.0f;
                tabButtons[i].transform.localScale = originalScales[i] * scaleFactor;
            }
        }
    }

    public void StartGame()
    {
        // StageSelectPanel의 현재 선택 스테이지를 저장 후 씬 전환
        var stagePanel = FindFirstObjectByType<StageSelectPanel>();
        if (stagePanel != null)
        {
            stagePanel.OnStartClicked();
            return;
        }
        // StageSelectPanel이 없으면 저장된 값으로 그냥 시작
        SceneLoadManager.LoadScene("stage@scene");
    }
}
