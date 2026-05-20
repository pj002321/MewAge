using System.Collections.Generic;
using Mewage.DB;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(RewardVisualConfig.Entry))]
public class RewardVisualConfigEntryDrawer : PropertyDrawer
{
    static string[] _ids;

    static void EnsureCache()
    {
        if (_ids != null) return;

        var seen = new HashSet<string>();
        var list = new List<string>();

        DB_상점.ForEachEntity(row =>
        {
            if (!string.IsNullOrEmpty(row.f_보상ID) && seen.Add(row.f_보상ID))
                list.Add(row.f_보상ID);
        });

        list.Sort();
        _ids = list.ToArray();
    }

    [MenuItem("Tools/Mewage/Refresh RewardID Cache")]
    static void RefreshCache()
    {
        _ids = null;
        EnsureCache();
        Debug.Log($"[RewardVisualConfig] 캐시 갱신 완료 — {_ids.Length}개 보상ID");
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EnsureCache();

        EditorGUI.BeginProperty(position, label, property);

        var idProp   = property.FindPropertyRelative("rewardId");
        var iconProp = property.FindPropertyRelative("icon");

        float dropW = position.width * 0.5f - 4f;
        float iconW = position.width - dropW - 4f;

        var dropRect = new Rect(position.x,              position.y, dropW, position.height);
        var iconRect = new Rect(position.x + dropW + 4f, position.y, iconW, position.height);

        int current  = System.Array.IndexOf(_ids, idProp.stringValue);
        int selected = EditorGUI.Popup(dropRect, current < 0 ? 0 : current, _ids);

        if (_ids.Length > 0)
            idProp.stringValue = _ids[selected];

        EditorGUI.PropertyField(iconRect, iconProp, GUIContent.none);

        EditorGUI.EndProperty();
    }
}
