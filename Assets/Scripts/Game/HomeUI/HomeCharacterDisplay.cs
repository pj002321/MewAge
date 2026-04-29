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
///   각 패널의 RawImage.texture = renderTexture 에셋 연결
///
/// ■ 레이어 설정 (Unity 에디터에서 수동):
///   1. Project Settings → Tags and Layers → Layer 추가: "RT"
///   2. portraitCamera.cullingMask = RT만 체크
///   3. 메인 카메라 cullingMask에서 RT 해제 (인게임에서는 Default 레이어 사용)
/// </summary>
public class HomeCharacterDisplay : MonoBehaviourSingleton<HomeCharacterDisplay>
{
    protected override bool DontDestroy => false; // 홈씬에서만 유효

    [Header("렌더 타겟")]
    public RenderTexture renderTexture;

    [Header("포트레이트 카메라 (CharacterPortrait 레이어만 렌더)")]
    public Camera portraitCamera;

    [Header("모델 스폰 위치")]
    public Transform modelSpawnPoint;

    private GameObject _currentModel;

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

        // RT 레이어 적용 (카메라 cullingMask와 일치해야 함)
        int layer = LayerMask.NameToLayer("RT");
        if (layer >= 0) SetLayerRecursive(_currentModel, layer);
    }

    /// <summary>현재 모델의 Animator에서 "happy" 클립을 재생합니다.</summary>
    public void PlayHappy()
    {
        if (_currentModel == null) return;
        var anim = _currentModel.GetComponentInChildren<Animator>();
        if (anim != null) anim.Play("happy", 0, 0f);
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
