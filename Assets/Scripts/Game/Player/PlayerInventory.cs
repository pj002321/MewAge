using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 장착 무기(최대 6)와 인벤토리(최대 10)를 관리합니다.
/// </summary>
public class PlayerInventory : MonoBehaviourSingleton<PlayerInventory>
{
    public const int MaxEquipped  = 6;
    public const int MaxInventory = 10;

    private readonly List<WeaponData> equipped  = new();
    private readonly List<WeaponData> inventory = new();

    /// <summary>장착/인벤 변경 시 발행됩니다. UpgradePanel이 구독합니다.</summary>
    public System.Action OnChanged;

    /// <summary>합성 직전에 발행 — 애니메이션용. 실제 슬롯 변경 전에 호출됩니다.</summary>
    public struct MergeAnimInfo
    {
        public List<int>        invenIndices;  // 소모되는 인벤 슬롯 인덱스
        public List<int>        equipIndices;  // 소모되는 장착 슬롯 인덱스
        public int              destEquipSlot; // 결과 장착 슬롯 (-1 = 인벤에 추가)
        public int              destInvenSlot; // 결과 인벤 슬롯 (destEquipSlot >= 0이면 무시)
        public Sprite           icon;
        public List<WeaponData> consumed;      // 소모된 2개 무기 데이터 (아이콘 참조용)
        // Destroy 이전에 캡처한 아이콘·등급 — queued 애니에서도 유효
        public List<Sprite>      consumedIcons;
        public List<WeaponGrade> consumedGrades;
        public bool             isGrandMerge;   // 3★+3★ → 상위등급 랜덤 무기 합성
        public WeaponData       resultWeapon;   // 애니 표시용 결과 (그랜드 머지: grade+1)
        public bool             isDoubleUp;     // 1% 더블업 머지 당첨
        public WeaponData       doubleUpResult; // 더블업 최종 결과 (grade+2, 실제 인벤 저장값)
    }
    public static event System.Action<MergeAnimInfo> OnBeforeMerge;

    protected override bool DontDestroy => false;

    public IReadOnlyList<WeaponData> Equipped  => equipped;
    public IReadOnlyList<WeaponData> Inventory => inventory;
    public bool InventoryFull
    {
        get { int n = 0; foreach (var w in inventory) if (w != null) n++; return n >= MaxInventory; }
    }

    // ────────────────────────────────────────────────────────────────
    // 인벤토리 추가
    // ────────────────────────────────────────────────────────────────
    public bool TryAddToInventory(WeaponData data)
    {
        // 중간 빈 슬롯(null) 우선 채움
        for (int i = 0; i < inventory.Count; i++)
            if (inventory[i] == null) { inventory[i] = data; OnChanged?.Invoke(); return true; }
        if (inventory.Count >= MaxInventory) return false;
        inventory.Add(data);
        OnChanged?.Invoke();
        return true;
    }

    // ────────────────────────────────────────────────────────────────
    // 인벤토리 → 장착 (장착 슬롯 꽉 차면 가장 낮은 등급 슬롯과 교체)
    // ────────────────────────────────────────────────────────────────
    public void EquipFromInventory(int invenIndex)
    {
        if (invenIndex < 0 || invenIndex >= inventory.Count) return;
        var weapon = inventory[invenIndex];

        // 첫 번째 빈(null) 슬롯 탐색, 없으면 MaxEquipped 이내 append
        int emptySlot = -1;
        for (int i = 0; i < equipped.Count; i++)
            if (equipped[i] == null) { emptySlot = i; break; }
        if (emptySlot < 0 && equipped.Count < MaxEquipped)
            emptySlot = equipped.Count;

        if (emptySlot >= 0)
        {
            if (emptySlot == equipped.Count) equipped.Add(weapon);
            else equipped[emptySlot] = weapon;
            inventory.RemoveAt(invenIndex);
        }
        else
        {
            // 6칸 모두 가득 찬 경우 — 가장 낮은 등급 슬롯과 교체
            int lowestIdx = FindLowestGradeSlot();
            var displaced = equipped[lowestIdx];
            equipped[lowestIdx] = weapon;
            inventory[invenIndex] = displaced;
        }
        OnChanged?.Invoke();
    }

