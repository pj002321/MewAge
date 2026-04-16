using System.Collections.Generic;
using Mewage.DB;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 런타임 중 Inspector에서 원하는 증강을 즉시 적용하는 디버그 컴포넌트.
/// 씬의 아무 GameObject에 붙인 뒤 플레이 모드에서 Inspector를 통해 사용합니다.
/// </summary>
public class AugmentDebugInspector : MonoBehaviour
{
    // ── 단일 적용 ─────────────────────────────────────────────────────

    [BoxGroup("단일 증강 적용")]
    [ValueDropdown(nameof(GetAugmentOptions))]
    [LabelText("증강 선택")]
    public string selectedAugId;

    [BoxGroup("단일 증강 적용")]
    [LabelText("적용 횟수"), PropertyRange(1, 10)]
    public int applyCount = 1;

    [BoxGroup("단일 증강 적용")]
    [Button("적용하기", ButtonSizes.Large), GUIColor(0.3f, 0.9f, 0.3f)]
    private void ApplySelected()
    {
        if (string.IsNullOrEmpty(selectedAugId))
        {
            Debug.LogWarning("[AugmentDebug] 증강을 먼저 선택하세요.");
            return;
        }

        var data = DB_증강.FindEntity(r => r.f_증강ID.ToString() == selectedAugId);
        if (data == null)
        {
            Debug.LogWarning($"[AugmentDebug] DB에서 '{selectedAugId}' 를 찾을 수 없습니다.");
            return;
        }

        var mgr = ImprovManager.Shared;
        if (mgr == null)
        {
            Debug.LogWarning("[AugmentDebug] ImprovManager.Shared가 null입니다.");
            return;
        }

        for (int i = 0; i < applyCount; i++)
            mgr.OnImprovSelected(data);

        Debug.Log($"[AugmentDebug] {data.f_이름} × {applyCount} 적용 완료");
    }

    // ── 전체 적용 ─────────────────────────────────────────────────────

    [BoxGroup("전체 증강 적용")]
    [LabelText("각 증강 적용 횟수"), PropertyRange(1, 5)]
    public int bulkCount = 1;

    [BoxGroup("전체 증강 적용")]
    [Button("모든 증강 1회씩 적용", ButtonSizes.Large), GUIColor(0.3f, 0.6f, 1f)]
    private void ApplyAll()
    {
        var mgr = ImprovManager.Shared;
        if (mgr == null)
        {
            Debug.LogWarning("[AugmentDebug] ImprovManager.Shared가 null입니다.");
            return;
        }

        int total = 0;
        DB_증강.ForEachEntity(data =>
        {
            for (int i = 0; i < bulkCount; i++)
                mgr.OnImprovSelected(data);
            total++;
        });

        Debug.Log($"[AugmentDebug] {total}종 증강 × {bulkCount}회 전체 적용 완료");
    }

    // ── 회복 구슬 테스트 ──────────────────────────────────────────────

    [BoxGroup("회복 구슬 테스트")]
    [ShowInInspector, ReadOnly, LabelText("현재 스폰 확률")]
    private string HealOrbChanceInfo => Application.isPlaying && PlayerController.Instance != null
        ? $"{PlayerController.Instance.HealOrbSpawnChance * 100f:F1}%"
        : "-";

    [BoxGroup("회복 구슬 테스트")]
    [LabelText("직접 설정할 스폰 확률 (0~1)"), Range(0f, 1f)]
    public float debugOrbChance = 0.5f;

    [BoxGroup("회복 구슬 테스트")]
    [Button("스폰 확률 직접 설정 (PlayerController 직접 호출)", ButtonSizes.Medium), GUIColor(0.3f, 0.9f, 0.3f)]
    private void SetOrbChanceDirectly()
    {
        var player = PlayerController.Instance;
        if (player == null) { Debug.LogWarning("[AugmentDebug] PlayerController.Instance null"); return; }

        player.IncreaseHealOrbChance(debugOrbChance, 0.2f);
        Debug.Log($"[AugmentDebug] HealOrbSpawnChance 직접 설정 → 현재={player.HealOrbSpawnChance:F2}");
    }

    [BoxGroup("회복 구슬 테스트")]
    [Button("회복 구슬 강제 스폰 (확률 무시)", ButtonSizes.Medium), GUIColor(0.9f, 0.7f, 0.2f)]
    private void ForceSpawnHealOrb()
    {
        var mgr = ItemDropManager.Shared;
        if (mgr == null) { Debug.LogWarning("[AugmentDebug] ItemDropManager.Shared null"); return; }

        // 웨이브 제한 리셋 후 강제 스폰
        mgr.ResetWaveDrops();

        var player = PlayerController.Instance;
        Vector3 pos = player != null ? (Vector3)(Vector2)player.transform.position : Vector3.zero;
        mgr.SpawnHealOrb(pos);
        Debug.Log($"[AugmentDebug] 회복 구슬 강제 스폰 at {pos}");
    }

    [BoxGroup("회복 구슬 테스트")]
    [Button("적용 경로 진단 로그 출력", ButtonSizes.Small)]
    private void DiagnoseOrbChain()
    {
        Debug.Log($"[진단] PlayerController.Instance = {(PlayerController.Instance != null ? "OK" : "NULL")}");
        Debug.Log($"[진단] PlayerStatManager.Shared  = {(PlayerStatManager.Shared  != null ? "OK" : "NULL")}");
        Debug.Log($"[진단] ImprovManager.Shared      = {(ImprovManager.Shared      != null ? "OK" : "NULL")}");
        Debug.Log($"[진단] ItemDropManager.Shared    = {(ItemDropManager.Shared    != null ? "OK" : "NULL")}");
        if (PlayerController.Instance != null)
            Debug.Log($"[진단] HealOrbSpawnChance        = {PlayerController.Instance.HealOrbSpawnChance:F3}");

        if (!string.IsNullOrEmpty(selectedAugId))
        {
            var data = DB_증강.FindEntity(r => r.f_증강ID.ToString() == selectedAugId);
            Debug.Log($"[진단] DB_증강 검색 ({selectedAugId}) = {(data != null ? $"OK (주={data.f_주수치}, 보조={data.f_보조수치})" : "NULL — DB에 없음")}");
        }
    }

    // ── 상태 조회 ─────────────────────────────────────────────────────

    [BoxGroup("현재 스탯 조회")]
    [ShowInInspector, ReadOnly, LabelText("현재 HP")]
    private string CurrentHP => Application.isPlaying && PlayerController.Instance != null
        ? $"{Mathf.RoundToInt(PlayerController.Instance.CurrentHP)} / {Mathf.RoundToInt(PlayerController.Instance.MaxHPValue)}"
        : "-";

    [BoxGroup("현재 스탯 조회")]
    [ShowInInspector, ReadOnly, LabelText("획득한 증강 수")]
    private int AcquiredCount => Application.isPlaying && ImprovManager.Shared != null
        ? ImprovManager.Shared.Acquired.Count
        : 0;

    // ── 드롭다운 ──────────────────────────────────────────────────────

    private IEnumerable<ValueDropdownItem<string>> GetAugmentOptions()
    {
        var list = new List<ValueDropdownItem<string>>();
        DB_증강.ForEachEntity(row =>
        {
            string label = $"{row.f_증강ID,-20} | {row.f_이름}  (주={row.f_주수치}, 보조={row.f_보조수치})";
            list.Add(new ValueDropdownItem<string>(label, row.f_증강ID.ToString()));
        });
        return list;
    }
}
