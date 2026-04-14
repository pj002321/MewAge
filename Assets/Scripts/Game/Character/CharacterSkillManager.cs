using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 고유 스킬 포인트·발동을 관리합니다.
///
/// ■ 흐름
///   PlayerCharacterManager.ApplyCharacterStats() → Initialize(skillId)
///   적 처치 → AddPoint(1)
///   게이지 가득 참 → skillButton 활성화
///   버튼 클릭 → TryActivate() → 스킬 발동 → duration 후 자동 종료
///
/// ■ 프리팹 배치
///   Resources/Skills/{skillId.ToLower()}.prefab 에 CharacterSkillBase 서브클래스를 부착하세요.
/// </summary>
public class CharacterSkillManager : MonoBehaviourSingleton<CharacterSkillManager>
{
    [Header("UI")]
    [SerializeField] private Button skillButton;
    [SerializeField] private Slider skillGaugeSlider;
    [SerializeField] private GameObject skillReadyFX; // 꽉 찼을 때 표시할 이펙트 (선택)

    [Header("기본 스킬 (프리팹 없을 때)")]
    [SerializeField] private float      defaultSpeedMultiplier = 5f;
    [SerializeField] private float      defaultDuration        = 3f;
    [SerializeField] private int        defaultPointsRequired  = 10;
    [SerializeField] private GameObject skillEffectPrefab;     // 스킬 발동 시 플레이어 위치에 스폰

    protected override bool DontDestroy => false;

    private CharacterSkillBase _skill;
    private int        _currentPoints;
    private int        _maxPoints;
    private bool       _isActive;
    private bool       _defaultBoostActive;
    private Coroutine  _activeRoutine;
    private GameObject _spawnedEffect;

    // ── 초기화 ─────────────────────────────────────────────────────

    /// <summary>캐릭터 장착 시 호출 — 스킬 프리팹을 로드하고 준비합니다.</summary>
    public void Initialize(string skillId)
    {
        // 이전 스킬 정리
        if (_skill != null)
        {
            StopSkill();
            Destroy(_skill.gameObject);
            _skill = null;
        }

        _currentPoints = 0;
        _isActive      = false;
        _skill         = null;

        if (!string.IsNullOrEmpty(skillId))
        {
            var prefab = Resources.Load<GameObject>($"Skills/{skillId.ToLower()}");
            if (prefab != null)
            {
                var parent = PlayerController.Instance != null
                    ? PlayerController.Instance.transform
                    : transform;
                var go = Instantiate(prefab, parent);
                go.transform.localPosition = Vector3.zero;
                _skill = go.GetComponent<CharacterSkillBase>();
            }
        }

        _maxPoints = _skill != null ? _skill.PointsRequired : defaultPointsRequired;

        skillButton?.gameObject.SetActive(true);
        if (skillGaugeSlider)
        {
            skillGaugeSlider.gameObject.SetActive(true);
            skillGaugeSlider.minValue = 0f;
            skillGaugeSlider.maxValue = _maxPoints;
            skillGaugeSlider.value    = 0f;
        }

        SetButtonInteractable(false);
        if (skillReadyFX) skillReadyFX.SetActive(false);
    }

    protected override void Awake()
    {
        base.Awake();
        skillButton?.onClick.AddListener(TryActivate);

        // 씬 시작 시 Initialize 전까지 숨김
        skillButton?.gameObject.SetActive(false);
        if (skillGaugeSlider) skillGaugeSlider.gameObject.SetActive(false);
    }

    // ── 포인트 관리 ─────────────────────────────────────────────────

    /// <summary>적 처치 시 호출합니다. (EnemyController에서 호출)</summary>
    public void AddPoint(int amount = 1)
    {
        if (_isActive) return;
        if (_maxPoints <= 0) _maxPoints = defaultPointsRequired;
        if (_currentPoints >= _maxPoints) return;

        _currentPoints = Mathf.Min(_currentPoints + amount, _maxPoints);
        RefreshUI();

        if (_currentPoints >= _maxPoints)
            OnGaugeFull();
    }

    // ── 스킬 발동 ──────────────────────────────────────────────────

    /// <summary>스킬 버튼 클릭 시 호출됩니다.</summary>
    public void TryActivate()
    {
        if (_isActive) return;
        if (_currentPoints < _maxPoints) return;

        FXManager.Shared?.SpawnPlayerText("FEVER !!", Color.blue);

        _isActive      = true;
        _currentPoints = 0;
        SetButtonInteractable(false);
        if (skillReadyFX) skillReadyFX.SetActive(false);
        RefreshUI();

        SpawnEffect();

        if (_skill != null)
        {
            _skill.Activate();
            _activeRoutine = StartCoroutine(SkillDurationRoutine(_skill.Duration));
        }
        else
        {
            // 프리팹 없을 때 기본 동작: 공격속도 부스트
            ApplyDefaultBoost(true);
            _activeRoutine = StartCoroutine(SkillDurationRoutine(defaultDuration));
        }
    }

    /// <summary>스킬을 즉시 강제 종료합니다.</summary>
    public void ForceStop()
    {
        if (!_isActive) return;
        StopSkill();
    }

    // ── 내부 ───────────────────────────────────────────────────────

    private IEnumerator SkillDurationRoutine(float dur)
    {
        yield return new WaitForSeconds(dur);
        StopSkill();
    }

    private void StopSkill()
    {
        if (_activeRoutine != null)
        {
            StopCoroutine(_activeRoutine);
            _activeRoutine = null;
        }
        if (_skill != null)
            _skill.Deactivate();
        else
            ApplyDefaultBoost(false);
        DespawnEffect();
        _isActive = false;
    }

    private void SpawnEffect()
    {
        if (skillEffectPrefab == null) return;
        if (_spawnedEffect != null) Destroy(_spawnedEffect);

        var parent = PlayerController.Instance != null ? PlayerController.Instance.transform : transform;
        _spawnedEffect = Instantiate(skillEffectPrefab, parent.position, Quaternion.identity, parent);
    }

    private void DespawnEffect()
    {
        if (_spawnedEffect == null) return;
        Destroy(_spawnedEffect);
        _spawnedEffect = null;
    }

    private void ApplyDefaultBoost(bool apply)
    {
        var wps = WeaponManager.Instance?.GetActiveWeaponPoints();
if (wps == null || wps.Count == 0) return;
        float mult = apply ? defaultSpeedMultiplier : 1f / defaultSpeedMultiplier;
        foreach (var wp in wps)
            wp.MultiplyAttackSpeed(mult);
        _defaultBoostActive = apply;
    }

    private void OnGaugeFull()
    {
        SetButtonInteractable(true);
        if (skillReadyFX) skillReadyFX.SetActive(true);
        TryActivate(); // 게이지 가득 차면 자동 발동
    }

    private void RefreshUI()
    {
        if (skillGaugeSlider)
            skillGaugeSlider.value = _currentPoints;
    }

    private void SetButtonInteractable(bool interactable)
    {
        if (skillButton) skillButton.interactable = interactable;
    }
}
