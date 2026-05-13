using UnityEditor;
using UnityEngine;

public static class FontManagerEditor
{
    [MenuItem("Tools/Apply All Text Styles")]
    private static void ApplyAll()
    {
        var appliers = Object.FindObjectsByType<TextStyleApplier>(FindObjectsSortMode.None);
        foreach (var a in appliers)
        {
            a.Apply();
            EditorUtility.SetDirty(a.gameObject);
        }
        Debug.Log($"[FontManager] {appliers.Length}개 TextStyleApplier 적용 완료.");
    }
}
