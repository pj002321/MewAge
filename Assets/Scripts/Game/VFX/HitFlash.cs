using System.Collections;
using UnityEngine;

/// <summary>
/// 피격 시 SpriteSilhouette 머티리얼로 교체해 완전한 실루엣 플래시를 연출합니다.
/// flashMaterial에 SpriteSilhouette 셰이더 머티리얼을 연결하고,
/// flashColor로 실루엣 색상을 지정하세요 (흰색=일반 적, 빨간색=플레이어, 회색=그로기 등).
/// </summary>
public class HitFlash : MonoBehaviour
{
    [SerializeField] private float    flashDuration = 0.1f;
    [Tooltip("SpriteSilhouette 셰이더 머티리얼 (공유 에셋 — 색상은 flashColor로 제어)")]
    [SerializeField] private Material flashMaterial;
    [Tooltip("실루엣 색상. 흰색=적, 빨간색=플레이어, 회색=그로기")]
    [SerializeField] private Color    flashColor    = Color.white;

    private SpriteRenderer[] srs;
    private Material[]       _originalMats;
    private Color[]          _originalColors;
    private Material         _instanceMat;   // flashMaterial의 컴포넌트별 인스턴스 (색상 독립)
    private Coroutine        _flashCoroutine;
    private bool             _isFlashing;
    private bool             _isPersistent;
    private Color            _persistentColor;

    [HideInInspector] public Color BaseColor = Color.white;

    private void Awake()
    {
        RefreshRenderers();
        BuildInstanceMat();
    }

    public void RefreshRenderers()
    {
        srs = GetComponentsInChildren<SpriteRenderer>(true);
        if (srs.Length == 0) srs = GetComponentsInParent<SpriteRenderer>();
    }

    private void BuildInstanceMat()
    {
        if (flashMaterial == null) return;
        if (_instanceMat != null) Destroy(_instanceMat);
        _instanceMat = new Material(flashMaterial);
        _instanceMat.SetColor("_Color", flashColor);
    }

    public void Flash()
    {
        if (srs == null || srs.Length == 0) return;
        if (_instanceMat == null)
        {
            this.DError("[HitFlash] flashMaterial이 할당되지 않았습니다.");
            return;
        }
        if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        _flashCoroutine = StartCoroutine(FlashRoutine());
    }

    /// <summary>실루엣 머티리얼을 지속 적용합니다 (예: 그로기 회색). Flash()는 이 상태에서도 정상 작동합니다.</summary>
    public void EnterPersistentState(Color color)
    {
        if (_instanceMat == null) return;
        if (_flashCoroutine != null) { StopCoroutine(_flashCoroutine); _flashCoroutine = null; _isFlashing = false; }
        if (!_isPersistent) Snapshot();
        _isPersistent    = true;
        _persistentColor = color;
        foreach (var sr in srs)
            if (sr != null) { sr.sharedMaterial = _instanceMat; sr.color = Color.white; }
    }

    /// <summary>지속 상태를 해제하고 원본 머티리얼/색상을 복구합니다.</summary>
    public void ExitPersistentState()
    {
        if (!_isPersistent) return;
        if (_flashCoroutine != null) { StopCoroutine(_flashCoroutine); _flashCoroutine = null; _isFlashing = false; }
        _isPersistent = false;
        Restore();
    }

    /// <summary>진행 중인 플래시를 즉시 중단하고 원본 머티리얼/색상을 복구합니다.</summary>
    public void StopFlash()
    {
        if (!_isFlashing) return;
        if (_flashCoroutine != null) { StopCoroutine(_flashCoroutine); _flashCoroutine = null; }
        if (_isPersistent)
            foreach (var sr in srs) if (sr != null) sr.color = _persistentColor;
        else
            Restore();
        _isFlashing = false;
    }

    private IEnumerator FlashRoutine()
    {
        if (!_isPersistent && !_isFlashing) Snapshot();

        _isFlashing = true;
        foreach (var sr in srs)
            if (sr != null) { sr.sharedMaterial = _instanceMat; sr.color = Color.white; }

        yield return new WaitForSecondsRealtime(flashDuration);

        if (_isPersistent)
            foreach (var sr in srs) if (sr != null) sr.color = _persistentColor;
        else
            Restore();
        _isFlashing = false;
        _flashCoroutine = null;
    }

    private void Snapshot()
    {
        if (srs == null) return;
        _originalMats   = new Material[srs.Length];
        _originalColors = new Color[srs.Length];
        for (int i = 0; i < srs.Length; i++)
        {
            if (srs[i] == null) continue;
            _originalMats[i]   = srs[i].sharedMaterial;  // 인스턴스 생성 없이 공유 머티리얼 참조
            _originalColors[i] = srs[i].color;
        }
    }

    private void Restore()
    {
        if (srs == null || _originalMats == null) return;
        for (int i = 0; i < srs.Length && i < _originalMats.Length; i++)
        {
            if (srs[i] == null) continue;
            if (_originalMats[i] != null) srs[i].sharedMaterial = _originalMats[i];
            if (_originalColors != null && i < _originalColors.Length)
                srs[i].color = _originalColors[i];
        }
    }

    private void OnDisable()
    {
        if (_isPersistent || _isFlashing) { Restore(); _isFlashing = false; _isPersistent = false; }
        StopAllCoroutines();
    }

    private void OnDestroy()
    {
        Restore();
        if (_instanceMat != null) Destroy(_instanceMat);
    }
}
