using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 기물 자동 사용 토글 버튼.
/// Inspector: button, onColor, offColor, labelText(옵션) 연결
/// </summary>
public class StructureAutoButton : MonoBehaviour
{
    [SerializeField] private Button   button;
    [SerializeField] private Image    buttonImage;
    [SerializeField] private TMP_Text labelText;

    [Header("색상")]
    [SerializeField] private Color onColor  = new Color(0.3f, 0.85f, 0.3f, 1f);
    [SerializeField] private Color offColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    [Header("텍스트")]
    [SerializeField] private string onLabel  = "AUTO ON";
    [SerializeField] private string offLabel = "AUTO OFF";

    private bool subscribed;

    private void Awake()
    {
        button?.onClick.AddListener(OnClick);
        RefreshVisual(false);
    }

    private void Update()
    {
        if (!subscribed && StructureInventory.Shared != null)
        {
            StructureInventory.Shared.OnAutoChanged += OnAutoChanged;
            subscribed = true;
            RefreshVisual(StructureInventory.Shared.IsAutoMode);
        }
    }

    private void OnDisable()
    {
        if (subscribed && StructureInventory.Shared != null)
            StructureInventory.Shared.OnAutoChanged -= OnAutoChanged;
        subscribed = false;
    }

    private void OnClick()
    {
        SoundManager.Shared?.Play(SoundKey.UI_Click);
        StructureInventory.Shared?.ToggleAuto();
    }

    private void OnAutoChanged()
    {
        if (StructureInventory.Shared != null)
            RefreshVisual(StructureInventory.Shared.IsAutoMode);
    }

    private void RefreshVisual(bool isOn)
    {
        if (buttonImage) buttonImage.color = isOn ? onColor : offColor;
        if (labelText)   labelText.text    = isOn ? onLabel : offLabel;
    }
}
