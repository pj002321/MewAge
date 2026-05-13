using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "Mewage/Text Style Preset", fileName = "TextStylePreset")]
public class TextStylePreset : ScriptableObject
{
    [Header("폰트")]
    public TMP_FontAsset font;
    public FontStyles    fontStyle = FontStyles.Normal;

    [Header("Face")]
    public bool  overrideFaceColor = false;
    public Color faceColor         = Color.white;
    [Range(-1f, 1f)] public float faceDilate   = 0f;
    [Range(0f,  1f)] public float faceSoftness = 0f;

    [Header("Outline")]
    public bool  useOutline       = false;
    public Color outlineColor     = Color.black;
    [Range(0f, 1f)] public float outlineThickness = 0f;

    [Header("Underlay")]
    public bool            useUnderlay    = false;
    public UnderlayType    underlayType   = UnderlayType.Normal;
    public Color           underlayColor  = Color.black;
    [Range(-1f, 1f)] public float underlayOffsetX = 0f;
    [Range(-1f, 1f)] public float underlayOffsetY = 0f;
    [Range(-1f, 1f)] public float underlayDilate  = 0f;
    [Range(0f,  1f)] public float underlaySoftness = 0f;

    [Header("버텍스 그라디언트")]
    public bool              useVertexGradient    = false;
    public TMP_ColorGradient vertexGradientPreset = null;
    public VertexGradient    vertexGradient        = new VertexGradient(Color.white);

    // ──────────────────────────────────────────────────────────────

    static int _idFaceDilate, _idFaceSoftness;
    static int _idOutlineColor, _idOutlineWidth;
    static int _idUnderlayColor, _idUnderlayOffsetX, _idUnderlayOffsetY, _idUnderlayDilate, _idUnderlaySoft;
    static bool _idsReady;

    static void EnsureIDs()
    {
        if (_idsReady) return;
        _idFaceDilate      = Shader.PropertyToID("_FaceDilate");
        _idFaceSoftness    = Shader.PropertyToID("_FaceSoftness");
        _idOutlineColor    = ShaderUtilities.ID_OutlineColor;
        _idOutlineWidth    = ShaderUtilities.ID_OutlineWidth;
        _idUnderlayColor   = ShaderUtilities.ID_UnderlayColor;
        _idUnderlayOffsetX = ShaderUtilities.ID_UnderlayOffsetX;
        _idUnderlayOffsetY = ShaderUtilities.ID_UnderlayOffsetY;
        _idUnderlayDilate  = ShaderUtilities.ID_UnderlayDilate;
        _idUnderlaySoft    = ShaderUtilities.ID_UnderlaySoftness;
        _idsReady = true;
    }

    public void Apply(TMP_Text text)
    {
        if (text == null) return;
        EnsureIDs();

        if (font != null) text.font = font;
        text.fontStyle = fontStyle;

        if (overrideFaceColor) text.color = faceColor;

        // fontMaterial: 공유 머티리얼이면 인스턴스 복사본을 반환, 이미 인스턴스면 그대로 반환
        var mat = text.fontMaterial;

        // Face
        mat.SetFloat(_idFaceDilate,   faceDilate);
        mat.SetFloat(_idFaceSoftness, faceSoftness);

        // Outline
        if (useOutline)
        {
            mat.EnableKeyword(ShaderUtilities.Keyword_Outline);
            mat.SetColor(_idOutlineColor, outlineColor);
            mat.SetFloat(_idOutlineWidth, outlineThickness);
        }
        else
        {
            mat.DisableKeyword(ShaderUtilities.Keyword_Outline);
            mat.SetFloat(_idOutlineWidth, 0f);
        }

        // Underlay
        if (useUnderlay)
        {
            if (underlayType == UnderlayType.Normal)
            {
                mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                mat.DisableKeyword("UNDERLAY_INNER");
            }
            else
            {
                mat.DisableKeyword(ShaderUtilities.Keyword_Underlay);
                mat.EnableKeyword("UNDERLAY_INNER");
            }
            mat.SetColor(_idUnderlayColor,   underlayColor);
            mat.SetFloat(_idUnderlayOffsetX, underlayOffsetX);
            mat.SetFloat(_idUnderlayOffsetY, underlayOffsetY);
            mat.SetFloat(_idUnderlayDilate,  underlayDilate);
            mat.SetFloat(_idUnderlaySoft,    underlaySoftness);
        }
        else
        {
            mat.DisableKeyword(ShaderUtilities.Keyword_Underlay);
            mat.DisableKeyword("UNDERLAY_INNER");
        }

        // Vertex Gradient
        text.enableVertexGradient = useVertexGradient;
        if (useVertexGradient)
        {
            if (vertexGradientPreset != null)
                text.colorGradientPreset = vertexGradientPreset;
            else
            {
                text.colorGradientPreset = null;
                text.colorGradient       = vertexGradient;
            }
        }
        else
        {
            text.colorGradientPreset = null;
        }
    }

    public enum UnderlayType { Normal, Inner }

#if UNITY_EDITOR
    private void OnValidate()
    {
        var appliers = Object.FindObjectsByType<TextStyleApplier>(FindObjectsSortMode.None);
        foreach (var a in appliers)
            if (a.preset == this) a.Apply();
    }
#endif
}
