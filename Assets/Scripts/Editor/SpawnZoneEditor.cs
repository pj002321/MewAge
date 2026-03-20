#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 씬 뷰에서 SpawnZone 폴리곤 포인트를 드래그로 편집합니다.
/// MovementBoundsEditor와 동일한 방식, 색상만 주황으로 구분.
/// </summary>
[CustomEditor(typeof(SpawnZone))]
public class SpawnZoneEditor : Editor
{
    private SerializedProperty pointsProp;

    private void OnEnable()
    {
        pointsProp = serializedObject.FindProperty("localPoints");
    }

    private void OnSceneGUI()
    {
        SpawnZone zone   = (SpawnZone)target;
        Vector2   offset = zone.transform.position;

        serializedObject.Update();

        for (int i = 0; i < pointsProp.arraySize; i++)
        {
            SerializedProperty elem  = pointsProp.GetArrayElementAtIndex(i);
            Vector2            world = elem.vector2Value + offset;

            Handles.color = new Color(1f, 0.55f, 0.1f);
            float size = HandleUtility.GetHandleSize(world) * 0.12f;
            Handles.DrawSolidDisc(world, Vector3.forward, size);

            Handles.Label(world + Vector2.up * (size + 0.15f),
                          i.ToString(), EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            Vector3 newWorld = Handles.PositionHandle(world, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(zone, "Move SpawnZone Point");
                elem.vector2Value = (Vector2)newWorld - offset;
                serializedObject.ApplyModifiedProperties();
            }
        }

        // 엣지 연결선
        if (pointsProp.arraySize >= 2)
        {
            Handles.color = new Color(1f, 0.45f, 0.1f, 0.7f);
            int count = pointsProp.arraySize;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector2 a = pointsProp.GetArrayElementAtIndex(j).vector2Value + offset;
                Vector2 b = pointsProp.GetArrayElementAtIndex(i).vector2Value + offset;
                Handles.DrawLine(a, b, 2f);
            }
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(pointsProp, true);

        EditorGUILayout.Space(4);

        if (GUILayout.Button("+ 포인트 추가 (마지막에 삽입)"))
        {
            pointsProp.InsertArrayElementAtIndex(pointsProp.arraySize);
            if (pointsProp.arraySize >= 2)
            {
                Vector2 prev = pointsProp.GetArrayElementAtIndex(pointsProp.arraySize - 2).vector2Value;
                pointsProp.GetArrayElementAtIndex(pointsProp.arraySize - 1).vector2Value = prev + Vector2.right;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
