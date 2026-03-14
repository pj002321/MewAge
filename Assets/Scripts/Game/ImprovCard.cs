using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ImprovPanel 안의 증강 카드 하나.
/// Inspector 연결: iconImg, nameText, descText, button, selectedOverlay(선택적), canvasGroup(선택적)
/// </summary>
public class ImprovCard : MonoBehaviour
{
    [SerializeField] private Image            iconImg;
    [SerializeField] private TextMeshProUGUI  nameText;
    [SerializeField] private TextMeshProUGUI  descText;
    [SerializeField] private Button           button;
    [SerializeField] private GameObject       selectedOverlay; // 선택된 카드에 표시할 오버레이 (선택적)
    [SerializeField] private CanvasGroup      canvasGroup;     // 비선택 카드 흐리게 (선택적)

    public ImprovData Data { get; private set; }

    private System.Action<ImprovData> onClick;

    private void Awake()
    {
        button?.onClick.AddListener(() =>
        {
            if (Data != null) onClick?.Invoke(Data);
        });
    }

    // ────────────────────────────────────────────────────────────────
    // 초기화
    // ────────────────────────────────────────────────────────────────
    public void Setup(ImprovData data, System.Action<ImprovData> onClickCallback)
    {
        Data    = data;
        onClick = onClickCallback;

        if (iconImg)
        {
            iconImg.sprite  = data.icon;
            iconImg.enabled = data.icon != null;
        }
        if (nameText) nameText.text = data.improvName;
        if (descText)
        {
            descText.text = (data.description != null && data.description.Contains("{0}"))
                ? string.Format(data.description, data.value)
                : data.description;
        }

        if (selectedOverlay) selectedOverlay.SetActive(false);
        if (canvasGroup)     { canvasGroup.alpha = 1f; canvasGroup.interactable = true; }
        if (button)          button.interactable = true;
        gameObject.SetActive(true);
    }

    public void SetEmpty()
    {
        Data = null;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 선택 후 호출 — 선택된 카드(true)는 하이라이트, 나머지(false)는 흐리게
    /// </summary>
    public void SetInteractable(bool isSelected)
    {
        if (selectedOverlay) selectedOverlay.SetActive(isSelected);
        if (canvasGroup)
        {
            canvasGroup.alpha        = isSelected ? 1f : 0.4f;
            canvasGroup.interactable = isSelected;
        }
        if (button) button.interactable = isSelected;
    }
}