    // ────────────────────────────────────────────────────────────────
    // 인벤토리 → 팔기 (판매가 = price / 2)
    // ────────────────────────────────────────────────────────────────
    public int SellFromInventory(int invenIndex)
    {
        if (invenIndex < 0 || invenIndex >= inventory.Count) return 0;
        int sellPrice = Mathf.Max(1, inventory[invenIndex].price / 2);
        inventory.RemoveAt(invenIndex);
        PlayerStats.Shared?.AddGold(sellPrice);
        OnChanged?.Invoke();
        return sellPrice;
    }

    // ────────────────────────────────────────────────────────────────
    // 장착 해제 → 인벤토리로
    // ────────────────────────────────────────────────────────────────
    /// <summary>장착 슬롯 → 인벤토리로 해제. 인벤토리 꽉 찼으면 false.</summary>
    public bool UnequipToInventory(int equipIndex)
    {
        if (equipIndex < 0 || equipIndex >= equipped.Count) return false;
        if (InventoryFull) return false;
        var weapon = equipped[equipIndex];
        for (int i = 0; i < inventory.Count; i++)
            if (inventory[i] == null) { inventory[i] = weapon; equipped[equipIndex] = null; TrimTrailingNullSlots(); OnChanged?.Invoke(); return true; }
        inventory.Add(weapon);
        equipped[equipIndex] = null;
        TrimTrailingNullSlots();
        OnChanged?.Invoke();
        return true;
    }

    // ────────────────────────────────────────────────────────────────
    // 장착 슬롯 → 팔기
    // ────────────────────────────────────────────────────────────────
    public int SellFromEquipped(int equipIndex)
    {
        if (equipIndex < 0 || equipIndex >= equipped.Count) return 0;
        int sellPrice = Mathf.Max(1, equipped[equipIndex].price / 2);
        equipped[equipIndex] = null;
        TrimTrailingNullSlots();
        PlayerStats.Shared?.AddGold(sellPrice);
        OnChanged?.Invoke();
        return sellPrice;
    }

    // ────────────────────────────────────────────────────────────────
    // 인벤토리 슬롯 → 특정 장착 슬롯 이동/교체
    // ────────────────────────────────────────────────────────────────
    public void MoveInvenToEquip(int invenIndex, int equipIndex)
    {
        if (invenIndex < 0 || invenIndex >= inventory.Count) return;
        if (equipIndex < 0 || equipIndex >= MaxEquipped) return;
        var weapon = inventory[invenIndex];

        // equipIndex까지 null로 확장 (빈 슬롯 포함)
        while (equipped.Count <= equipIndex) equipped.Add(null);

        if (equipped[equipIndex] != null)
        {
            // 장착 슬롯에 무기가 있으면 인벤과 교체
            inventory[invenIndex] = equipped[equipIndex];
            equipped[equipIndex]  = weapon;
        }
        else
        {
            // 빈 장착 슬롯에 이동 (정확한 equipIndex 위치로)
            equipped[equipIndex] = weapon;
            inventory.RemoveAt(invenIndex);
        }
        // 뒤의 null 정리
        while (equipped.Count > 0 && equipped[equipped.Count - 1] == null)
            equipped.RemoveAt(equipped.Count - 1);

        OnChanged?.Invoke();
    }

    // ────────────────────────────────────────────────────────────────
    // 인벤토리 슬롯 ↔ 인벤토리 슬롯 교체
    // ────────────────────────────────────────────────────────────────
    public void SwapInvenToInven(int a, int b)
    {
        if (a < 0 || b < 0 || a == b) return;
        if (a >= inventory.Count || b >= MaxInventory) return;

        // b가 현재 아이템 범위 밖이면 null로 채워 빈 슬롯 이동 허용
        while (inventory.Count <= b) inventory.Add(null);
        (inventory[a], inventory[b]) = (inventory[b], inventory[a]);

        // 끝의 null 정리
        while (inventory.Count > 0 && inventory[inventory.Count - 1] == null)
            inventory.RemoveAt(inventory.Count - 1);

        OnChanged?.Invoke();
    }

