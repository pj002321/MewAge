using UnityEditor;

#if UNITY_EDITOR
namespace TheBlackCat.TrailEffect2D.Editor
{
    [CustomEditor(typeof(TrailInstance))]
    public class TrailInstanceDrawer : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();
            var serializedObj = new SerializedObject(target);

            var property = serializedObj.FindProperty("OrderInLayer");
            EditorGUILayout.PropertyField(property, true, null);

            Space(2);

            property = serializedObj.FindProperty("TrailColour");
            EditorGUILayout.PropertyField(property, true, null);

            property = serializedObj.FindProperty("AffectedByEasing");
            EditorGUILayout.PropertyField(property, true, null);

            Space(2);

            property = serializedObj.FindProperty("StartSizeType");
            EditorGUILayout.PropertyField(property, true, null);

            if ((SizingType)property.enumValueIndex == SizingType.FIXED_SIZE)
            {
                property = serializedObj.FindProperty("StartSize");
                EditorGUILayout.PropertyField(property, true, null);                
            }
            else
            {
                property = serializedObj.FindProperty("StartMultiplier");
                EditorGUILayout.PropertyField(property, true, null);
            }

            property = serializedObj.FindProperty("EndSizeType");
            EditorGUILayout.PropertyField(property, true, null);

            if ((SizingType)property.enumValueIndex == SizingType.FIXED_SIZE)
            {
                property = serializedObj.FindProperty("EndSize");
                EditorGUILayout.PropertyField(property, true, null);
            }
            else
            {
                property = serializedObj.FindProperty("EndMultiplier");
                EditorGUILayout.PropertyField(property, true, null);
            }

            Space(1);

            property = serializedObj.FindProperty("EaseType");
            EditorGUILayout.PropertyField(property, true, null);

            if ((EaseType)property.enumValueIndex >= EaseType.EXPONENTIAL_EASE_OUT && (EaseType)property.enumValueIndex <= EaseType.EXPONENTIAL_EASE_IN_OUT)
            {
                property = serializedObj.FindProperty("Power");
                EditorGUILayout.PropertyField(property, true, null);
            }

            Space(2);

            property = serializedObj.FindProperty("Lifespan");
            EditorGUILayout.PropertyField(property, true, null);

            property = serializedObj.FindProperty("SpawnTrailOnStart");
            EditorGUILayout.PropertyField(property, true, null);

            property = serializedObj.FindProperty("DisableAllTrailOnStop");
            EditorGUILayout.PropertyField(property, true, null);

            Space(2);

            property = serializedObj.FindProperty("SpawnCondition");
            EditorGUILayout.PropertyField(property, true, null);

            if ((TrailSpawningCondition)property.enumValueIndex == TrailSpawningCondition.TIME)
            {
                property = serializedObj.FindProperty("TimeBetweenSpawn");
                EditorGUILayout.PropertyField(property, true, null);
            }
            else
            {
                property = serializedObj.FindProperty("DistanceBetweenSpawn");
                EditorGUILayout.PropertyField(property, true, null);
            }

            if (EditorGUI.EndChangeCheck())
            {
                serializedObj.ApplyModifiedProperties();
            }
        }

        private void Space(int time)
        {
            for (int i = 0; i < time; i++)
            {
                EditorGUILayout.Space();
            }
        }
    }
}
#endif