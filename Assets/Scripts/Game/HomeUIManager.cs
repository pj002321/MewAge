using System.Collections;
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

    [Header("Tab Width (탕탕특공대 스타일)")]
    [SerializeField] private float activeWidthMult   = 1.5f;   // 선택된 탭 너비 배율
    [SerializeField] private float inactiveWidthMult = 0.85f;  // 나머지 탭 너비 배율
    [SerializeField] private float widthAnimTime     = 0.15f;  // 전환 시간(초)

    private int currentTab = -1;
    private float[]      tabOriginalWidths;
    private Color[]      tabOriginalColors;
    private Coroutine[]  _widthCoroutines;
    private LayoutElement[] _layoutElements;

    void Start()
    {
        var structwareMgr = StructureWareManager.Shared;
        Debug.Log($"[HomeUIManager] StructureWareManager 로드: {(structwareMgr != null ? "성공" : "실패")}");

        int count = tabButtons.Length;
        tabOriginalWidths = new float[count];
        tabOriginalColors = new Color[count];
        _widthCoroutines  = new Coroutine[count];
        _layoutElements   = new LayoutElement[count];

        for (int i = 0; i < count; i++)
        {
            if (tabButtons[i] == null) continue;

            // 색상 캡처
            var img = tabButtons[i].GetComponent<Image>();
            if (img != null) tabOriginalColors[i] = img.color;

            // LayoutElement 확보 (없으면 추가)
            var le = tabButtons[i].GetComponent<LayoutElement>();
            if (le == null) le = tabButtons[i].gameObject.AddComponent<LayoutElement>();
            _layoutElements[i] = le;

            // 현재 RectTransform 너비를 원본으로 캡처
            var rt = tabButtons[i].GetComponent<RectTransform>();
            float baseW = rt != null ? rt.rect.width : 100f;
            // LayoutElement에 이미 preferredWidth가 있으면 그걸 사용
            tabOriginalWidths[i] = (le.preferredWidth > 0f) ? le.preferredWidth : baseW;
            le.preferredWidth = tabOriginalWidths[i];
        }

        for (int i = 0; i < count; i++)
        {
            int index = i;
            if (tabButtons[i] != null)
                tabButtons[i].onClick.AddListener(() => ShowTab(index));
        }

        // 전투 탭 기본
        ShowTab(2);
    }

    public void ShowTab(int index)
    {
        if (index < 0 || index >= panels.Length) return;

        currentTab = index;

        // 패널 활성화 + 위치·스케일 초기화
        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] == null) continue;
            panels[i].SetActive(i == index);
            if (i == index)
            {
                var rt = panels[i].GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = Vector2.zero;
                    rt.localScale       = Vector3.one;
                }
            }
        }

        // 활성 탭 강조 (색상 + preferredWidth 애니메이션)
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] == null) continue;

            // 색상
            var img = tabButtons[i].GetComponent<Image>();
            if (img != null)
                img.color = (i == index) ? activeColor : tabOriginalColors[i];

            // 너비
            float targetW = tabOriginalWidths[i] * ((i == index) ? activeWidthMult : inactiveWidthMult);
            if (_widthCoroutines[i] != null) StopCoroutine(_widthCoroutines[i]);
            _widthCoroutines[i] = StartCoroutine(LerpWidth(_layoutElements[i], targetW, widthAnimTime));
        }
    }

    private IEnumerator LerpWidth(LayoutElement le, float to, float duration)
    {
        float from    = le.preferredWidth;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            le.preferredWidth = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        le.preferredWidth = to;
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
