using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
namespace TheBlackCat.TrailEffect2D.Editor
{
    [CustomEditor(typeof(TrailManager))]
    public class TrailManagerDrawer : UnityEditor.Editor
    {
        private void OnEnable()
        {
            EditorApplication.update += Update;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Update;
        }

        private void Update()
        {
            if (Application.isPlaying && TrailManager.Instance.IsDebugMode && TrailManager.Instance.DebuggingGameObject != null)
            {
                Repaint();
            }
        }

        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            var serializedObj = new SerializedObject(target);

            var property = serializedObj.FindProperty("autoCleanUp");
            EditorGUILayout.PropertyField(property, true, null);

            if (property.boolValue)
            {
                property = serializedObj.FindProperty("cleanUpAfterSeconds");
                EditorGUILayout.PropertyField(property, true, null);                
            }

            property = serializedObj.FindProperty("poolTrailsOnStart");
            EditorGUILayout.PropertyField(property, true, null);

            EditorGUILayout.Space();

            property = serializedObj.FindProperty("debugMode");
            EditorGUILayout.PropertyField(property, true, null);

            if (property.boolValue)
            {
                if (Application.isPlaying)
                {
                    GUI.enabled = TrailManager.Instance != null;

                    property = serializedObj.FindProperty("debugGameObject");
                    EditorGUILayout.PropertyField(property, true, null);
                    GameObject debugObj = property.objectReferenceValue as GameObject;

                    if (debugObj != null)
                    {
                        EditorGUILayout.LabelField("Is Spawning Trail: " + (TrailManager.Instance.IsObjectSpawningTrails(debugObj) ? "Yes" : "No"));

                        TrailProcessor processor = TrailManager.Instance.GetTrailProcessor(debugObj);
                        EditorGUILayout.BeginHorizontal();

                        EditorGUILayout.LabelField("Trail Processor: ");
                        if (processor != null && GUILayout.Button("Ping Processor"))
                        {
                            EditorGUIUtility.PingObject(processor);
                        }

                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.LabelField("Trail Count: " + (processor != null ? processor.GetActiveTrailCount() : "0"));

                        EditorGUILayout.BeginHorizontal();

                        if (GUILayout.Button("Start Trail"))
                        {
                            TrailManager.Instance.StartTrail(debugObj);
                        }
                        if (GUILayout.Button("Stop Trail"))
                        {
                            TrailManager.Instance.StopTrail(debugObj);
                        }

                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Enter play mode to activate debug mode.", MessageType.Info);
                }
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObj.ApplyModifiedProperties();
            }
        }
    }
}
#endif