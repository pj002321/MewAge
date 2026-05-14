using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 업그레이드 패널용 캐릭터 포트레이트 렌더러.
/// UpgradePanel이 열릴 때 DropIn(), 닫힐 때 DropOut()을 호출합니다.
///
/// 입장: enter_portrait 트리거 → 애니메이션 완료 → 데칼 페이드인
/// 퇴장: 데칼 페이드아웃 → exit_portrait 트리거 → 애니메이션 완료 → 모델 비활성화
/// </summary>
public class InGameCharacterDisplay : MonoBehaviourSingleton<InGameCharacterDisplay>
{
    protected override bool DontDestroy => false;

    [Header("렌더 타겟")]
    public RenderTexture renderTexture;

    [Header("포트레이트 카메라 (RT 레이어만 렌더)")]
    public Camera portraitCamera;

    [Header("모델 스폰 위치")]
    public Transform modelSpawnPoint;

    [Header("포트레이트 UI 애니메이터 (RawImage 오브젝트)")]
    [SerializeField] private Animator portraitAnimator;

    [Header("데칼 (RT 씬 내 바닥 스프라이트)")]
    [SerializeField] private SpriteRenderer decalRenderer;
    [SerializeField] private float          decalFadeDuration = 0.2f;

    private static readonly int TEnterPortrait = Animator.StringToHash("enter_portrait");
    private static readonly int BExit          = Animator.StringToHash("bExit");

    private GameObject _currentModel;
    private Coroutine  _animCoroutine;

    protected override void Awake()
    {
        base.Awake();
        if (portraitCamera != null && renderTexture != null)
            portraitCamera.targetTexture = renderTexture;
        // UpgradePanel은 timeScale=0으로 일시정지되므로 UnscaledTime 강제 설정
        if (portraitAnimator != null)
            portraitAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
    }

    private void Start()
    {
        SpawnModel();
        if (_currentModel != null) _currentModel.SetActive(false);
        SetDecalAlpha(0f);
    }

    // ── 모델 로드 ─────────────────────────────────────────────────────

    private void SpawnModel()
    {
        if (_currentModel != null) { Destroy(_currentModel); _currentModel = null; }

        var charId = PlayerCharacterManager.Shared?.EquippedCharID;
        if (string.IsNullOrEmpty(charId)) return;

        var prefab = Resources.Load<GameObject>($"Character/{charId.ToLower()}_model");
        if (prefab == null)
        {
            this.DLog($"모델 없음: Character/{charId.ToLower()}_model");
            return;
        }

        Vector3    pos = modelSpawnPoint != null ? modelSpawnPoint.position : Vector3.zero;
        Quaternion rot = modelSpawnPoint != null ? modelSpawnPoint.rotation : Quaternion.identity;
        _currentModel = Instantiate(prefab, pos, rot);

        int layer = LayerMask.NameToLayer("RT");
        if (layer >= 0) SetLayerRecursive(_currentModel, layer);
    }

    // ── 공개 API ─────────────────────────────────────────────────────

    public void DropIn()
    {
        if (_currentModel == null) SpawnModel();
        if (_currentModel == null) return;

        _currentModel.SetActive(true);
        _currentModel.transform.position   = modelSpawnPoint != null ? modelSpawnPoint.position : Vector3.zero;
        _currentModel.transform.localScale  = Vector3.one;
        _currentModel.GetComponentInChildren<Animator>()?.Play("happy", 0, 0f);

        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(DropInRoutine());
    }

    public void DropOut(System.Action onComplete = null)
    {
        if (_currentModel == null || !_currentModel.activeSelf)
        {
            onComplete?.Invoke();
            return;
        }
        if (_animCoroutine != null) StopCoroutine(_animCoroutine);
        _animCoroutine = StartCoroutine(DropOutRoutine(onComplete));
    }

    // ── 연출 코루틴 ──────────────────────────────────────────────────

    private IEnumerator DropInRoutine()
    {
        SetDecalAlpha(0f);

        if (portraitAnimator != null)
        {
            portraitAnimator.SetBool(BExit, false);
            portraitAnimator.SetTrigger(TEnterPortrait);
        }
        yield return StartCoroutine(PlayPortraitAnim("enter_portrait"));

        yield return StartCoroutine(FadeDecal(0f, 1f));
        _animCoroutine = null;
    }

    private IEnumerator DropOutRoutine(System.Action onComplete)
    {
        yield return StartCoroutine(FadeDecal(1f, 0f));

        if (portraitAnimator != null) portraitAnimator.SetBool(BExit, true);
        yield return StartCoroutine(PlayPortraitAnim("exit_portrait"));
        if (portraitAnimator != null) portraitAnimator.SetBool(BExit, false);

        if (_currentModel != null) _currentModel.SetActive(false);
        onComplete?.Invoke();
        _animCoroutine = null;
    }

    /// <summary>
    /// stateName 상태가 실제로 재생 완료될 때까지 대기합니다.
    /// GetNextAnimatorStateInfo로 "전환 대상"을 확인하므로 이전 상태 오탐을 방지합니다.
    /// </summary>
    private IEnumerator PlayPortraitAnim(string stateName)
    {
        if (portraitAnimator == null) yield break;

        yield return null; // 파라미터 처리 대기

        // Phase 1: stateName 상태로의 전환이 시작될 때까지 대기 (최대 1초)
        float elapsed = 0f;
        while (elapsed < 1f)
        {
            if (portraitAnimator == null) yield break;

            if (portraitAnimator.IsInTransition(0))
            {
                // 전환 대상이 목표 상태면 탈출
                if (portraitAnimator.GetNextAnimatorStateInfo(0).IsName(stateName)) break;
            }
            else
            {
                // 이미 목표 상태에 진입해 있으면 탈출
                if (portraitAnimator.GetCurrentAnimatorStateInfo(0).IsName(stateName)) break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (portraitAnimator == null) yield break;

        // Phase 2: 전환 완료 + stateName 상태 재생 완료 대기 (최대 5초)
        elapsed = 0f;
        while (elapsed < 5f)
        {
            if (portraitAnimator == null) yield break;

            var info = portraitAnimator.GetCurrentAnimatorStateInfo(0);
            if (!portraitAnimator.IsInTransition(0) &&
                info.IsName(stateName) &&
                info.normalizedTime >= 1f)
                break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // ── 데칼 헬퍼 ────────────────────────────────────────────────────

    private IEnumerator FadeDecal(float from, float to)
    {
        if (decalRenderer == null) yield break;
        float elapsed = 0f;
        while (elapsed < decalFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetDecalAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / decalFadeDuration)));
            yield return null;
        }
        SetDecalAlpha(to);
    }

    private void SetDecalAlpha(float alpha)
    {
        if (decalRenderer == null) return;
        var c = decalRenderer.color;
        c.a = alpha;
        decalRenderer.color = c;
    }

    // ────────────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        if (_currentModel != null) Destroy(_currentModel);
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }
}
