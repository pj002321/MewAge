using System.Collections.Generic;
using Mewage.DB;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [SynergyID] 속성이 붙은 string 필드를 DB_시너지 드롭다운으로 표시합니다.
/// </summary>
[CustomPropertyDrawer(typeof(SynergyIDAttribute))]
public class SynergyIDDrawer : PropertyDrawer
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

        DB_시너지.ForEachEntity(row =>
        {
            ids.Add(row.f_시너지ID);
            labels.Add($"{row.f_이름}  [{row.f_시너지ID}]");
        });

        _ids    = ids.ToArray();
        _labels = labels.ToArray();
    }

    /// <summary>DB가 변경된 경우 에디터 메뉴에서 캐시를 수동으로 갱신합니다.</summary>
    [MenuItem("Tools/Mewage/Refresh SynergyID Cache")]
    private static void RefreshCache()
    {
        _ids = _labels = null;
        EnsureCache();
        Debug.Log($"[SynergyIDDrawer] 캐시 갱신 완료 — {_ids.Length - 1}개 시너지 로드");
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.HelpBox(position, "[SynergyID] 는 string 필드에만 사용 가능합니다.", MessageType.Error);
            return;
        }

        EnsureCache();

        EditorGUI.BeginProperty(position, label, property);

        // 현재 값의 인덱스 검색
        string current = property.stringValue;
        int currentIdx = 0;
        for (int i = 1; i < _ids.Length; i++)
        {
            if (_ids[i] == current) { currentIdx = i; break; }
        }

        // 드롭다운 표시
        int selected = EditorGUI.Popup(position, label.text, currentIdx, _labels);
        if (selected != currentIdx)
            property.stringValue = selected == 0 ? "" : _ids[selected];

        EditorGUI.EndProperty();
    }
}
