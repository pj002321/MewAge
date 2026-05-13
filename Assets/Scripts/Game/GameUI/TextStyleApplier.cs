using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class TextStyleApplier : MonoBehaviour
{
    [SerializeField] public TextStylePreset preset;

    [Header("로컬 오버라이드 (프리셋보다 우선)")]
    [SerializeField] private bool  overrideFaceColor;
    [SerializeField] private Color localFaceColor    = Color.white;

    [SerializeField] private bool  overrideOutlineColor;
    [SerializeField] private Color localOutlineColor = Color.black;

    [SerializeField] private bool  overrideUnderlayColor;
    [SerializeField] private Color localUnderlayColor = Color.black;

    [SerializeField] private bool          overrideVertexGradient;
    [SerializeField] private VertexGradient localVertexGradient = new VertexGradient(Color.white);

    private TMP_Text _text;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
        Apply();
    }

    private void OnEnable()  => FontManager.Register(this);
    private void OnDisable() => FontManager.Unregister(this);

    private void OnValidate() => Apply();

    public void Apply()
    {
        if (_text == null) _text = GetComponent<TMP_Text>();
        if (_text == null) return;

        preset?.Apply(_text);

        if (overrideFaceColor)
            _text.color = localFaceColor;

        if (overrideOutlineColor)
            _text.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, localOutlineColor);

        if (overrideUnderlayColor)
            _text.fontMaterial.SetColor(ShaderUtilities.ID_UnderlayColor, localUnderlayColor);

        if (overrideVertexGradient)
        {
            _text.enableVertexGradient = true;
            _text.colorGradientPreset  = null;
            _text.colorGradient        = localVertexGradient;
        }
    }
}
