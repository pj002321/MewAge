using System.Collections.Generic;
using Mewage.DB;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [EquipID] 속성이 붙은 string 필드를 DB_장비 드롭다운으로 표시합니다.
/// </summary>
[CustomPropertyDrawer(typeof(EquipIDAttribute))]
public class EquipIDDrawer : PropertyDrawer
{
    private static string[] _ids;
    private static string[] _labels;   // "이름 (ID)" 형식으로 표시

    private static void EnsureCache()
    {
        if (_ids != null) return;

        var ids    = new List<string>();
        var labels = new List<string>();

        ids.Add("");
        labels.Add("(없음)");

        DB_장비.ForEachEntity(row =>
        {
            if (string.IsNullOrEmpty(row.f_장비ID)) return;
            ids.Add(row.f_장비ID);
            labels.Add($"{row.f_이름}  [{row.f_장비ID}]");
        });

        _ids    = ids.ToArray();
        _labels = labels.ToArray();
    }

    [MenuItem("Tools/Mewage/Refresh EquipID Cache")]
    private static void RefreshCache()
    {
        _ids = _labels = null;
        EnsureCache();
        Debug.Log($"[EquipIDDrawer] 캐시 갱신 완료 — {_ids.Length - 1}개 장비 로드");
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.HelpBox(position, "[EquipID] 는 string 필드에만 사용 가능합니다.", MessageType.Error);
            return;
        }

        EnsureCache();

        EditorGUI.BeginProperty(position, label, property);

        string current    = property.stringValue;
        int    currentIdx = 0;
        for (int i = 1; i < _ids.Length; i++)
        {
            if (_ids[i] == current) { currentIdx = i; break; }
        }

        int selected = EditorGUI.Popup(position, label.text, currentIdx, _labels);
        if (selected != currentIdx)
            property.stringValue = selected == 0 ? "" : _ids[selected];

        EditorGUI.EndProperty();
    }
}
