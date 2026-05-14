using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 인게임 업그레이드 패널용 캐릭터 포트레이트 렌더러.
/// HomeCharacterDisplay와 동일한 RT 카메라 패턴.
/// UpgradePanel이 열릴 때 DropIn(), 닫힐 때 DropOut()을 호출합니다.
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

    [Header("입장 연출")]
    [SerializeField] private float dropHeight      = 5f;
    [SerializeField] private float fallDuration    = 0.5f;
    [SerializeField] private float squashDuration  = 0.08f;
    [SerializeField] private float stretchDuration = 0.15f;
    [SerializeField] private float squashScaleY    = 0.65f;
    [SerializeField] private float squashScaleX    = 1.35f;

    [Header("퇴장 연출")]
    [SerializeField] private float exitHeight   = 5f;
    [SerializeField] private float exitDuration = 0.35f;

    private GameObject _currentModel;
    private Coroutine  _animCoroutine;

    protected override void Awake()
    {
        base.Awake();
        if (portraitCamera != null && renderTexture != null)
            portraitCamera.targetTexture = renderTexture;
    }

    private void Start()
    {
        SpawnModel();
        if (_currentModel != null) _currentModel.SetActive(false);
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
        var model = _currentModel;
        if (model == null) yield break;

        Vector3 landPos  = modelSpawnPoint != null ? modelSpawnPoint.position : model.transform.position;
        Vector3 startPos = landPos + Vector3.up * dropHeight;
        model.transform.position   = startPos;
        model.transform.localScale = Vector3.one;

        model.GetComponentInChildren<Animator>()?.Play("happy", 0, 0f);

        // 낙하 (ease-in)
        float elapsed = 0f;
        while (elapsed < fallDuration)
        {
            if (model == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fallDuration);
            model.transform.position = Vector3.LerpUnclamped(startPos, landPos, t * t);
            yield return null;
        }
        if (model == null) yield break;
        model.transform.position = landPos;

        // 찌그러짐
        elapsed = 0f;
        while (elapsed < squashDuration)
        {
            if (model == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / squashDuration);
            model.transform.localScale = new Vector3(
                Mathf.Lerp(1f, squashScaleX, t),
                Mathf.Lerp(1f, squashScaleY, t), 1f);
            yield return null;
        }

        // 복원
        elapsed = 0f;
        while (elapsed < stretchDuration)
        {
            if (model == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / stretchDuration);
            model.transform.localScale = new Vector3(
                Mathf.Lerp(squashScaleX, 1f, t),
                Mathf.Lerp(squashScaleY, 1f, t), 1f);
            yield return null;
        }
        if (model == null) yield break;
        model.transform.localScale = Vector3.one;
        _animCoroutine = null;
    }

    private IEnumerator DropOutRoutine(System.Action onComplete)
    {
        var model = _currentModel;
        if (model == null) { onComplete?.Invoke(); yield break; }

        Vector3 startPos = model.transform.position;
        Vector3 endPos   = startPos + Vector3.up * exitHeight;

        float elapsed = 0f;
        while (elapsed < exitDuration)
        {
            if (model == null) { onComplete?.Invoke(); yield break; }
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / exitDuration);
            float ease = t * t; // ease-in (가속 퇴장)
            model.transform.position = Vector3.LerpUnclamped(startPos, endPos, ease);
            yield return null;
        }

        if (model != null) model.SetActive(false);
        onComplete?.Invoke();
        _animCoroutine = null;
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
