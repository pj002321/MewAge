#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene view editing for MovementBounds.
/// - Yellow spheres: drag to move polygon points
/// - Cyan rect handles: drag to resize camera bounds (when custom rect is used)
/// - Magenta rect: camera view preview in FixedView mode
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

        // ── Movement polygon point handles ──
        for (int i = 0; i < pointsProp.arraySize; i++)
        {
            SerializedProperty elem  = pointsProp.GetArrayElementAtIndex(i);
            Vector2            world = elem.vector2Value + offset;

            Handles.color = Color.yellow;
            float size = HandleUtility.GetHandleSize(world) * 0.12f;
            Handles.DrawSolidDisc(world, Vector3.forward, size);

            Handles.Label(world + Vector2.up * (size + 0.15f),
                          i.ToString(), EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            Vector3 newWorld = Handles.PositionHandle(world, Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(bounds, "Move Bounds Point");
                elem.vector2Value = (Vector2)newWorld - offset;
                serializedObject.ApplyModifiedProperties();
            }
        }

        // ── Polygon edge lines ──
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

        // ── Camera bounds rect handles ──
        var viewModeProp = serializedObject.FindProperty("viewMode");
        bool isFixedView = viewModeProp.enumValueIndex == (int)MovementBounds.CameraViewMode.FixedView;
        bool showRect = isFixedView || bounds.UseCustomCameraBounds;

        if (showRect)
        {
            var rectProp = serializedObject.FindProperty("cameraBoundsRect");
            Rect localRect = rectProp.rectValue;

            Vector2 worldMin = offset + new Vector2(localRect.xMin, localRect.yMin);
            Vector2 worldMax = offset + new Vector2(localRect.xMax, localRect.yMax);

            // Rect outline (cyan)
            Handles.color = new Color(0f, 0.9f, 1f, 0.9f);
            Vector3[] corners = new Vector3[]
            {
                new(worldMin.x, worldMin.y, 0f),
                new(worldMax.x, worldMin.y, 0f),
                new(worldMax.x, worldMax.y, 0f),
                new(worldMin.x, worldMax.y, 0f),
                new(worldMin.x, worldMin.y, 0f),
            };
            Handles.DrawPolyLine(corners);

            // Corner drag handles
            float handleSize = HandleUtility.GetHandleSize(worldMin) * 0.08f;
            Handles.color = new Color(0f, 1f, 1f, 1f);

            EditorGUI.BeginChangeCheck();
            Vector3 newMin = Handles.FreeMoveHandle(worldMin, handleSize, Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(bounds, "Move Camera Bounds Min");
                Vector2 localMin = (Vector2)newMin - offset;
                rectProp.rectValue = Rect.MinMaxRect(localMin.x, localMin.y, localRect.xMax, localRect.yMax);
                serializedObject.ApplyModifiedProperties();
            }

            EditorGUI.BeginChangeCheck();
            Vector3 newMax = Handles.FreeMoveHandle(worldMax, handleSize, Vector3.zero, Handles.DotHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(bounds, "Move Camera Bounds Max");
                Vector2 localMax = (Vector2)newMax - offset;
                rectProp.rectValue = Rect.MinMaxRect(localRect.xMin, localRect.yMin, localMax.x, localMax.y);
                serializedObject.ApplyModifiedProperties();
            }

            Handles.Label((Vector3)worldMin + Vector3.down * 0.3f, "Cam Min", EditorStyles.miniLabel);
            Handles.Label((Vector3)worldMax + Vector3.up   * 0.3f, "Cam Max", EditorStyles.miniLabel);
        }

        // ── FixedView: draw actual camera view (magenta) at bounds center ──
        if (isFixedView)
        {
            var rectProp = serializedObject.FindProperty("cameraBoundsRect");
            Rect r = rectProp.rectValue;
            Vector2 center = offset + new Vector2(r.xMin + r.width * 0.5f, r.yMin + r.height * 0.5f);

            float boundsW = r.width;
            float boundsH = r.height;

            if (boundsW > 0f && boundsH > 0f)
            {
                // Aspect ratio: use main camera if available, else 9:19.5 mobile default
                Camera cam = Camera.main;
                float aspect = cam != null ? cam.aspect : (9f / 19.5f);

                // Same calc as CameraFollow.ApplyFixedView
                float orthoByH = boundsH * 0.5f;
                float orthoByW = boundsW * 0.5f / aspect;
                float ortho    = Mathf.Min(orthoByH, orthoByW);
                float halfH    = ortho;
                float halfW    = ortho * aspect;

                Handles.color = new Color(1f, 0.3f, 1f, 0.8f);
                Vector3[] camCorners = new Vector3[]
                {
                    new(center.x - halfW, center.y - halfH, 0f),
                    new(center.x + halfW, center.y - halfH, 0f),
                    new(center.x + halfW, center.y + halfH, 0f),
                    new(center.x - halfW, center.y + halfH, 0f),
                    new(center.x - halfW, center.y - halfH, 0f),
                };
                Handles.DrawPolyLine(camCorners);
                Handles.Label(new Vector3(center.x, center.y + halfH + 0.2f, 0f),
                    $"📷 Camera View (ortho={ortho:F2})", EditorStyles.boldLabel);
            }
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // ── Camera View Mode ──
        var viewModeProp = serializedObject.FindProperty("viewMode");
        EditorGUILayout.PropertyField(viewModeProp, new GUIContent("🎥 카메라 뷰 모드"));
        bool isFixedView = viewModeProp.enumValueIndex == (int)MovementBounds.CameraViewMode.FixedView;

        if (isFixedView)
            EditorGUILayout.HelpBox(
                "FixedView: 카메라가 cameraBoundsRect 중심에 고정됩니다.\n" +
                "orthographicSize는 변하지 않으므로 이동 영역을 카메라 뷰 안에 배치하세요.\n" +
                "씬 뷰의 마젠타(분홍) 사각형이 실제 카메라 뷰입니다.",
                MessageType.Info);
        else
            EditorGUILayout.HelpBox(
                "DynamicFollow: 카메라가 플레이어를 따라가며 카메라 영역 안에서만 이동합니다.\n" +
                "카메라 영역이 카메라 뷰보다 커야 활동 반경이 생깁니다.",
                MessageType.Info);

        EditorGUILayout.Space(8);

        // ── Movement Polygon ──
        EditorGUILayout.LabelField("🟢 플레이어 이동 영역 (폴리곤)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(pointsProp, true);

        if (GUILayout.Button("+ 포인트 추가 (마지막에 삽입)"))
        {
            pointsProp.InsertArrayElementAtIndex(pointsProp.arraySize);
            if (pointsProp.arraySize >= 2)
            {
                Vector2 prev = pointsProp.GetArrayElementAtIndex(pointsProp.arraySize - 2).vector2Value;
                pointsProp.GetArrayElementAtIndex(pointsProp.arraySize - 1).vector2Value = prev + Vector2.right;
            }
        }

        EditorGUILayout.Space(8);

        // ── Camera Bounds ──
        EditorGUILayout.LabelField("🔵 카메라 제한 영역", EditorStyles.boldLabel);

        if (isFixedView)
        {
            // FixedView always uses cameraBoundsRect
            var rectProp = serializedObject.FindProperty("cameraBoundsRect");
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(rectProp, new GUIContent("카메라 영역 (로컬 Rect)"));
            EditorGUI.indentLevel--;
        }
        else
        {
            // DynamicFollow: toggle between custom rect and auto-padding
            var useCustomProp = serializedObject.FindProperty("useCustomCameraBounds");
            EditorGUILayout.PropertyField(useCustomProp, new GUIContent("커스텀 카메라 영역 사용"));

            if (useCustomProp.boolValue)
            {
                var rectProp = serializedObject.FindProperty("cameraBoundsRect");
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(rectProp, new GUIContent("카메라 영역 (로컬 Rect)"));
                EditorGUI.indentLevel--;
            }
            else
            {
                EditorGUILayout.LabelField("자동 패딩 (이동 영역 AABB 기준)", EditorStyles.miniLabel);
                var padProp = serializedObject.FindProperty("cameraPadding");
                Vector4 pad = padProp.vector4Value;
                EditorGUI.indentLevel++;
                pad.w = EditorGUILayout.FloatField("Top   (↑)", Mathf.Max(0f, pad.w));
                pad.z = EditorGUILayout.FloatField("Bottom(↓)", Mathf.Max(0f, pad.z));
                pad.x = EditorGUILayout.FloatField("Left  (←)", Mathf.Max(0f, pad.x));
                pad.y = EditorGUILayout.FloatField("Right (→)", Mathf.Max(0f, pad.y));
                EditorGUI.indentLevel--;
                padProp.vector4Value = pad;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
