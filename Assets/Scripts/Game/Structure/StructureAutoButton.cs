using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 기물 자동 사용 토글 버튼.
/// Inspector: button, onColor, offColor, labelText(옵션), autoOnAnimator(옵션) 연결
/// 씬 시작 시 자동으로 AUTO ON 상태로 시작합니다.
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

    [Header("애니메이션")]
    [SerializeField] private Animator autoOnAnimator; // autoon 클립이 있는 Animator
    [SerializeField] private string   autoOnClip = "autoon";

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

            // 아직 OFF 상태면 자동으로 켜줌
            if (!StructureInventory.Shared.IsAutoMode)
                StructureInventory.Shared.ToggleAuto();
            else
                RefreshVisual(true);
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
        if (StructureInventory.Shared == null) return;
        bool isOn = StructureInventory.Shared.IsAutoMode;
        RefreshVisual(isOn);

        if (autoOnAnimator == null) return;
        if (isOn)
            autoOnAnimator.Play(autoOnClip, 0, 0f);  // 처음부터 재생
        else
            autoOnAnimator.StopPlayback();            // 정지
    }

    private void RefreshVisual(bool isOn)
    {
        if (buttonImage) buttonImage.color = isOn ? onColor : offColor;
        if (labelText)   labelText.text    = isOn ? onLabel : offLabel;
    }
}
