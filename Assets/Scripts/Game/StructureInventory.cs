using UnityEngine;
using Mewage.DB;

/// <summary>
/// 기물 인벤토리 — 최대 4종류의 기물을 슬롯별로 관리합니다.
/// 같은 종류의 기물은 같은 슬롯에 누적됩니다.
/// </summary>
public class StructureInventory : MonoBehaviourSingleton<StructureInventory>
{
    public const int SlotCount = 4;

    private readonly DB_기물[] slotData  = new DB_기물[SlotCount];
    private readonly int[]     slotCount = new int[SlotCount];

    protected override bool DontDestroy => false;

    public event System.Action OnChanged;

    // ── 조회 ────────────────────────────────────────────────────────
    public DB_기물 GetData(int slot)  => slot >= 0 && slot < SlotCount ? slotData[slot]  : null;
    public int     GetCount(int slot) => slot >= 0 && slot < SlotCount ? slotCount[slot] : 0;

    // ── 획득 (기물 구슬 수집 시 호출) ───────────────────────────────
    public void Push(DB_기물 data)
    {
        if (data == null) return;

        // 같은 종류 슬롯이 있으면 누적
        for (int i = 0; i < SlotCount; i++)
        {
            if (slotData[i] == data) { slotCount[i]++; OnChanged?.Invoke(); return; }
        }

        // 빈 슬롯에 새로 추가
        for (int i = 0; i < SlotCount; i++)
        {
            if (slotData[i] == null) { slotData[i] = data; slotCount[i] = 1; OnChanged?.Invoke(); return; }
        }

        Debug.LogWarning("[StructureInventory] 슬롯이 가득 찼습니다.");
    }

    // ── 사용 (버튼 클릭 시 호출) ────────────────────────────────────
    public void UseAt(int slot)
    {
        if (slot < 0 || slot >= SlotCount) return;
        if (slotData[slot] == null || slotCount[slot] <= 0) return;

        var player = PlayerController.Instance;
        if (player == null) return;

        var data = slotData[slot];
        slotCount[slot]--;
        if (slotCount[slot] <= 0) slotData[slot] = null;

        if (data.f_설치프리팹 != null)
            Object.Instantiate(data.f_설치프리팹, player.Position, Quaternion.identity);

        OnChanged?.Invoke();
    }
}
