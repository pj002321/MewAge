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

    private void Start() => LoadFromPresets();

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
    // ── 프리셋 로드 ──────────────────────────────────────────────────

    /// <summary>아웃게임 프리셋에 등록된 기물을 인게임 슬롯에 1:1로 배정합니다.
    /// Push() 스태킹 로직을 우회해 프리셋 슬롯 0→0, 1→1, 2→2, 3→3으로 직접 할당합니다.</summary>
    private void LoadFromPresets()
    {
        var ware = StructureWareManager.Shared;
        if (ware == null) return;

        var assigned = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < SlotCount; i++)
        {
            string id = ware.GetPreset(i);
            if (string.IsNullOrEmpty(id)) continue;
            if (!assigned.Add(id)) continue; // 이미 다른 슬롯에 등록된 기물은 스킵

            var data = Mewage.DB.DB_기물.FindEntity(r => r.f_기물ID == id);
            if (data == null) continue;

            slotData[i]  = data;
            slotCount[i] = 1;
        }
        OnChanged?.Invoke();
    }

    // ── 기물 설치 ────────────────────────────────────────────────────
    /// <summary>
    /// DB_기물 행에서 ID를 읽어 Resources/Ware/{챕터}/{id_s}.prefab 을 로드해 설치합니다.
    /// ID 형식: C1_W0 → 챕터=C1, 경로=Ware/C1/c1_w0_s
    /// </summary>
    private static void SpawnStructure(DB_기물 data, Vector3 pos)
    {
        string id = GetStructureId(data);
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[StructureInventory] 기물 ID를 찾을 수 없음");
            return;
        }

        // C1_W0 → chapter=C1, filename=c1_w0
        string[] parts   = id.Split('_');
        string   chapter = parts.Length > 0 ? parts[0] : id;
        string   path    = $"Ware/{chapter}/{id.ToLower()}_s";

        var prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[StructureInventory] 설치 프리팹 없음: Resources/{path}");
            return;
        }

        Object.Instantiate(prefab, pos, Quaternion.identity);
        Debug.Log($"[StructureInventory] 기물 설치: {id} at {pos}");
    }

    /// <summary>DB_기물 행에서 ID 문자열을 가져옵니다.</summary>
    private static string GetStructureId(DB_기물 data)
    {
        if (data == null) return null;
        return data.f_기물ID;
    }

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

        // 같은 종류(기물ID가 같은) 기물이 있으면 누적
        for (int i = 0; i < SlotCount; i++)
        {
            if (slotData[i] != null && slotData[i].f_기물ID == data.f_기물ID) 
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

        Vector3 launchOrigin = launchOriginWorld ?? (Vector3)(Vector2)player.transform.position;
        StructurePlacer.Shared?.Place(data, launchOrigin);

        OnChanged?.Invoke();
    }
}
