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
    private Vector3[] tabOriginalScales;
    private Color[]   tabOriginalColors;

    void Start()
    {
        var structwareMgr = StructureWareManager.Shared;
        Debug.Log($"[HomeUIManager] StructureWareManager 로드: {(structwareMgr != null ? "성공" : "실패")}");

        // 탭 버튼 원본 스케일·색상 캡처
        tabOriginalScales = new Vector3[tabButtons.Length];
        tabOriginalColors = new Color[tabButtons.Length];
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;
            tabOriginalScales[i] = tabButtons[i].transform.localScale;
            var img = tabButtons[i].GetComponent<Image>();
            if (img != null) tabOriginalColors[i] = img.color;
        }

        for (int i = 0; i < tabButtons.Length; i++)
        {
            int index = i;
            tabButtons[i].onClick.AddListener(() => ShowTab(index));
        }

        // 전투 탭 기본
        ShowTab(2);
    }

    public void ShowTab(int index)
    {
        if (index < 0 || index >= panels.Length) return;

        currentTab = index;

        // 패널 활성화만 관리 (트랜스폼은 건드리지 않음)
        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null)
                panels[i].SetActive(i == index);
        }

        // 활성 탭 강조
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;

            var img = tabButtons[i].GetComponent<Image>();
            if (img != null && i == index)
                img.color = activeColor;
            else if (img != null && tabOriginalColors != null)
                img.color = tabOriginalColors[i];

            tabButtons[i].transform.localScale = tabOriginalScales != null
                ? tabOriginalScales[i] * (i == index ? 1.1f : 1.0f)
                : Vector3.one;
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