    // ────────────────────────────────────────────────────────────────
    // 합성 (3 same type+grade → 1 next grade)
    // ────────────────────────────────────────────────────────────────
    /// <summary>인벤토리에 같은 이름+등급 무기가 2개 이상 있는지 확인합니다.</summary>
    public bool CanMerge(string weaponName, int star)
    {
        int count = 0;
        foreach (var w in inventory)
            if (w != null && w.weaponName == weaponName && w.star == star)
                if (++count >= 2) return true;
        return false;
    }

    /// <summary>인벤 + 장착 슬롯 합산으로 2개 이상 있는지 확인합니다.</summary>
    public bool CanMergeAll(string weaponName, int star)
        => CountAll(weaponName, star) >= 2;

    /// <summary>인벤 + 장착 슬롯 합산 개수를 반환합니다.</summary>
    public int CountAll(string weaponName, int star)
    {
        int count = 0;
        foreach (var w in inventory)
            if (w != null && w.weaponName == weaponName && w.star == star) count++;
        foreach (var w in equipped)
            if (w != null && w.weaponName == weaponName && w.star == star) count++;
        return count;
    }

    /// <summary>인벤 내 같은 이름+성 2개를 제거하고 result를 인벤에 추가합니다.</summary>
    public bool TryMerge(string weaponName, int star, WeaponData result)
    {
        if (result == null) return false;

        var indices = new List<int>();
        for (int i = 0; i < inventory.Count && indices.Count < 2; i++)
        {
            var w = inventory[i];
            if (w != null && w.weaponName == weaponName && w.star == star)
                indices.Add(i);
        }

        if (indices.Count < 2) return false;

        // 합성 결과물 가격 = 소모 3개 합산
        int totalPrice = 0;
        foreach (int i in indices) totalPrice += Mathf.Max(1, inventory[i].price);
        result.price = totalPrice;

        // 높은 인덱스부터 제거해야 인덱스 밀림 없음
        for (int i = indices.Count - 1; i >= 0; i--)
            inventory.RemoveAt(indices[i]);

        inventory.Add(result);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>인벤 + 장착 슬롯을 합산해 2개를 소모하고 result를 배치합니다.
    /// 장착 슬롯에서 소모된 경우 result는 그 슬롯에 우선 배치됩니다.</summary>
    public bool TryMergeAll(string weaponName, int star, WeaponData result,
        WeaponData displayResult = null, bool isDoubleUp = false, WeaponData doubleUpResult = null)
    {
        if (result == null) return false;

        // 인벤 먼저, 그 다음 장착에서 최대 2개 수집
        var invenIdx = new List<int>();
        var equipIdx = new List<int>();

        for (int i = 0; i < inventory.Count && invenIdx.Count + equipIdx.Count < 2; i++)
        {
            var w = inventory[i];
            if (w != null && w.weaponName == weaponName && w.star == star)
                invenIdx.Add(i);
        }
        for (int i = 0; i < equipped.Count && invenIdx.Count + equipIdx.Count < 2; i++)
        {
            var w = equipped[i];
            if (w != null && w.weaponName == weaponName && w.star == star)
                equipIdx.Add(i);
        }

        if (invenIdx.Count + equipIdx.Count < 2) return false;

        // 소모 3개 목록 (가격용)
        var consumed = new List<WeaponData>();
        foreach (int i in invenIdx) consumed.Add(inventory[i]);
        foreach (int i in equipIdx) consumed.Add(equipped[i]);

        // 합성 결과물 가격 = 소모 3개 합산
        int totalPrice = 0;
        foreach (var w in consumed) totalPrice += Mathf.Max(1, w.price);
        result.price = totalPrice;

        // 결과 배치 슬롯 기억
        int resultEquipSlot = equipIdx.Count > 0 ? equipIdx[0] : -1;

        // 애니 목적지: 첫 번째 소모 인벤 슬롯 원본 인덱스 (슬롯 이동 없으므로 그대로 유효)
        int destInvenSlot = resultEquipSlot >= 0 ? -1
            : invenIdx.Count > 0 ? invenIdx[0]
            : inventory.Count;

        // ScriptableObject Destroy 전에 아이콘·등급 캡처
        // (queued 애니메이션은 Destroy 이후 프레임에서 실행되므로 반드시 미리 저장)
        var capIcons  = new List<Sprite>();
        var capGrades = new List<WeaponGrade>();
        foreach (var w in consumed)
        {
            capIcons.Add(w != null ? w.icon  : null);
            capGrades.Add(w != null ? w.grade : WeaponGrade.Common);
        }

        OnBeforeMerge?.Invoke(new MergeAnimInfo
        {
            invenIndices   = new List<int>(invenIdx),
            equipIndices   = new List<int>(equipIdx),
            destEquipSlot  = resultEquipSlot,
            destInvenSlot  = destInvenSlot,
            icon           = consumed.Count > 0 ? consumed[0].icon : null,
            consumed       = new List<WeaponData>(consumed),
            consumedIcons  = capIcons,
            consumedGrades = capGrades,
            isGrandMerge   = star >= 3,
            resultWeapon   = displayResult ?? result,
            isDoubleUp     = isDoubleUp,
            doubleUpResult = doubleUpResult,
        });

        // 장착 소모 (null 처리)
        for (int i = equipIdx.Count - 1; i >= 0; i--)
        {
            var w = equipped[equipIdx[i]];
            equipped[equipIdx[i]] = null;
            if (w != null) Destroy(w);
        }

        // 인벤 소모: null-in-place (다른 슬롯 이동 없이 해당 슬롯만 변경)
        if (resultEquipSlot >= 0)
        {
            // 결과가 장착 슬롯에 배치 → 인벤 소모 슬롯 전부 null
            for (int i = 0; i < invenIdx.Count; i++)
            {
                var w = inventory[invenIdx[i]];
                inventory[invenIdx[i]] = null;
                if (w != null) Destroy(w);
            }
        }
        else if (invenIdx.Count > 0)
        {
            // 결과가 인벤에 배치 → 첫 번째 소모 슬롯에 결과, 나머지는 null
            var w0 = inventory[invenIdx[0]];
            inventory[invenIdx[0]] = result;
            if (w0 != null) Destroy(w0);
            for (int i = 1; i < invenIdx.Count; i++)
            {
                var w = inventory[invenIdx[i]];
                inventory[invenIdx[i]] = null;
                if (w != null) Destroy(w);
            }
        }
        else
        {
            inventory.Add(result);
        }

        // 결과가 장착 슬롯에 배치되는 경우
        if (resultEquipSlot >= 0 && resultEquipSlot < equipped.Count)
            equipped[resultEquipSlot] = result;

        // 뒤의 null 장착 슬롯 정리
        while (equipped.Count > 0 && equipped[equipped.Count - 1] == null)
            equipped.RemoveAt(equipped.Count - 1);

        OnChanged?.Invoke();
        return true;
    }

    // ────────────────────────────────────────────────────────────────
    // 내부 헬퍼
    // ────────────────────────────────────────────────────────────────

    /// <summary>장착 슬롯에서 가장 낮은 등급의 슬롯 인덱스를 반환합니다.</summary>
    private int FindLowestGradeSlot()
    {
        int idx = 0;
        for (int i = 1; i < equipped.Count; i++)
        {
            if (equipped[i] == null) continue;
            var cur    = equipped[idx]?.grade ?? WeaponGrade.Mythic;
            var cand   = equipped[i].grade;
            if ((int)cand < (int)cur) idx = i;
        }
        return idx;
    }

    /// <summary>장착 리스트 끝의 null 슬롯을 제거합니다. WeaponPoint 싱크 보존을 위해 중간 null은 유지합니다.</summary>
    private void TrimTrailingNullSlots()
    {
        while (equipped.Count > 0 && equipped[equipped.Count - 1] == null)
            equipped.RemoveAt(equipped.Count - 1);
    }

    /// <summary>장착 슬롯의 무기를 교체합니다 (증강 등 외부 업그레이드용).</summary>
    public void ReplaceEquipped(int index, WeaponData data)
    {
        if (index < 0 || index >= equipped.Count) return;
        equipped[index] = data;
        OnChanged?.Invoke();
    }

    /// <summary>용량 무시하고 인벤에 추가합니다. 합성으로 슬롯이 바로 확보되는 경우에만 사용하세요.</summary>
    public void ForceAddToInventory(WeaponData data)
    {
        for (int i = 0; i < inventory.Count; i++)
            if (inventory[i] == null) { inventory[i] = data; OnChanged?.Invoke(); return; }
        inventory.Add(data);
        OnChanged?.Invoke();
    }

    /// <summary>디버그 전용 — 인벤 경유 없이 무기를 장착 슬롯에 직접 추가합니다. OnChanged 1회만 발행.</summary>
    public void QuietEquip(WeaponData data)
    {
        int emptySlot = -1;
        for (int i = 0; i < equipped.Count; i++)
            if (equipped[i] == null) { emptySlot = i; break; }
        if (emptySlot < 0 && equipped.Count < MaxEquipped)
            emptySlot = equipped.Count;

        if (emptySlot >= 0)
        {
            if (emptySlot == equipped.Count) equipped.Add(data);
            else equipped[emptySlot] = data;
        }
        else
        {
            equipped[FindLowestGradeSlot()] = data;
        }
        OnChanged?.Invoke();
    }

    // ────────────────────────────────────────────────────────────────
    // 드래그 & 드롭 교체
    // ────────────────────────────────────────────────────────────────
    /// <summary>장착 슬롯 A ↔ 장착 슬롯 B 교체</summary>
    public void SwapEquipToEquip(int a, int b)
    {
        if (a < 0 || b < 0 || a == b) return;
        // 빈 슬롯까지 허용 (equipped 범위 외이면 무시)
        if (a >= equipped.Count && b >= equipped.Count) return;

        // equipped 크기를 최소한 max(a,b)+1까지 null로 확장
        while (equipped.Count <= Mathf.Max(a, b)) equipped.Add(null);
        (equipped[a], equipped[b]) = (equipped[b], equipped[a]);
        // 뒤의 null 제거
        while (equipped.Count > 0 && equipped[equipped.Count - 1] == null)
            equipped.RemoveAt(equipped.Count - 1);

        OnChanged?.Invoke();
    }

    /// <summary>장착 슬롯 → 인벤토리 슬롯 이동/교체</summary>
    public void MoveEquipToInven(int equipIndex, int invenIndex)
    {
        if (equipIndex < 0 || equipIndex >= equipped.Count) return;
        var weapon = equipped[equipIndex];

        if (invenIndex < inventory.Count)
        {
            equipped[equipIndex] = inventory[invenIndex];
            inventory[invenIndex] = weapon;
        }
        else if (inventory.Count < MaxInventory)
        {
            inventory.Add(weapon);
            equipped[equipIndex] = null;
        }
        // 뒤의 null 장착 정리
        while (equipped.Count > 0 && equipped[equipped.Count - 1] == null)
            equipped.RemoveAt(equipped.Count - 1);

        OnChanged?.Invoke();
    }

    /// <summary>인벤토리를 같은 무기끼리 묶고 성급 내림차순으로 정렬합니다.</summary>
    public void SortInventory()
    {
        inventory.Sort((a, b) =>
        {
            if (a == null && b == null) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            int nameComp = string.Compare(a.weaponName, b.weaponName, System.StringComparison.Ordinal);
            return nameComp != 0 ? nameComp : b.star.CompareTo(a.star);
        });
        while (inventory.Count > 0 && inventory[inventory.Count - 1] == null)
            inventory.RemoveAt(inventory.Count - 1);
        OnChanged?.Invoke();
    }
}
