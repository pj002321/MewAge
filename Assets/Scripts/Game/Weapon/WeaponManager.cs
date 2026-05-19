using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player에 부착. 모든 WeaponPoint를 관리합니다.
/// </summary>
public class WeaponManager : MonoBehaviour
{
    public static WeaponManager Instance { get; private set; }

    [Header("무기 앵커 (순서: 우상단·우하단·좌상단·좌하단·우·좌)")]
    [SerializeField] private Transform anchorTopRight;
    [SerializeField] private Transform anchorBottomRight;
    [SerializeField] private Transform anchorTopLeft;
    [SerializeField] private Transform anchorBottomLeft;
    [SerializeField] private Transform anchorRight;
    [SerializeField] private Transform anchorLeft;

    private Transform[] _anchors;

    private void Awake()
    {
        Instance = this;
        _anchors = new[]
        {
            anchorTopRight, anchorBottomRight,
            anchorTopLeft,  anchorBottomLeft,
            anchorRight,    anchorLeft,
        };
    }

    private void Start()
    {
        if (PlayerInventory.Shared != null)
        {
            PlayerInventory.Shared.OnChanged -= SyncWeapons; // 중복 방지
            PlayerInventory.Shared.OnChanged += SyncWeapons;
            SyncWeapons(); // 초기 동기화
        }
        else
        {
            this.DError("[WeaponManager] Start - PlayerInventory.Shared가 null입니다. 동기화 실패.");
        }
    }

    private void OnDestroy()
    {
        if (PlayerInventory.Shared != null)
        {
            PlayerInventory.Shared.OnChanged -= SyncWeapons;
        }
    }

    /// <summary>장착 슬롯과 _anchors(Transform) 위치 동기화</summary>
    public void SyncWeapons()
    {
        if (PlayerInventory.Shared == null) return;
        if (_anchors == null || _anchors.Length == 0)
        {
            this.DError("[WeaponManager] SyncWeapons - 앵커가 모두 null입니다. 인스펙터에서 방향별 Transform을 연결해주세요!");
            return;
        }

        var equipped = PlayerInventory.Shared.Equipped;

        for (int i = 0; i < _anchors.Length; i++)
        {
            Transform anchor = _anchors[i];
            if (anchor == null) continue;

            // 1. 기존 장착된 무기(자식)들 전부 제거
            // SetParent(null)로 즉시 anchor에서 분리 → ApplyStatSheet의 GetComponentsInChildren이
            // 삭제 대기 중인 구 무기를 탐색하지 않도록 방지
            for (int j = anchor.childCount - 1; j >= 0; j--)
            {
                var child = anchor.GetChild(j);
                child.SetParent(null);  // 즉시 계층에서 제거 (Destroy는 프레임 끝에 실행)
                Destroy(child.gameObject);
            }

            // 2. 장착 슬롯에 무기가 있다면 새로 생성
            if (i < equipped.Count && equipped[i] != null && equipped[i].prefab == null)
            {
                this.DError($"[WeaponManager] 슬롯 [{i}]: '{equipped[i].weaponName}' prefab 없음 → 무기 생성 스킵. Resources/Weapons/Ch{equipped[i].chapter}/{equipped[i].id?.ToLower()} 확인 필요.");
            }
            if (i < equipped.Count && equipped[i] != null && equipped[i].prefab != null)
            {
                GameObject weaponGO = Instantiate(equipped[i].prefab, anchor);
                weaponGO.transform.localPosition = Vector3.zero;
                weaponGO.transform.localRotation = Quaternion.identity;
                weaponGO.transform.localScale    = Vector3.one;

                // WeaponPoint 컴포넌트가 없으면 추가, 있으면 가져와서 Init
                if (!weaponGO.TryGetComponent<WeaponPoint>(out var wp))
                    wp = weaponGO.AddComponent<WeaponPoint>();
                wp.Init(equipped[i]);

                this.DLog($"[WeaponManager] 슬롯 [{i}]: '{equipped[i].weaponName}' 장착 완료 (부모: {anchor.name})");
            }
        }

        // 무기 생성/교체 후 현재 스탯 시트를 즉시 재적용
        var sm = PlayerStatManager.Shared;
        var player = PlayerController.Instance;
        if (sm != null && player != null)
            player.ApplyStatSheet(sm);
    }

    /// <summary>현재 장착된 모든 WeaponPoint를 반환합니다.</summary>
    public List<WeaponPoint> GetActiveWeaponPoints()
    {
        var result = new List<WeaponPoint>();
        if (_anchors == null) return result;
        var temp = new List<WeaponPoint>();
        foreach (var anchor in _anchors)
        {
            if (anchor == null) continue;
            anchor.GetComponentsInChildren<WeaponPoint>(temp);
            result.AddRange(temp);
        }
        return result;
    }
}
