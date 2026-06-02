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

    [Header("SYN_ALL3_HIGHEST_EXTRA 전용 보너스 슬롯")]
    [SerializeField] private Transform anchorBonus;
    [SerializeField] private float bonusOrbitRadius = 1.8f;
    [SerializeField] private float bonusOrbitSpeed  = 120f;

    private Transform[] _anchors;
    private Vector3[]   _anchorOrigPos;    // 이격 복구용 원본 localPosition
    private Vector3     _anchorBonusOrigPos;
    private Vector3[]   _anchorOrigScale;  // 크기 복구용 원본 localScale
    private Vector3     _anchorBonusOrigScale;

    private const float MeleeStaggerStep = 0.08f;

    private static bool IsMeleeRange(WeaponType t) =>
        t is WeaponType.Melee or WeaponType.Slash or WeaponType.Hammer or WeaponType.Spin or WeaponType.Charge;

    private void Awake()
    {
        Instance = this;
        _anchors = new[]
        {
            anchorTopRight, anchorBottomRight,
            anchorTopLeft,  anchorBottomLeft,
            anchorRight,    anchorLeft,
        };

        // 원본 위치·스케일 저장
        _anchorOrigPos   = new Vector3[_anchors.Length];
        _anchorOrigScale = new Vector3[_anchors.Length];
        for (int i = 0; i < _anchors.Length; i++)
        {
            _anchorOrigPos[i]   = _anchors[i] != null ? _anchors[i].localPosition : Vector3.zero;
            _anchorOrigScale[i] = _anchors[i] != null ? _anchors[i].localScale    : Vector3.one;
        }
        _anchorBonusOrigPos   = anchorBonus != null ? anchorBonus.localPosition : Vector3.zero;
        _anchorBonusOrigScale = anchorBonus != null ? anchorBonus.localScale    : Vector3.one;
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
        int meleeStaggerIdx = 0;

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

                if (IsMeleeRange(equipped[i].weaponType))
                    wp.SetMeleeStagger(meleeStaggerIdx++ * MeleeStaggerStep);

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

    /// <summary>모든 무기 앵커의 localScale을 원본 기준 배율로 변경합니다 (크기 모드 비주얼 전용).</summary>
    public void SetWeaponVisualScale(float multiplier)
    {
        if (_anchors != null)
            for (int i = 0; i < _anchors.Length; i++)
                if (_anchors[i] != null)
                    _anchors[i].localScale = _anchorOrigScale[i] * multiplier;
        if (anchorBonus != null)
            anchorBonus.localScale = _anchorBonusOrigScale * multiplier;
    }

    /// <summary>모든 무기 앵커의 localScale을 원본값으로 복구합니다.</summary>
    public void ResetWeaponVisualScale()
    {
        if (_anchors != null)
            for (int i = 0; i < _anchors.Length; i++)
                if (_anchors[i] != null)
                    _anchors[i].localScale = _anchorOrigScale[i];
        if (anchorBonus != null)
            anchorBonus.localScale = _anchorBonusOrigScale;
    }

    /// <summary>모든 무기 앵커의 localPosition을 원본 기준으로 multiplier배 이격합니다 (빅 모드 전용).</summary>
    public void SetWeaponAnchorSpread(float multiplier)
    {
        if (_anchors != null)
            for (int i = 0; i < _anchors.Length; i++)
                if (_anchors[i] != null)
                    _anchors[i].localPosition = _anchorOrigPos[i] * multiplier;
        if (anchorBonus != null)
            anchorBonus.localPosition = _anchorBonusOrigPos * multiplier;
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
        if (anchorBonus != null)
        {
            anchorBonus.GetComponentsInChildren<WeaponPoint>(temp);
            result.AddRange(temp);
        }
        return result;
    }

    // ── SYN_ALL3_HIGHEST_EXTRA 전용 보너스 슬롯 ──────────────────────

    /// <summary>보너스 앵커에 무기를 인스턴스화합니다. 이미 있으면 교체합니다.</summary>
    public void SetBonusWeapon(WeaponData data)
    {
        ClearBonusWeapon();
        if (anchorBonus == null)
        {
            this.DError("[WeaponManager] anchorBonus가 null입니다. Inspector에서 연결하세요.");
            return;
        }
        if (data?.prefab == null) return;

        var go = Instantiate(data.prefab, anchorBonus);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = Vector3.one;

        // 공전 컴포넌트 추가 (Spin 타입은 자체 localPosition 제어를 하므로 제외)
        if (data.weaponType != WeaponType.Spin)
        {
            var orbit = go.AddComponent<BonusOrbitBehaviour>();
            orbit.Init(bonusOrbitRadius, bonusOrbitSpeed,
                       startAngleDeg: Random.Range(0f, 360f));
        }

        if (!go.TryGetComponent<WeaponPoint>(out var wp))
            wp = go.AddComponent<WeaponPoint>();
        wp.Init(data);

        this.DLog($"[WeaponManager] 보너스 슬롯: '{data.weaponName}' 장착 완료 (공전 반경={bonusOrbitRadius})");
    }

    /// <summary>보너스 앵커의 무기를 제거합니다.</summary>
    public void ClearBonusWeapon()
    {
        if (anchorBonus == null) return;
        for (int i = anchorBonus.childCount - 1; i >= 0; i--)
        {
            var child = anchorBonus.GetChild(i);
            child.SetParent(null);
            Destroy(child.gameObject);
        }
    }
}
