using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mewage.DB;

/// <summary>
/// ImprovPanel 안의 증강 카드 하나.
/// Inspector 연결: iconImg, nameText, descText, button
/// </summary>
public class ImprovCard : MonoBehaviour
{
    [SerializeField] private Image           iconImg;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descText;
    [SerializeField] private Button          button;

    public DB_증강 Data { get; private set; }

    private System.Action<DB_증강> onClick;

    private void Awake()
    {
        button?.onClick.AddListener(() =>
        {
            if (Data != null) onClick?.Invoke(Data);
        });
    }

    public void Setup(DB_증강 data, System.Action<DB_증강> onClickCallback)
    {
        Data    = data;
        onClick = onClickCallback;

        if (iconImg)
        {
            var sprite = data.f_아이콘;
            iconImg.sprite  = sprite;
            iconImg.enabled = sprite != null;
        }

        if (nameText) nameText.text = data.f_이름;
        if (descText)
        {
            string desc = data.f_설명;
            if (!string.IsNullOrEmpty(desc))
            {
                desc = desc.Replace("{0}", data.f_주수치.ToString());
                desc = desc.Replace("{1}", data.f_보조수치.ToString());
            }
            descText.text = desc ?? string.Empty;
        }

        if (button) button.interactable = true;
        gameObject.SetActive(true);
    }

    public void SetEmpty()
    {
        Data = null;
        gameObject.SetActive(false);
    }
}
