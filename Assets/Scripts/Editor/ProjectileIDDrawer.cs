using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [ProjectileID] 속성이 붙은 string 필드를 proj/ 폴더 드롭다운으로 표시합니다.
/// rootFolder가 "Unit"이면 Resources/Unit, "Weapons"이면 Resources/Weapons 탐색.
/// 프로젝트 에셋이 변경되면 캐시가 자동으로 초기화됩니다.
/// </summary>
[CustomPropertyDrawer(typeof(ProjectileIDAttribute))]
public class ProjectileIDDrawer : PropertyDrawer
{
    private static readonly Dictionary<string, (string[] paths, string[] labels)> _cache = new();

    [InitializeOnLoadMethod]
    private static void RegisterAutoRefresh()
    {
        EditorApplication.projectChanged -= ClearCache;
        EditorApplication.projectChanged += ClearCache;
    }

    private static void ClearCache() => _cache.Clear();

    private static (string[] paths, string[] labels) EnsureCache(string rootFolder)
    {
        if (_cache.TryGetValue(rootFolder, out var cached)) return cached;

        var paths  = new List<string> { "" };
        var labels = new List<string> { "(없음)" };

        string[] guids = AssetDatabase.FindAssets("t:Prefab",
            new[] { $"Assets/Resources/{rootFolder}" });
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

        var result = (paths.ToArray(), labels.ToArray());
        _cache[rootFolder] = result;
        return result;
    }

    [MenuItem("Tools/Mewage/Refresh ProjectileID Cache")]
    private static void RefreshCache()
    {
        ClearCache();
        Debug.Log("[ProjectileIDDrawer] 캐시 초기화 완료");
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.HelpBox(position, "[ProjectileID] 는 string 필드에만 사용 가능합니다.", MessageType.Error);
            return;
        }

        string rootFolder = (attribute as ProjectileIDAttribute)?.RootFolder ?? "Unit";
        var (paths, labels) = EnsureCache(rootFolder);

        EditorGUI.BeginProperty(position, label, property);

        string current    = property.stringValue;
        int    currentIdx = 0;
        for (int i = 1; i < paths.Length; i++)
        {
            if (paths[i] == current) { currentIdx = i; break; }
        }

        const float BtnW = 24f;
        var dropRect = new Rect(position.x, position.y, position.width - BtnW - 2f, position.height);
        var btnRect  = new Rect(position.xMax - BtnW, position.y, BtnW, position.height);

        int selected = EditorGUI.Popup(dropRect, label.text, currentIdx, labels);
        if (selected != currentIdx)
            property.stringValue = selected == 0 ? "" : paths[selected];

        if (GUI.Button(btnRect, "↻"))
        {
            ClearCache();
            EnsureCache(rootFolder);
        }

        EditorGUI.EndProperty();
    }
}
