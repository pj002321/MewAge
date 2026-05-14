#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StatusEffectConfig))]
public class StatusEffectConfigInspector : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "특수효과 설정은 전용 에디터 창에서만 편집하세요.\n" +
            "MuAge > Weapon Special Effect Config Editor",
            MessageType.Info);

        EditorGUILayout.Space(4);

        if (GUILayout.Button("에디터 창 열기", GUILayout.Height(32)))
            WeaponSpecialEffectConfigEditor.Open();
    }
}
#endif
