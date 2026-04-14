using UnityEngine;

/// <summary>
/// 설치된 기물의 수명을 관리하고 스킬을 시작합니다.
/// 기물 동작은 같은 GameObject에 부착된 <see cref="StructureSkillBase"/>
/// 서브클래스(DoTSkill, HealZoneSkill 등)에 정의하세요.
/// </summary>
public class StructureController : MonoBehaviour
{
    public static readonly System.Collections.Generic.List<StructureController> All = new();

    [Header("기물 유지 시간 (0 = 영구)")]
    [SerializeField] private float duration = 15f;

    public float Duration => duration;

    /// <summary>씬에 배치된 모든 기물을 즉시 파괴합니다 (웨이브 종료 시 호출).</summary>
    public static void DestroyAll()
    {
        for (int i = All.Count - 1; i >= 0; i--)
            if (All[i] != null) Destroy(All[i].gameObject);
        All.Clear();
    }

    private void Awake() => All.Add(this);

    private System.Collections.IEnumerator Start()
    {
        var skill = GetComponent<StructureSkillBase>();

        if (skill != null && skill.IsInstallation)
        {
            // 설치형: 낙하 연출 완료 후 스킬 발동
            yield return StartCoroutine(skill.PlayDropAnimation());
            skill.Activate();
        }
        else
        {
            // 비설치형(즉발 스펠 등): 즉시 발동 후 스프라이트 숨김
            skill?.Activate();
            if (skill != null)
            {
                var srs = GetComponentsInChildren<SpriteRenderer>();
                foreach (var sr in srs) sr.enabled = false;
            }
        }

        if (duration > 0f)
        {
            this.DLog($"[StructureController] {gameObject.name} 설치됨. {duration}초 뒤 파괴 예정.");
            yield return new WaitForSeconds(duration);

            this.DLog($"[StructureController] {gameObject.name} 수명 다하여 파괴됨.");
            Destroy(gameObject);
        }
        else
        {
            this.DLog($"[StructureController] {gameObject.name} 유지시간 세팅 0 -> 영구 유지됨.");
        }
    }

    private void OnDestroy()
    {
        All.Remove(this);
        // 기물이 파괴(또는 수거)되면 점유하던 타일 영역을 반환합니다.
        var zone = FindAnyObjectByType<StructureZone>();
        if (zone != null) zone.ReleaseCell(transform.position);
    }
}
