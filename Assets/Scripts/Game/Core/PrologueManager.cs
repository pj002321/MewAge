using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 프롤로그 씬 컨트롤러.
/// PlayerPrefs "Prologue_Seen" 플래그가 1이면 즉시 home@scene으로 이동.
/// 컷 이미지 + 한 글자씩 타이핑 대사, 탭으로 진행/스킵.
/// </summary>
public class PrologueManager : MonoBehaviour
{
    public  const string SeenKey        = "Prologue_Seen";
    private const float  TypingInterval = 0.04f;
    private const float  PanelFadeTime  = 0.35f;

    [System.Serializable]
    public class PanelData
    {
        public Sprite image;
        [TextArea(2, 6)]
        public string dialogue;
    }

    [Header("컷 데이터 (순서대로 3컷)")]
    [SerializeField] private PanelData[] panels;

    [Header("UI 레퍼런스")]
    [SerializeField] private CanvasGroup      contentGroup;
    [SerializeField] private Image            panelImage;
    [SerializeField] private TextMeshProUGUI  dialogueText;
    [SerializeField] private GameObject       tapHint;
    [SerializeField] private Button           skipButton;
    [SerializeField] private Button           screenButton;

    private int       _index;
    private bool      _isTyping;
    private Coroutine _typingCo;

    private void Awake()
    {
        skipButton.onClick.AddListener(OnSkip);
        screenButton.onClick.AddListener(OnTap);
    }

    private void Start()
    {
        StartCoroutine(ShowPanel(0));
    }

    private IEnumerator ShowPanel(int index)
    {
        if (index >= panels.Length)
        {
            yield return StartCoroutine(EndPrologue());
            yield break;
        }

        // 첫 컷이 아니면 페이드 아웃 후 교체
        if (contentGroup.alpha > 0f)
            yield return StartCoroutine(Fade(contentGroup, 1f, 0f, PanelFadeTime));

        _index            = index;
        panelImage.sprite = panels[index].image;
        panelImage.SetNativeSize();
        dialogueText.text = string.Empty;
        tapHint?.SetActive(false);

        yield return StartCoroutine(Fade(contentGroup, 0f, 1f, PanelFadeTime));

        if (_typingCo != null) StopCoroutine(_typingCo);
        _typingCo = StartCoroutine(TypeText(panels[index].dialogue));
    }

    private IEnumerator TypeText(string text)
    {
        _isTyping = true;
        dialogueText.text = string.Empty;
        foreach (char c in text)
        {
            dialogueText.text += c;
            yield return new WaitForSeconds(TypingInterval);
        }
        _isTyping = false;
        tapHint?.SetActive(true);
    }

    private void OnTap()
    {
        if (_isTyping)
        {
            if (_typingCo != null) StopCoroutine(_typingCo);
            _isTyping         = false;
            dialogueText.text = panels[_index].dialogue;
            tapHint?.SetActive(true);
        }
        else
        {
            StartCoroutine(ShowPanel(_index + 1));
        }
    }

    private void OnSkip() => MarkSeenAndLoad();

    private IEnumerator EndPrologue()
    {
        yield return StartCoroutine(Fade(contentGroup, 1f, 0f, PanelFadeTime));
        MarkSeenAndLoad();
    }

    private void MarkSeenAndLoad()
    {
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
        SceneLoadManager.LoadScene("home@scene");
    }

    private static IEnumerator Fade(CanvasGroup cg, float from, float to, float duration)
    {
        float t = 0f;
        cg.alpha = from;
        while (t < duration)
        {
            t        += Time.deltaTime;
            cg.alpha  = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        cg.alpha = to;
    }
}
