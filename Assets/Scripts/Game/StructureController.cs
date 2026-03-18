using UnityEngine;

/// <summary>
/// 설치된 기물의 수명을 관리하고 스킬을 시작합니다.
/// 기물 동작은 같은 GameObject에 부착된 <see cref="StructureSkillBase"/>
/// 서브클래스(DoTSkill, HealZoneSkill 등)에 정의하세요.
/// </summary>
public class StructureController : MonoBehaviour
{
    [Header("기물 유지 시간 (0 = 영구)")]
    [SerializeField] private float duration = 15f;

    private void Start()
    {
        if (duration > 0f)
            Destroy(gameObject, duration);

        GetComponent<StructureSkillBase>()?.Activate();
    }
}
