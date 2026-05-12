using System.Collections;
using UnityEngine;

/// <summary>
/// 피격/상태이상 실루엣 플래시.
/// · flashColor : 피격 플래시 색 (플레이어=빨강, 적=흰색) — Inspector에서 컴포넌트별 지정
/// · 셰이더 색상은 _instanceMat._Color 로 제어합니다 (버텍스 컬러 아님).
/// · EnterPersistentState(gray) : 그로기 진입. GrogyEntryHighlight가 UpdatePersistentColor()로 페이드.
/// · ExitPersistentState()       : 그로기 종료 — 원본 머티리얼 복구.
/// </summary>
public class HitFlash : MonoBehaviour
{
    [SerializeField] private float    flashDuration = 0.1f;
    [SerializeField] private Material flashMaterial;
    [SerializeField] private Color    flashColor    = Color.white;

    private SpriteRenderer[] _srs;
    private Material[]       _origMats;
    private Color[]          _origColors;
    private Material         _instanceMat;   // 컴포넌트별 독립 인스턴스 — _Color로 색상 제어
    private Coroutine        _co;
    private bool             _flashing;
    private bool             _persistent;
    private Color            _persistentColor;

    [HideInInspector] public Color BaseColor = Color.white;

    private void Awake()
    {
        RefreshRenderers();
        if (flashMaterial != null)
        {
            _instanceMat = new Material(flashMaterial);
            _instanceMat.SetColor("_Color", flashColor);
        }
    }

    public void RefreshRenderers()
    {
        _srs = GetComponentsInChildren<SpriteRenderer>(true);
        if (_srs.Length == 0) _srs = GetComponentsInParent<SpriteRenderer>();
    }

    // ── 피격 플래시 ───────────────────────────────────────────────

    public void Flash()
    {
        if (_srs == null || _srs.Length == 0 || _instanceMat == null) return;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(FlashRoutine());
    }

    public void StopFlash()
    {
        if (!_flashing) return;
        if (_co != null) { StopCoroutine(_co); _co = null; }
        if (_persistent) SetMatColor(_persistentColor);
        else             Restore();
        _flashing = false;
    }

    // ── 지속 상태 (그로기 = 회색) ─────────────────────────────────

    /// <summary>
    /// 실루엣 머티리얼을 지속 적용합니다.
    /// 애니메이션은 GrogyEntryHighlight에서 UpdatePersistentColor()를 매 프레임 호출해 진행합니다.
    /// </summary>
    public void EnterPersistentState(Color persistentColor)
    {
        if (_instanceMat == null) return;
        // 플래시 중이었다면 원본 복구 후 스냅샷 (그래야 _instanceMat이 아닌 원본을 저장)
        if (_co != null) { StopCoroutine(_co); _co = null; }
        if (_isFlashing) { Restore(); _isFlashing = false; }
        if (!_persistent) SaveOriginals();
        _persistent      = true;
        _persistentColor = persistentColor;
        SetMatColor(Color.white);   // 애니메이션 시작점: 흰색 → GrogyEntryHighlight가 회색으로 페이드
        ApplyMaterial();
    }

    // 플래시 여부 별도 프로퍼티 (StopFlash 내부 guard용)
    private bool _isFlashing;

    /// <summary>BossController.GrogyEntryHighlight에서 매 프레임 호출 — 실루엣 색상 페이드.</summary>
    public void UpdatePersistentColor(Color color)
    {
        _persistentColor = color;
        if (_persistent && !_flashing) SetMatColor(color);
    }

    /// <summary>지속 상태를 해제하고 원본 머티리얼/색상을 복구합니다.</summary>
    public void ExitPersistentState()
    {
        if (!_persistent) return;
        if (_co != null) { StopCoroutine(_co); _co = null; _flashing = false; _isFlashing = false; }
        _persistent = false;
        SetMatColor(flashColor);    // 인스턴스 mat 색상을 원래 flashColor로 리셋
        Restore();
    }

    // ── 내부 ─────────────────────────────────────────────────────

    private IEnumerator FlashRoutine()
    {
        if (!_persistent && !_flashing) SaveOriginals();
        _flashing   = true;
        _isFlashing = true;
        SetMatColor(flashColor);
        ApplyMaterial();

        yield return new WaitForSecondsRealtime(flashDuration);

        if (_persistent) SetMatColor(_persistentColor);
        else             Restore();
        _flashing   = false;
        _isFlashing = false;
        _co = null;
    }

    private void SetMatColor(Color color)
    {
        if (_instanceMat != null) _instanceMat.SetColor("_Color", color);
    }

    private void ApplyMaterial()
    {
        foreach (var sr in _srs)
            if (sr) sr.sharedMaterial = _instanceMat;
    }

    private void SaveOriginals()
    {
        _origMats   = new Material[_srs.Length];
        _origColors = new Color[_srs.Length];
        for (int i = 0; i < _srs.Length; i++)
        {
            if (!_srs[i]) continue;
            _origMats[i]   = _srs[i].sharedMaterial;
            _origColors[i] = _srs[i].color;
        }
    }

    private void Restore()
    {
        if (_origMats == null) return;
        for (int i = 0; i < _srs.Length && i < _origMats.Length; i++)
        {
            if (!_srs[i] || _origMats[i] == null) continue;
            _srs[i].sharedMaterial = _origMats[i];
            _srs[i].color          = _origColors[i];
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _co = null;
        if (_persistent || _flashing || _isFlashing)
        {
            Restore();
            _flashing   = false;
            _isFlashing = false;
            _persistent = false;
        }
    }

    private void OnDestroy()
    {
        Restore();
        if (_instanceMat != null) Destroy(_instanceMat);
    }
}
