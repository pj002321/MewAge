using UnityEngine;

/// <summary>
/// 기물 스킬의 기반 클래스.
/// 새 스킬 추가 방법:
///   1. 이 클래스를 상속한 새 컴포넌트를 만드세요.
///   2. 기물 프리팹에 StructureController와 함께 새 컴포넌트를 부착하세요.
/// </summary>
public abstract class StructureSkillBase : MonoBehaviour
{
    [Header("스킬 수치")]
    [SerializeField] protected float mainValue    = 10f;   // 피해량 / 회복량 등
    [SerializeField] protected float subValue     = 1f;    // 마비 지속시간 등 보조 수치
    [SerializeField] protected float radius       = 1.5f;  // 범위
    [SerializeField] protected float tickInterval = 1f;    // 틱 간격

    [Header("시각 연출 설정 (설치형 여부)")]
    [Tooltip("체크 시 발동 후에도 기물 스프라이트가 맵에 남습니다.\n체크 해제 시 얼음이나 스펠처럼 즉시 투명해집니다.")]
    [SerializeField] protected bool isInstallation = false;

    /// <summary>이 기물이 맵에 형체가 남는 설치형인지 여부. false면 발동 즉시 외형이 사라집니다.</summary>
    public bool IsInstallation => isInstallation;

    /// <summary>기물의 현재 강화 레벨 (Lv.1~20). 스킬에서 레벨별 분기에 사용합니다.</summary>
    public int StructureLevel { get; private set; } = 1;

    /// <summary>
    /// StructurePlacer가 _s 프리팹을 설치한 직후 호출합니다.
    /// 레벨별 수치로 mainValue / subValue를 덮어씁니다.
    /// </summary>
    public void Init(Mewage.DB.DB_기물 data, int level)
    {
        if (data == null) return;
        StructureLevel = level;
        mainValue = StructureWareManager.Shared?.GetMainValue(data, level) ?? data.f_주수치;
        subValue  = data.f_보조수치;
    }

    /// <summary>StructureController.Start()에서 호출됩니다.</summary>
    public abstract void Activate();

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
