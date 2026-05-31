using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[CustomEditor(typeof(SpriteAnimation))]
public class SpriteAnimationEditor : Editor
{
    private bool   _previewing;
    private int    _frameIndex;
    private double _lastFrameTime;

    private SerializedProperty _framesProp;
    private SerializedProperty _fpsProp;
    private SerializedProperty _loopProp;

    private void OnEnable()
    {
        _framesProp = serializedObject.FindProperty("frames");
        _fpsProp    = serializedObject.FindProperty("fps");
        _loopProp   = serializedObject.FindProperty("loop");
    }

    private void OnDisable() => StopPreview();

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(6);

        int frameCount = _framesProp?.arraySize ?? 0;

        using (new EditorGUI.DisabledScope(frameCount == 0))
        {
            string label = _previewing ? "■  미리보기 중지" : "▶  미리보기";
            if (GUILayout.Button(label, GUILayout.Height(28)))
            {
                if (_previewing) StopPreview();
                else             StartPreview();
            }
        }

        if (_previewing && frameCount > 0)
            EditorGUILayout.LabelField($"프레임  {_frameIndex + 1} / {frameCount}",
                EditorStyles.centeredGreyMiniLabel);
    }

    private void StartPreview()
    {
        _previewing    = true;
        _frameIndex    = 0;
        _lastFrameTime = EditorApplication.timeSinceStartup;

        ApplyFrame(_frameIndex);
        EditorApplication.update += OnEditorUpdate;
    }

    private void StopPreview()
    {
        if (!_previewing) return;
        _previewing = false;
        EditorApplication.update -= OnEditorUpdate;
        Repaint();
    }

    private void OnEditorUpdate()
    {
        var sa = target as SpriteAnimation;
        if (sa == null) { StopPreview(); return; }

        serializedObject.Update();
        int   frameCount = _framesProp.arraySize;
        float fps        = Mathf.Max(_fpsProp.floatValue, 0.01f);
        float interval   = 1f / fps;

        double now = EditorApplication.timeSinceStartup;
        if (now - _lastFrameTime < interval) return;

        _lastFrameTime = now;
        _frameIndex++;

        if (_frameIndex >= frameCount)
        {
            if (_loopProp.boolValue) _frameIndex = 0;
            else { _frameIndex = frameCount - 1; StopPreview(); return; }
        }

        ApplyFrame(_frameIndex);
        SceneView.RepaintAll();
        Repaint();
    }

    private void ApplyFrame(int index)
    {
        serializedObject.Update();
        if (_framesProp.arraySize == 0) return;

        var sprite = _framesProp.GetArrayElementAtIndex(index).objectReferenceValue as Sprite;
        if (sprite == null) return;

        var sa = (SpriteAnimation)target;

        var sr = sa.GetComponent<SpriteRenderer>();
        if (sr != null) { sr.sprite = sprite; EditorUtility.SetDirty(sr); }

        var img = sa.GetComponent<Image>();
        if (img != null) { img.sprite = sprite; EditorUtility.SetDirty(img); }
    }
}
