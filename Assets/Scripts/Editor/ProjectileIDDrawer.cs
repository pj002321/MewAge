using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [ProjectileID] 속성이 붙은 string 필드를 Resources/Unit/Ch*/proj/ 드롭다운으로 표시합니다.
/// 프로젝트 에셋이 변경되면 캐시가 자동으로 초기화됩니다.
/// </summary>
[CustomPropertyDrawer(typeof(ProjectileIDAttribute))]
public class ProjectileIDDrawer : PropertyDrawer
{
    private static string[] _paths;
    private static string[] _labels;

    [InitializeOnLoadMethod]
    private static void RegisterAutoRefresh()
    {
        EditorApplication.projectChanged -= ClearCache;
        EditorApplication.projectChanged += ClearCache;
    }

    private static void ClearCache() => _paths = _labels = null;

    private static void EnsureCache()
    {
        if (_paths != null) return;

        var paths  = new List<string>();
        var labels = new List<string>();

        paths.Add("");
        labels.Add("(없음)");

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Unit" });
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);

            if (assetPath.IndexOf("/proj/", System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            const string prefix = "Assets/Resources/";
            if (!assetPath.StartsWith(prefix)) continue;

            string resPath = assetPath.Substring(prefix.Length);
            if (resPath.EndsWith(".prefab"))
                resPath = resPath.Substring(0, resPath.Length - 7);

            string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            string[] parts  = resPath.Split('/');
            string chapter  = parts.Length >= 2 ? parts[1] : "";

            paths.Add(resPath);
            labels.Add($"[{chapter}]  {fileName}");
        }

        _paths  = paths.ToArray();
        _labels = labels.ToArray();
    }

    [MenuItem("Tools/Mewage/Refresh ProjectileID Cache")]
    private static void RefreshCache()
    {
        ClearCache();
        EnsureCache();
        Debug.Log($"[ProjectileIDDrawer] 캐시 갱신 완료 — {_paths.Length - 1}개 투사체 프리팹");
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.HelpBox(position, "[ProjectileID] 는 string 필드에만 사용 가능합니다.", MessageType.Error);
            return;
        }

        EnsureCache();

        EditorGUI.BeginProperty(position, label, property);

        string current    = property.stringValue;
        int    currentIdx = 0;
        for (int i = 1; i < _paths.Length; i++)
        {
            if (_paths[i] == current) { currentIdx = i; break; }
        }

        const float BtnW = 24f;
        var dropRect = new Rect(position.x, position.y, position.width - BtnW - 2f, position.height);
        var btnRect  = new Rect(position.xMax - BtnW, position.y, BtnW, position.height);

        int selected = EditorGUI.Popup(dropRect, label.text, currentIdx, _labels);
        if (selected != currentIdx)
            property.stringValue = selected == 0 ? "" : _paths[selected];

        if (GUI.Button(btnRect, "↻"))
        {
            ClearCache();
            EnsureCache();
        }

        EditorGUI.EndProperty();
    }
}
