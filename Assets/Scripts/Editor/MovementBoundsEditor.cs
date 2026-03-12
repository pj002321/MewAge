#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 씬 뷰에서 MovementBounds 폴리곤 포인트를 드래그로 편집할 수 있게 합니다.
/// - 노란 구체를 드래그 → 포인트 이동
/// - Inspector의 + 버튼 → 포인트 추가
/// - Inspector의 - 버튼 → 포인트 삭제
/// </summary>
[CustomEditor(typeof(MovementBounds))]
public class MovementBoundsEditor : Editor
{
    private SerializedProperty pointsProp;

    private void OnEnable()
    {
        pointsProp = serializedObject.FindProperty("localPoints");
    }

    private void OnSceneGUI()
    {
        MovementBounds bounds = (MovementBounds)target;
        Vector2 offset = bounds.transform.position;

        serializedObject.Update();

        for (int i = 0; i < pointsProp.arraySize; i++)
        {
            SerializedProperty elem  = pointsProp.GetArrayElementAtIndex(i);
            Vector2            world = elem.vector2Value + offset;

            // 핸들 표시
            Handles.color = Color.yellow;
            float size = HandleUtility.GetHandleSize(world) * 0.12f;
            Handles.DrawSolidDisc(world, Vector3.forward, size);

            // 번호 레이블
            Handles.Label(world + Vector2.up * (size + 0.15f),
                          i.ToString(), EditorStyles.boldLabel);

            // 드래그 핸들
            EditorGUI.BeginChangeCheck();
            Vector3 newWorld = Handles.PositionHandle(world, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(bounds, "Move Bounds Point");
                elem.vector2Value = (Vector2)newWorld - offset;
                serializedObject.ApplyModifiedProperties();
            }
        }

        // 엣지 라인 (씬 뷰에서 연결선)
        if (pointsProp.arraySize >= 2)
        {
            Handles.color = new Color(0.2f, 1f, 0.4f, 0.6f);
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

        // 포인트 추가 버튼
        if (GUILayout.Button("+ 포인트 추가 (마지막에 삽입)"))
        {
            pointsProp.InsertArrayElementAtIndex(pointsProp.arraySize);
            if (pointsProp.arraySize >= 2)
            {
                // 마지막 두 점의 중간 위치로 초기화
                Vector2 prev = pointsProp.GetArrayElementAtIndex(pointsProp.arraySize - 2).vector2Value;
                pointsProp.GetArrayElementAtIndex(pointsProp.arraySize - 1).vector2Value = prev + Vector2.right;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
