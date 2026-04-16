using System.Collections.Generic;
using Mewage.DB;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 런타임 중 Inspector에서 모든 기물을 즉시 설치할 수 있는 디버그 컴포넌트.
/// 씬의 아무 GameObject에 붙인 뒤 플레이 모드에서 Inspector를 통해 사용합니다.
/// 쿨타임·인벤토리를 우회하여 직접 프리팹을 스폰합니다.
/// </summary>
public class StructureDebugInspector : MonoBehaviour
{
    // ── 단일 설치 ─────────────────────────────────────────────────────

    [BoxGroup("단일 기물 설치")]
    [ValueDropdown(nameof(GetWareOptions))]
    [LabelText("기물 선택")]
    public string selectedWareId;

    [BoxGroup("단일 기물 설치")]
    [LabelText("레벨"), PropertyRange(1, 20)]
    public int level = 1;

    [BoxGroup("단일 기물 설치")]
    [LabelText("설치 위치 오프셋 (플레이어 기준)")]
    public Vector2 spawnOffset = new Vector2(2f, 0f);

    [BoxGroup("단일 기물 설치")]
    [Button("설치하기", ButtonSizes.Large), GUIColor(0.3f, 0.9f, 0.3f)]
    private void PlaceSelected()
    {
        if (string.IsNullOrEmpty(selectedWareId))
        {
            Debug.LogWarning("[StructureDebug] 기물을 먼저 선택하세요.");
            return;
        }

        var data = DB_기물.FindEntity(r => r.f_기물ID == selectedWareId);
        if (data == null)
        {
            Debug.LogWarning($"[StructureDebug] DB에서 '{selectedWareId}' 를 찾을 수 없습니다.");
            return;
        }

        PlaceWare(data, level, GetSpawnPosition(spawnOffset));
    }

    // ── 전체 설치 ─────────────────────────────────────────────────────

    [BoxGroup("전체 기물 설치")]
    [LabelText("전체 설치 레벨"), PropertyRange(1, 20)]
    public int bulkLevel = 1;

    [BoxGroup("전체 기물 설치")]
    [LabelText("간격 (유닛)"), PropertyRange(0.5f, 5f)]
    public float bulkSpacing = 1.8f;

    [BoxGroup("전체 기물 설치")]
    [Button("모든 기물 설치", ButtonSizes.Large), GUIColor(0.3f, 0.6f, 1f)]
    private void PlaceAll()
    {
        var list = new List<DB_기물>();
        DB_기물.ForEachEntity(r => list.Add(r));

        if (list.Count == 0)
        {
            Debug.LogWarning("[StructureDebug] DB에 기물이 없습니다.");
            return;
        }

        // 격자 배치: 가로 방향으로 나열
        Vector3 origin = GetSpawnPosition(Vector2.zero);
        int cols = Mathf.CeilToInt(Mathf.Sqrt(list.Count));
        for (int i = 0; i < list.Count; i++)
        {
            int row = i / cols;
            int col = i % cols;
            Vector3 pos = origin + new Vector3(col * bulkSpacing, row * bulkSpacing, 0f);
            PlaceWare(list[i], bulkLevel, pos);
        }

        Debug.Log($"[StructureDebug] {list.Count}개 기물 전체 설치 완료 (Lv.{bulkLevel})");
    }

    // ── 제어 ──────────────────────────────────────────────────────────

    [BoxGroup("제어")]
    [Button("설치된 기물 모두 제거", ButtonSizes.Medium), GUIColor(0.9f, 0.3f, 0.3f)]
    private void DestroyAll()
    {
        int count = StructureController.All.Count;
        StructureController.DestroyAll();
        Debug.Log($"[StructureDebug] {count}개 기물 제거됨");
    }

    [BoxGroup("제어")]
    [ShowInInspector, ReadOnly, LabelText("현재 설치 수")]
    private int ActiveCount => Application.isPlaying ? StructureController.All.Count : 0;

    // ── 내부 ──────────────────────────────────────────────────────────

    private static void PlaceWare(DB_기물 data, int lv, Vector3 worldPos)
    {
        string id      = data.f_기물ID;
        string chapter = id.Split('_')[0];
        string path    = $"Ware/{chapter}/{id.ToLower()}_s";

        var prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[StructureDebug] 프리팹 없음: Resources/{path}");
            return;
        }

        var go    = Object.Instantiate(prefab, worldPos, Quaternion.identity);
        var skill = go.GetComponent<StructureSkillBase>();
        skill?.Init(data, lv);

        Debug.Log($"[StructureDebug] {id} Lv.{lv} → {worldPos}");
    }

    private static Vector3 GetSpawnPosition(Vector2 offset)
    {
        var player = PlayerController.Instance;
        Vector3 base_ = player != null
            ? (Vector3)(Vector2)player.transform.position
            : Vector3.zero;
        return base_ + (Vector3)offset;
    }

    private IEnumerable<ValueDropdownItem<string>> GetWareOptions()
    {
        var list = new List<ValueDropdownItem<string>>();
        DB_기물.ForEachEntity(row =>
        {
            string label = $"[{row.f_기물ID.Split('_')[0]}] {row.f_등급} | {row.f_역할,-4} | {row.f_기물ID}";
            list.Add(new ValueDropdownItem<string>(label, row.f_기물ID));
        });
        return list;
    }
}
