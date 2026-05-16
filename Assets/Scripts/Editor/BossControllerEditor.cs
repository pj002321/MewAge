using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// BossController 전용 커스텀 에디터.
/// [SerializeReference] _skills 리스트를 타입 선택 드롭다운과 함께 그립니다.
/// </summary>
[CustomEditor(typeof(BossController))]
[CanEditMultipleObjects]
public class BossControllerEditor : OdinEditor
{
    private static readonly Type[]   SkillTypes;
    private SerializedProperty        _skillsProp;

    static BossControllerEditor()
    {
        SkillTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .Where(t => !t.IsAbstract && typeof(BossSkillBase).IsAssignableFrom(t))
            .OrderBy(t => t.Name)
            .ToArray();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        _skillsProp = serializedObject.FindProperty("_skills");
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        serializedObject.Update();

        EditorGUILayout.Space(8);
        DrawHeader();
        DrawSkillList();

        serializedObject.ApplyModifiedProperties();
    }

    // ── 헤더 ─────────────────────────────────────────────────────

    private void DrawHeader()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("스킬 목록", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("+ 스킬 추가", GUILayout.Width(90), GUILayout.Height(20)))
            ShowTypeMenu();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(2);
    }

    // ── 리스트 ────────────────────────────────────────────────────

    private void DrawSkillList()
    {
        if (_skillsProp == null || _skillsProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("스킬 없음. '+ 스킬 추가'를 눌러 추가하세요.", MessageType.Info);
            return;
        }

        for (int i = 0; i < _skillsProp.arraySize; i++)
        {
            var elem   = _skillsProp.GetArrayElementAtIndex(i);
            var refVal = elem.managedReferenceValue;
            string typeName = refVal?.GetType().Name ?? "(null)";

            var boxRect = EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUI.DrawRect(new Rect(boxRect.x, boxRect.y, 3f, boxRect.height),
                               new Color(0.3f, 0.6f, 1f, 0.8f));

            // 헤더 (이름 + 삭제)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"[{i}]  {typeName}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("✕", GUILayout.Width(22)))
            {
                _skillsProp.DeleteArrayElementAtIndex(i);
                serializedObject.ApplyModifiedProperties();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            // 필드 (리플렉션으로 찾아 FindPropertyRelative로 렌더)
            if (refVal != null)
            {
                EditorGUI.indentLevel++;
                DrawFields(elem, refVal.GetType());
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3);
        }
    }

    // ── 필드 렌더링 ───────────────────────────────────────────────

    /// <summary>
    /// 타입 계층(base → concrete)을 따라 [SerializeField] 필드를 순서대로 그립니다.
    /// </summary>
    private static void DrawFields(SerializedProperty elem, Type concreteType)
    {
        // base → concrete 순으로 타입 체인 구성
        var chain = new List<Type>();
        for (var t = concreteType; t != null && t != typeof(object); t = t.BaseType)
            chain.Insert(0, t);

        foreach (var type in chain)
        {
            foreach (var field in type.GetFields(
                         BindingFlags.Instance |
                         BindingFlags.NonPublic |
                         BindingFlags.Public |
                         BindingFlags.DeclaredOnly))
            {
                if (!IsSerializedField(field)) continue;

                var prop = elem.FindPropertyRelative(field.Name);
                if (prop != null)
                    EditorGUILayout.PropertyField(prop, includeChildren: true);
            }
        }
    }

    private static bool IsSerializedField(FieldInfo f)
    {
        if (f.GetCustomAttribute<HideInInspector>() != null) return false;
        if (f.GetCustomAttribute<SerializeField>()  != null) return true;
        return f.IsPublic && f.GetCustomAttribute<NonSerializedAttribute>() == null;
    }

    // ── 타입 드롭다운 ─────────────────────────────────────────────

    private void ShowTypeMenu()
    {
        var menu = new GenericMenu();
        foreach (var type in SkillTypes)
        {
            var t = type;
            menu.AddItem(new GUIContent(t.Name), false, () => AddSkill(t));
        }
        menu.ShowAsContext();
    }

    private void AddSkill(Type type)
    {
        serializedObject.Update();
        int idx = _skillsProp.arraySize;
        _skillsProp.arraySize++;
        _skillsProp.GetArrayElementAtIndex(idx).managedReferenceValue = Activator.CreateInstance(type);
        serializedObject.ApplyModifiedProperties();
    }
}
