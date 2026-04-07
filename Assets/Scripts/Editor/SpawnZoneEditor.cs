#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(SpawnZone))]
public class SpawnZoneEditor : Editor
{
    private SerializedProperty pointsProp;
    private SerializedProperty edgeProp;

    private void OnEnable()
    {
        pointsProp = serializedObject.FindProperty("localPoints");
        edgeProp   = serializedObject.FindProperty("edgeOffset");
    }

    private void OnSceneGUI()
    {
        var zone   = (SpawnZone)target;
        var offset = (Vector2)zone.transform.position;

        serializedObject.Update();

        // ── 포인트 핸들 ──
        for (int i = 0; i < pointsProp.arraySize; i++)
        {
            var     elem  = pointsProp.GetArrayElementAtIndex(i);
            Vector2 world = (Vector2)zone.transform.TransformPoint(elem.vector2Value);

            Handles.color = new Color(1f, 0.55f, 0.1f);
            float size = HandleUtility.GetHandleSize(world) * 0.12f;
            Handles.DrawSolidDisc(world, Vector3.forward, size);
            Handles.Label(world + Vector2.up * (size + 0.15f), i.ToString(), EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            Vector3 newWorld = Handles.PositionHandle(world, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(zone, "Move SpawnZone Point");
                // 월드 → 로컬 (스케일 역변환)
                elem.vector2Value = (Vector2)zone.transform.InverseTransformPoint(newWorld);
                serializedObject.ApplyModifiedProperties();
            }
        }

        // ── 엣지 연결선 (주황) ──
        if (pointsProp.arraySize >= 2)
        {
            Handles.color = new Color(1f, 0.45f, 0.1f, 0.8f);
            int count = pointsProp.arraySize;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector2 a = zone.transform.TransformPoint(pointsProp.GetArrayElementAtIndex(j).vector2Value);
                Vector2 b = zone.transform.TransformPoint(pointsProp.GetArrayElementAtIndex(i).vector2Value);
                Handles.DrawLine(a, b, 2f);
            }
        }

        // ── edgeOffset 외곽선 (빨강) ──
        float edgeVal = edgeProp.floatValue;
        if (edgeVal > 0f && zone.PointCount >= 2)
        {
            int     count    = zone.PointCount;
            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < count; i++) centroid += zone.GetWorld(i);
            centroid /= count;

            Handles.color = new Color(1f, 0.25f, 0.25f, 0.9f);
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector2 a = zone.GetWorld(j), b = zone.GetWorld(i);
                Vector2 n = new Vector2((b - a).y, -(b - a).x).normalized;
                if (Vector2.Dot(n, (a + b) * 0.5f - centroid) < 0f) n = -n;
                Handles.DrawLine(a + n * edgeVal, b + n * edgeVal, 2f);
            }
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(pointsProp, true);
        EditorGUILayout.Space(4);

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(edgeProp, new GUIContent("Edge Offset"));
        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            SceneView.RepaintAll();
        }

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
