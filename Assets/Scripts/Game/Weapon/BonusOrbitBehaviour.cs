using UnityEngine;

/// <summary>
/// SYN_ALL3_HIGHEST_EXTRA 보너스 슬롯 전용 공전 컴포넌트.
/// 부모(anchorBonus) 기준으로 타원 공전하며, WeaponPoint가 사거리 안의 적을 정상 공격합니다.
/// WeaponManager.SetBonusWeapon()에서 자동으로 추가됩니다.
/// </summary>
public class BonusOrbitBehaviour : MonoBehaviour
{
    private float            _radius;
    private float            _speedDeg;
    private float            _angle;
    private SpriteRenderer[] _srs;
    private WeaponPoint      _wp;

    /// <param name="radius">공전 반경</param>
    /// <param name="speedDeg">초당 회전 각도 (양수=CCW, 음수=CW)</param>
    /// <param name="startAngleDeg">초기 각도 (여러 슬롯이 겹치지 않도록 랜덤 주입)</param>
    public void Init(float radius, float speedDeg, float startAngleDeg = 0f)
    {
        _radius   = radius;
        _speedDeg = speedDeg;
        _angle    = startAngleDeg;
        _srs      = GetComponentsInChildren<SpriteRenderer>();
        _wp       = GetComponent<WeaponPoint>();
    }

    private void Update()
    {
        // WeaponBehaviourBase가 localPosition을 직접 제어 중이면 공전을 양보합니다.
        // (웨이브 인트로, 근거리 무기 공격 모션 등)
        if (_wp != null && !_wp.IsBehaviourIdle) return;

        _angle += _speedDeg * Time.deltaTime;

        float rad = _angle * Mathf.Deg2Rad;
        transform.localPosition = new Vector3(
            Mathf.Cos(rad) * _radius,
            Mathf.Sin(rad) * _radius * 0.55f,  // Y 압축으로 타원 형태
            0f);

        // 공전 방향에 따라 스프라이트 좌우 반전
        // 속도 벡터 X = -sin(θ) * ω  →  음수면 왼쪽으로 이동
        float velX = -Mathf.Sin(rad) * _speedDeg;
        if (_srs != null)
            foreach (var sr in _srs)
                if (sr != null) sr.flipX = velX < 0f;
    }
}
