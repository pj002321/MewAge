using UnityEngine;

public class PopUpUserProfile : MonoBehaviour
{
    private ScalablePopup _popup;
    private ScalablePopup Popup => _popup != null ? _popup : (_popup = GetComponent<ScalablePopup>());

    public void Show() => Popup.Show();
    public void Hide() => Popup.Hide();
}
