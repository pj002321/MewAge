using System.Collections;
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

    [Header("낙하 연출 (isInstallation=true 일 때 자동 재생)")]
    [SerializeField] private float dropHeight   = 5f;    // 낙하 시작 높이 (units)
    [SerializeField] private float dropDuration = 0.32f; // 낙하 시간 (초)
    [SerializeField] private float squashScaleX = 1.35f; // 착지 squash 가로 배율
    [SerializeField] private float squashScaleY = 0.6f;  // 착지 squash 세로 배율
    [SerializeField] private float squashDuration = 0.08f; // squash → 복구 시간 (초)

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
        mainValue = data.f_주수치;
        subValue  = data.f_보조수치;
    }

    /// <summary>StructureController.Start()에서 호출됩니다.</summary>
    public abstract void Activate();

    /// <summary>
    /// 하늘에서 떨어지며 착지하는 연출 코루틴.
    /// StructureController가 IsInstallation=true 스킬에 한해 Activate() 전에 호출합니다.
    /// </summary>
    public IEnumerator PlayDropAnimation()
    {
        Vector3 landPos    = transform.position;
        Vector3 startPos   = landPos + new Vector3(0f, dropHeight, 0f);
        Vector3 normalScale = transform.localScale;

        transform.position   = startPos;
        transform.localScale = normalScale * 0.15f; // 멀리서 아주 작게 시작

        // ── 낙하 (ease-in: 가속) ────────────────────────────
        float elapsed = 0f;
        while (elapsed < dropDuration)
        {
            float t    = elapsed / dropDuration;
            float ease = t * t;                    // 가속 낙하
            transform.position   = Vector3.LerpUnclamped(startPos, landPos, ease);
            transform.localScale = Vector3.Lerp(normalScale * 0.15f, normalScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position   = landPos;
        transform.localScale = normalScale;

        // ── 착지 squash ──────────────────────────────────────
        Vector3 squash = new Vector3(normalScale.x * squashScaleX,
                                     normalScale.y * squashScaleY,
                                     normalScale.z);
        float half = squashDuration * 0.5f;

        elapsed = 0f;
        while (elapsed < half)
        {
            transform.localScale = Vector3.Lerp(normalScale, squash, elapsed / half);
            elapsed += Time.deltaTime;
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < half)
        {
            transform.localScale = Vector3.Lerp(squash, normalScale, elapsed / half);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localScale = normalScale;
    }

    /// <summary>
    /// 기물이 수명을 다해 파괴되기 직전에 StructureController가 호출합니다.
    /// 사라지는 연출(역재생, bDestroy 등)이 필요한 스킬에서 오버라이드하세요.
    /// 이 코루틴이 끝난 후 gameObject가 파괴됩니다.
    /// </summary>
    public virtual System.Collections.IEnumerator OnPreDestroy() { yield break; }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
