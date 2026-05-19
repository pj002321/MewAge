using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(BossCloneSkill.SkillTypeEntry))]
public class SkillTypeEntryDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var typeNameProp = property.FindPropertyRelative("typeName");

        // 같은 BossController의 _skills 리스트에서 타입명 수집 (BossCloneSkill 제외)
        var names = GetSkillNamesFromBoss(property);

        EditorGUI.BeginProperty(position, label, property);

        if (names.Count == 0)
        {
            EditorGUI.LabelField(position, label.text, "(스킬 없음)");
        }
        else
        {
            int currentIdx = names.IndexOf(typeNameProp.stringValue);
            if (currentIdx < 0) currentIdx = 0;

            int selected = EditorGUI.Popup(position, label, currentIdx,
                names.Select(n => new GUIContent(n)).ToArray());
            typeNameProp.stringValue = names[selected];
        }

        EditorGUI.EndProperty();
    }

    private static List<string> GetSkillNamesFromBoss(SerializedProperty property)
    {
        var result = new List<string>();
        var skillsProp = property.serializedObject.FindProperty("_skills");
        if (skillsProp == null || !skillsProp.isArray) return result;

        for (int i = 0; i < skillsProp.arraySize; i++)
        {
            var elem     = skillsProp.GetArrayElementAtIndex(i);
            var typeName = elem.managedReferenceFullTypename; // "Assembly TypeName"
            if (string.IsNullOrEmpty(typeName)) continue;

            // "Assembly TypeName" → "TypeName" 만 추출
            var shortName = typeName.Contains(' ')
                ? typeName.Substring(typeName.LastIndexOf(' ') + 1)
                : typeName;

            if (shortName == nameof(BossCloneSkill)) continue;
            result.Add(shortName);
        }

        return result;
    }
}

