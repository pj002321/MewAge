using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 홈 씬 캐릭터 포트레이트 렌더링.
///
/// ■ 구성
///   - portraitCamera : cullingMask = RT 레이어만, targetTexture = renderTexture
///   - modelSpawnPoint: 카메라 앞 모델 배치 위치
///   - renderTexture  : 카메라 출력 대상 (RawImage들이 이 RT를 참조)
///
/// ■ 사용법
///   캐릭터 선택 시: HomeCharacterDisplay.Shared.ChangeCharacter(charId)
///   패널 열릴 때  : HomeCharacterDisplay.Shared.DropIn()
///
/// ■ 레이어 설정 (Unity 에디터에서 수동):
///   1. Project Settings → Tags and Layers → Layer 추가: "RT"
///   2. portraitCamera.cullingMask = RT만 체크
///   3. 메인 카메라 cullingMask에서 RT 해제 (인게임에서는 Default 레이어 사용)
/// </summary>
public class HomeCharacterDisplay : MonoBehaviourSingleton<HomeCharacterDisplay>
{
    protected override bool DontDestroy => false;

    [Header("렌더 타겟")]
    public RenderTexture renderTexture;

    [Header("포트레이트 카메라 (CharacterPortrait 레이어만 렌더)")]
    public Camera portraitCamera;

    [Header("모델 스폰 위치")]
    public Transform modelSpawnPoint;

    [Header("낙하 연출")]
    [SerializeField] private float dropHeight     = 5f;
    [SerializeField] private float fallDuration   = 0.6f;
    [SerializeField] private float squashDuration = 0.08f;
    [SerializeField] private float stretchDuration= 0.15f;
    [SerializeField] private float squashScaleY   = 0.65f;
    [SerializeField] private float squashScaleX   = 1.35f;

    private GameObject _currentModel;
    private Coroutine  _dropCoroutine;

    protected override void Awake()
    {
        base.Awake();
        if (portraitCamera != null && renderTexture != null)
            portraitCamera.targetTexture = renderTexture;
    }

    void Start()
    {
        PlayerCharacterManager.Shared?.EnsureDefaultCharacter();
        var charId = PlayerCharacterManager.Shared?.EquippedCharID;
        if (!string.IsNullOrEmpty(charId))
            ChangeCharacter(charId);
    }

    /// <summary>캐릭터 변경. 홈에서 캐릭터 선택 시 호출.</summary>
    public void ChangeCharacter(string charId)
    {
        if (_dropCoroutine != null)
        {
            StopCoroutine(_dropCoroutine);
            _dropCoroutine = null;
            if (_currentModel != null)
                _currentModel.transform.localScale = Vector3.one;
        }

        if (_currentModel != null)
        {
            Destroy(_currentModel);
            _currentModel = null;
        }

        var prefab = Resources.Load<GameObject>($"Character/{charId.ToLower()}_model");
        if (prefab == null)
        {
            Debug.LogWarning($"[HomeCharDisplay] 모델 프리팹 없음: Character/{charId.ToLower()}_model");
            return;
        }

        Vector3    spawnPos = modelSpawnPoint != null ? modelSpawnPoint.position : Vector3.zero;
        Quaternion spawnRot = modelSpawnPoint != null ? modelSpawnPoint.rotation : Quaternion.identity;
        _currentModel = Instantiate(prefab, spawnPos, spawnRot);

        int layer = LayerMask.NameToLayer("RT");
        if (layer >= 0) SetLayerRecursive(_currentModel, layer);
    }

    /// <summary>
    /// 모델을 위에서 낙하시키는 연출을 재생합니다.
    /// HomeMemberPanel이 열릴 때 호출하세요.
    /// </summary>
    public void DropIn()
    {
        if (_currentModel == null) return;
        if (_dropCoroutine != null) StopCoroutine(_dropCoroutine);
        _dropCoroutine = StartCoroutine(DropInRoutine());
    }

    private IEnumerator DropInRoutine()
    {
        var model = _currentModel;
        if (model == null) yield break;

        Vector3 landPos  = model.transform.position;
        Vector3 startPos = landPos + Vector3.up * dropHeight;

        model.transform.position   = startPos;
        model.transform.localScale = Vector3.one;

        // 낙하 시작과 동시에 happy 재생 (착지 후 자연스럽게 마무리됨)
        PlayHappy();

        // 낙하 (가속 ease-in)
        float elapsed = 0f;
        while (elapsed < fallDuration)
        {
            if (model == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fallDuration);
            model.transform.position = Vector3.LerpUnclamped(startPos, landPos, t * t);
            yield return null;
        }
        if (model == null) yield break;
        model.transform.position = landPos;

        // 압축 (squash)
        elapsed = 0f;
        while (elapsed < squashDuration)
        {
            if (model == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / squashDuration);
            model.transform.localScale = new Vector3(
                Mathf.Lerp(1f, squashScaleX, t),
                Mathf.Lerp(1f, squashScaleY, t),
                1f);
            yield return null;
        }

        // 복원 (stretch back)
        elapsed = 0f;
        while (elapsed < stretchDuration)
        {
            if (model == null) yield break;
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / stretchDuration);
            // 부드러운 overshoot 없이 단순 복원
            model.transform.localScale = new Vector3(
                Mathf.Lerp(squashScaleX, 1f, t),
                Mathf.Lerp(squashScaleY, 1f, t),
                1f);
            yield return null;
        }
        if (model == null) yield break;
        model.transform.localScale = Vector3.one;

        _dropCoroutine = null;
    }

    /// <summary>현재 모델의 Animator에서 "happy" 클립을 재생합니다.</summary>
    public void PlayHappy()
    {
        if (_currentModel == null) return;
        var anim = _currentModel.GetComponentInChildren<Animator>();
        if (anim != null) anim.Play("happy", 0, 0f);
    }

    public void PlaySad()
    {
        if (_currentModel == null) return;
        var anim = _currentModel.GetComponentInChildren<Animator>();
        if (anim != null) anim.Play("sad", 0, 0f);
    }

    void OnDestroy()
    {
        if (_currentModel != null) Destroy(_currentModel);
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }
}
