using System.Collections;
using UnityEngine;

/// <summary>
/// 탱커 방어막 시스템.
/// - 피격 시 방어막이 나타나며 shieldRoot 자식 SpriteRenderer들이 조각별로 표시됩니다.
/// - 방어막 HP가 줄어들수록 조각이 하나씩 사라집니다.
/// - 방어막이 소진되면 이후 피해는 EnemyController HP에 적용됩니다.
/// - rechargeDelay(초) 동안 피격이 없으면 방어막이 재충전됩니다.
///
/// 설정 방법:
///   1. 탱커 프리팹에 이 컴포넌트를 추가합니다.
///   2. Inspector의 shieldRoot에 방어막 스프라이트들의 부모 오브젝트를 연결합니다.
///      (knight_shield_05_0 ~ (6)을 담고 있는 GameObject)
/// </summary>
public class TankShield : MonoBehaviour
{
    [Header("방어막 설정")]
    [SerializeField] private GameObject shieldRoot;
    [SerializeField] private float shieldMaxHP    = 50f;
    [SerializeField] private float rechargeDelay  = 4f;   // 마지막 피격 후 재충전 시작까지 대기 시간
    [SerializeField] private float rechargeRate   = 20f;  // 초당 재충전량

    [Header("파괴 연출")]
    [SerializeField] private float breakFlashDuration = 0.25f;

    private SpriteRenderer[] _pieces;
    private float  _currentHP;
    private bool   _isVisible;
    private Coroutine _rechargeRoutine;
    private Coroutine _breakRoutine;

    public bool IsActive => _currentHP > 0f;

    private void Awake()
    {
        if (shieldRoot != null)
            _pieces = shieldRoot.GetComponentsInChildren<SpriteRenderer>(true);

        _currentHP = shieldMaxHP;
        SetVisible(false);
    }

    /// <summary>EnemyController.WakeFromPool에서 호출 — 방어막을 완전히 리셋합니다.</summary>
    public void ResetShield()
    {
        if (_rechargeRoutine != null) { StopCoroutine(_rechargeRoutine); _rechargeRoutine = null; }
        if (_breakRoutine    != null) { StopCoroutine(_breakRoutine);    _breakRoutine    = null; }

        _currentHP = shieldMaxHP;
        RestorePieceColors();
        UpdatePieces();
        SetVisible(false);
    }

    /// <summary>
    /// 피해를 방어막이 흡수합니다.
    /// 방어막이 모두 소진되면 남은 피해를 반환합니다.
    /// 방어막이 없으면 damage를 그대로 반환합니다.
    /// </summary>
    public float TryAbsorbDamage(float damage)
    {
        if (_currentHP <= 0f) return damage;

        // 방어막 표시 (최초 피격 시 등장)
        if (!_isVisible) SetVisible(true);

        // 재충전 중이었다면 중단
        if (_rechargeRoutine != null)
        {
            StopCoroutine(_rechargeRoutine);
            _rechargeRoutine = null;
        }

        float absorbed  = Mathf.Min(_currentHP, damage);
        float remaining = damage - absorbed;
        _currentHP -= absorbed;

        UpdatePieces();

        if (_currentHP <= 0f)
        {
            // 방어막 파괴
            if (_breakRoutine != null) StopCoroutine(_breakRoutine);
            _breakRoutine = StartCoroutine(BreakRoutine());
        }
        else
        {
            // 피격 후 재충전 예약
            _rechargeRoutine = StartCoroutine(RechargeRoutine());
        }

        return remaining;
    }

    // ── 조각 표시/숨김 ─────────────────────────────────────────

    private void UpdatePieces()
    {
        if (_pieces == null || _pieces.Length == 0) return;

        // HP 비율에 따라 표시할 조각 수 결정
        int showCount = Mathf.CeilToInt((_currentHP / shieldMaxHP) * _pieces.Length);
        for (int i = 0; i < _pieces.Length; i++)
            _pieces[i].enabled = i < showCount;
    }

    private void SetVisible(bool visible)
    {
        _isVisible = visible;
        if (shieldRoot != null) shieldRoot.SetActive(visible);
    }

    // ── 연출 코루틴 ────────────────────────────────────────────

    private IEnumerator BreakRoutine()
    {
        float elapsed = 0f;
        while (elapsed < breakFlashDuration)
        {
            float alpha = Mathf.Lerp(1f, 0f, elapsed / breakFlashDuration);
            SetPieceAlpha(alpha);
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetVisible(false);
        RestorePieceColors();
        _breakRoutine = null;

        // 방어막 파괴 후 재충전 예약
        _rechargeRoutine = StartCoroutine(RechargeRoutine());
    }

    private IEnumerator RechargeRoutine()
    {
        // 대기
        yield return new WaitForSeconds(rechargeDelay);

        // 서서히 재충전
        while (_currentHP < shieldMaxHP)
        {
            _currentHP = Mathf.Min(_currentHP + rechargeRate * Time.deltaTime, shieldMaxHP);
            UpdatePieces();

            if (!_isVisible && _currentHP > 0f)
                SetVisible(true);

            yield return null;
        }

        _rechargeRoutine = null;
    }

    // ── 헬퍼 ──────────────────────────────────────────────────

    private void SetPieceAlpha(float alpha)
    {
        if (_pieces == null) return;
        foreach (var p in _pieces)
            p.color = new Color(p.color.r, p.color.g, p.color.b, alpha);
    }

    private void RestorePieceColors()
    {
        if (_pieces == null) return;
        foreach (var p in _pieces)
            p.color = Color.white;
    }
}
