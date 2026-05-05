using UnityEngine;

/// <summary>
/// 무기 등급에 따라 불꽃 아우라를 생성합니다.
/// 방식: 무기 스프라이트를 1.15배 확대한 복사본을 뒤에 깔고
///       WeaponFireOutline 셰이더로 불꽃 색 애니메이션.
/// WeaponPoint.Init() → Apply() 호출.
/// </summary>
public class WeaponAura : UnityEngine.MonoBehaviour
{
    private static readonly int FireColorInnerProp = UnityEngine.Shader.PropertyToID("_FireColorInner");
    private static readonly int FireColorOuterProp = UnityEngine.Shader.PropertyToID("_FireColorOuter");

    private UnityEngine.GameObject _auraGo;
    private UnityEngine.Material   _mat;

    private void OnDestroy() => Cleanup();

    private void Cleanup()
    {
        if (_auraGo != null) UnityEngine.Object.Destroy(_auraGo);
        if (_mat    != null) UnityEngine.Object.Destroy(_mat);
        _auraGo = null;
        _mat    = null;
    }

    public void Apply(UnityEngine.SpriteRenderer weaponSr, WeaponGrade grade)
    {
        Cleanup();
        if (weaponSr == null || weaponSr.sprite == null) return;
        if (grade == WeaponGrade.Common) return;

        var shader = UnityEngine.Shader.Find("Mewage/WeaponFireOutline");
        if (shader == null)
        {
            UnityEngine.Debug.LogWarning("[WeaponAura] WeaponFireOutline 셰이더 없음");
            return;
        }

        // 등급별 불꽃 색
        (UnityEngine.Color inner, UnityEngine.Color outer) = grade switch
        {
            WeaponGrade.Rare      => (new UnityEngine.Color(0.7f, 0.9f, 1.0f), new UnityEngine.Color(0.1f, 0.3f, 1.0f)),
            WeaponGrade.Epic      => (new UnityEngine.Color(0.95f, 0.7f, 1.0f), new UnityEngine.Color(0.5f, 0.0f, 1.0f)),
            WeaponGrade.Legendary => (new UnityEngine.Color(1.0f, 0.95f, 0.3f), new UnityEngine.Color(1.0f, 0.3f, 0.0f)),
            WeaponGrade.Mythic    => (new UnityEngine.Color(1.0f, 1.0f,  0.5f), new UnityEngine.Color(1.0f, 0.0f, 0.0f)),
            _                     => (UnityEngine.Color.white, UnityEngine.Color.grey)
        };

        _mat = new UnityEngine.Material(shader);
        _mat.SetColor(FireColorInnerProp, inner);
        _mat.SetColor(FireColorOuterProp, outer);

        // 확대 복사본 생성
        _auraGo = new UnityEngine.GameObject("WeaponFireAura");
        _auraGo.transform.SetParent(weaponSr.transform);
        _auraGo.transform.localPosition = UnityEngine.Vector3.zero;
        _auraGo.transform.localRotation = UnityEngine.Quaternion.identity;
        _auraGo.transform.localScale    = UnityEngine.Vector3.one * 1.18f; // 테두리 두께

        var sr = _auraGo.AddComponent<UnityEngine.SpriteRenderer>();
        sr.sprite         = weaponSr.sprite;
        sr.material       = _mat;
        sr.sortingLayerID = weaponSr.sortingLayerID;
        sr.sortingOrder   = weaponSr.sortingOrder - 1; // 무기 뒤에 렌더
    }
}
