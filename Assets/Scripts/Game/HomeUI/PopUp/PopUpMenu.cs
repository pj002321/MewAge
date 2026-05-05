using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PopUpMenu : MonoBehaviour
{
    [SerializeField] private Button debugToggleButton;

    private ScalablePopup _popup;
    private ScalablePopup Popup => _popup != null ? _popup : (_popup = GetComponent<ScalablePopup>());

    private void Awake()
    {
        debugToggleButton?.onClick.AddListener(OnDebugToggle);
    }

    private void OnDebugToggle()
    {
        DebugPanel.Shared?.Toggle();
        RefreshDebugLabel();
    }

    private void RefreshDebugLabel()
    {
        var label = debugToggleButton?.GetComponentInChildren<TextMeshProUGUI>();
        if (label == null) return;
        bool visible = DebugPanel.Shared?.IsVisible ?? false;
        label.text = visible ? "디버그 OFF" : "디버그 ON";
    }

    public void Show() => Popup.Show();
    public void Hide() => Popup.Hide();
}
