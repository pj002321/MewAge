using UnityEngine;
using Mewage.DB;

/// <summary>
/// 기물 인벤토리 — 최대 4종류의 기물을 슬롯별로 관리합니다.
/// 같은 종류의 기물은 같은 슬롯에 누적됩니다.
/// Auto 모드 활성화 시 slotAutoInterval 주기로 보유 슬롯을 자동 사용합니다.
/// </summary>
public class StructureInventory : MonoBehaviourSingleton<StructureInventory>
{
    public const int SlotCount = 4;

    [Header("자동 사용 설정")]
    [Tooltip("Auto 모드에서 슬롯 하나를 사용하는 주기 (초)")]
    [SerializeField] private float slotAutoInterval = 5f;

    private readonly DB_기물[] slotData  = new DB_기물[SlotCount];
    private readonly int[]     slotCount = new int[SlotCount];
    private float              autoTimer;

    protected override bool DontDestroy => false;

    public event System.Action OnChanged;
    public event System.Action OnAutoChanged;

    public bool IsAutoMode     { get; private set; }
    /// <summary>AutoMode에서 다음으로 사용될 슬롯 인덱스. 없으면 -1.</summary>
    public int  ActiveAutoSlot { get; private set; } = -1;

    public void ToggleAuto()
    {
        IsAutoMode = !IsAutoMode;
        autoTimer  = 0f;
        ActiveAutoSlot = IsAutoMode ? FindNextSlot(-1) : -1;
        OnAutoChanged?.Invoke();
    }

    private void Update()
    {
        if (!IsAutoMode) return;

        // 활성 슬롯이 없으면 (재고 소진 후 보충됐을 수 있으므로) 다시 탐색
        if (ActiveAutoSlot < 0)
        {
            ActiveAutoSlot = FindNextSlot(-1);
            if (ActiveAutoSlot < 0) return;
            autoTimer = 0f;
            OnAutoChanged?.Invoke();
        }

        autoTimer += Time.deltaTime;
        if (autoTimer >= slotAutoInterval)
        {
            autoTimer = 0f;
            int used = ActiveAutoSlot;
            UseAt(used);                                   // UseAt → OnChanged 발생
            ActiveAutoSlot = FindNextSlot(used);           // 다음 슬롯으로
            OnAutoChanged?.Invoke();
        }
    }

    /// <summary>from 다음부터 순환 탐색해 보유량이 있는 첫 슬롯 인덱스를 반환. 없으면 -1.</summary>
    private int FindNextSlot(int from)
    {
        for (int i = 1; i <= SlotCount; i++)
        {
            int idx = (from + i + SlotCount) % SlotCount;
            if (slotData[idx] != null && slotCount[idx] > 0) return idx;
        }
        return -1;
    }

    // ── 조회 ────────────────────────────────────────────────────────
    public DB_기물 GetData(int slot)  => slot >= 0 && slot < SlotCount ? slotData[slot]  : null;
    public int     GetCount(int slot) => slot >= 0 && slot < SlotCount ? slotCount[slot] : 0;

    // ── 획득 (기물 구슬 수집 시 호출) ───────────────────────────────
    public void Push(DB_기물 data)
    {
        if (data == null) return;

        // 같은 종류(설치 프리팹이 같은) 기물이 있으면 누적
        for (int i = 0; i < SlotCount; i++)
        {
            if (slotData[i] != null && slotData[i].f_설치프리팹 == data.f_설치프리팹) 
            { 
                slotCount[i]++; 
                OnChanged?.Invoke(); 
                return; 
            }
        }

        // 빈 슬롯에 새로 추가
        for (int i = 0; i < SlotCount; i++)
        {
            if (slotData[i] == null) { slotData[i] = data; slotCount[i] = 1; OnChanged?.Invoke(); return; }
        }

        Debug.LogWarning("[StructureInventory] 슬롯이 가득 찼습니다.");
    }

    // ── 사용 (버튼 클릭 시 호출) ────────────────────────────────────
    /// <param name="launchOriginWorld">발사 시작 월드 좌표. null이면 플레이어 위치 사용.</param>
    public void UseAt(int slot, Vector3? launchOriginWorld = null)
    {
        if (slot < 0 || slot >= SlotCount) return;
        if (slotData[slot] == null || slotCount[slot] <= 0) return;

        var player = PlayerController.Instance;
        if (player == null) return;

        var data = slotData[slot];
        slotCount[slot]--;
        if (slotCount[slot] <= 0) slotData[slot] = null;

        Vector3 origin = launchOriginWorld ?? (Vector3)(Vector2)player.Position;

        if (StructurePlacer.Shared != null)
            StructurePlacer.Shared.Place(data, origin);
        else if (data.f_설치프리팹 != null)
            Object.Instantiate(data.f_설치프리팹, (Vector3)(Vector2)player.Position, Quaternion.identity);

        OnChanged?.Invoke();
    }
}
