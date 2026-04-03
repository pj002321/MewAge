using UnityEditor;
using UnityEngine;

namespace TheBlackCat.TrailEffect2D
{
#if UNITY_EDITOR
    [InitializeOnLoad]
    public static class DebuggingHelper
    {
        static DebuggingHelper()
        {
            EditorApplication.hierarchyWindowItemOnGUI += HightlightTrailObjects;
        }

        private static void HightlightTrailObjects(int instanceID, Rect selectionRect)
        {
            var manager = TrailManager.Instance;
            if (Application.isPlaying && TrailManager.Instance != null && manager.IsDebugMode && manager.DebuggingGameObject != null && manager.IsObjectSpawningTrails(manager.DebuggingGameObject))
            {
                GameObject obj = EditorUtility.InstanceIDToObject(instanceID) as GameObject;

                if (obj != null)
                {
                    if (obj.TryGetComponent<TrailObject>(out var trailObject) && trailObject.TrailProcessor?.TrailInstance?.gameObject == TrailManager.Instance.DebuggingGameObject)
                    {
                        if (obj.activeSelf)
                        {
                            EditorGUI.DrawRect(selectionRect, new Color32(51, 255, 0, 40));
                        }
                        else
                        {
                            EditorGUI.DrawRect(selectionRect, new Color32(112, 0, 0, 40));
                        }
                    }
                    else if (obj.TryGetComponent<TrailProcessor>(out var trailProcessor) && obj.activeSelf && trailProcessor.TrailInstance.gameObject == TrailManager.Instance.DebuggingGameObject)
                    {
                        EditorGUI.DrawRect(selectionRect, new Color32(66, 135, 245, 40));
                    }
                }
            }
        }
    }
#endif
}