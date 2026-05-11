using UnityEditor;

/// <summary>
/// BossController 멀티셀렉트 편집 지원.
/// 여러 보스 프리팹을 동시에 선택했을 때 공통 필드를 한 번에 수정할 수 있습니다.
/// </summary>
[CustomEditor(typeof(BossController))]
[CanEditMultipleObjects]
public class BossControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }
}
