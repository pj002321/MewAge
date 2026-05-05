using UnityEngine;
using UnityEngine.UI;

public class PopUpMenu : MonoBehaviour
{
    [SerializeField] private Button         debugToggleButton;
    [SerializeField] private DebugHomePanel debugHomePanel;
    [SerializeField] private DebugPanel     debugPanel;

    private ScalablePopup _popup;
    private ScalablePopup Popup => _popup != null ? _popup : (_popup = GetComponent<ScalablePopup>());

    private void Awake()
    {
        debugToggleButton?.onClick.AddListener(ToggleDebugPanels);
    }

    public void Show() => Popup.Show();
    public void Hide() => Popup.Hide();

    private void ToggleDebugPanels()
    {
        if (debugHomePanel != null)
        {
            bool next = !debugHomePanel.gameObject.activeSelf;
            debugHomePanel.gameObject.SetActive(next);
        }
        if (debugPanel != null)
        {
            bool next = !debugPanel.gameObject.activeSelf;
            debugPanel.gameObject.SetActive(next);
        }
    }
}
