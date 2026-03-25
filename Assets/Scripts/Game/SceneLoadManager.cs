using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬과 씬 사이를 전환할 때 "loading@scene"을 활용해 트랜지션을 연출하는 매니저입니다.
/// 
/// 사용법: SceneLoadManager.LoadScene("stage@scene");
/// 
/// 흐름:
///   1. loading@scene을 Additive로 로드하여 화면을 덮습니다.
///   2. 로딩 씬 안의 Canvas를 찾아 파괴되지 않게(DontDestroyOnLoad) 보호합니다.
///   3. 타겟 씬(예: stage@scene)을 Single 모드로 비동기 로드합니다 (다른 모든 씬은 언로드됨).
///   4. 타겟 씬 로딩이 완료(isDone)될 때까지 대기합니다.
///   5. 로딩 Canvas를 서서히 확대(Scale Up)한 뒤, 크기를 0으로 줄여 화면에서 사라지게 합니다.
///   6. 전환 연출이 끝난 로딩 Canvas를 파괴합니다.
/// 
/// # 조건:
///   - Build Settings에 "loading@scene"이 등록되어 있어야 합니다.
///   - loading@scene 안 최상단에 Canvas 객체가 하나 있어야 연출이 정상 작동합니다.
/// </summary>
public class SceneLoadManager : MonoBehaviour
{
    private static SceneLoadManager instance;
    private string targetSceneName;

    [Header("연출 설정")]
    private float scaleUpDuration   = 0.2f;  // 살짝 커지는 시간
    private float scaleUpMultiplier = 1.15f; // 커지는 최대 비율
    private float scaleDownDuration = 0.35f; // 0으로 줄어드는 시간

    /// <summary>지정한 이름의 씬으로 로딩 트랜지션을 시작합니다.</summary>
    public static void LoadScene(string sceneName)
    {
        if (instance == null)
        {
            var go   = new GameObject("SceneLoadManager");
            instance = go.AddComponent<SceneLoadManager>();
            DontDestroyOnLoad(go);
        }

        instance.targetSceneName = sceneName;
        instance.StartCoroutine(instance.LoadRoutine());
    }

    private IEnumerator LoadRoutine()
    {
        // 1. timeScale 정상화 (게임오버 시 0이었을 수 있음)
        Time.timeScale = 1f;

        // 2. loading@scene Additive 로드 (기존 씬 위에 즉시 띄움)
        yield return SceneManager.LoadSceneAsync("loading@scene", LoadSceneMode.Additive);

        // 3. 로딩 Canvas 찾아 최상단 렌더링 강제 및 보호
        var    loadingScene  = SceneManager.GetSceneByName("loading@scene");
        Canvas loadingCanvas = null;

        foreach (var root in loadingScene.GetRootGameObjects())
        {
            loadingCanvas = root.GetComponentInChildren<Canvas>();
            if (loadingCanvas != null) break;
        }

        if (loadingCanvas != null)
        {
            // 어떤 UI보다 무조건 렌더링을 맨 앞으로 가져옴
            loadingCanvas.sortingOrder   = 9999;
            loadingCanvas.renderMode     = RenderMode.ScreenSpaceOverlay;
            
            // 타겟 씬 로드 시 파괴되지 않게 루트를 보호
            DontDestroyOnLoad(loadingCanvas.transform.root.gameObject);

            // 로딩 화면이 뜨고 난 뒤, 유저에게 안정감(완전히 덮인 모습)을 주도록 잠시 대기
            // (이 시간에 기존 씬의 버벅임을 숨김)
            yield return new WaitForSecondsRealtime(0.5f);
        }

        // 4. 타겟 씬 비동기 로드 시작
        var asyncOp = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
        asyncOp.allowSceneActivation = false;

        // 에셋 로딩 대기
        while (asyncOp.progress < 0.9f)
            yield return null;

        // 씬 활성화
        asyncOp.allowSceneActivation = true;
        yield return new WaitUntil(() => asyncOp.isDone);

        // 5. 타겟 씬이 완전히 켜지고 Awake/Start가 실행되는 동안 생기는 프레임 드랍(스파이크)을 화면 뒤에 숨김
        yield return new WaitForSecondsRealtime(0.6f);

        // 6. 로딩 Canvas 연출 (확대하면서 오른쪽으로 슬라이드 아웃)
        if (loadingCanvas != null)
        {
            RectTransform uiRect = loadingCanvas.transform.childCount > 0 
                                 ? loadingCanvas.transform.GetChild(0) as RectTransform 
                                 : loadingCanvas.transform as RectTransform;

            if (uiRect != null)
            {
                Vector3 origScale = uiRect.localScale;
                Vector2 origPos   = uiRect.anchoredPosition;
                float   elapsed   = 0f;
                // 해상도 너비보다 넉넉하게 1.5배 이동시켜 화면 밖으로 완전히 밀어냄
                float   slideDist = Screen.width * 1.5f;
                float   duration  = scaleDownDuration + scaleUpDuration;

                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = elapsed / duration;
                    // 가속도 느낌 (Ease-In)
                    float easeIn = t * t * t; 

                    // 스케일은 서서히 커지고, 위치는 오른쪽으로 빠르게 빠짐
                    uiRect.localScale       = Vector3.LerpUnclamped(origScale, origScale * scaleUpMultiplier, t);
                    uiRect.anchoredPosition = origPos + new Vector2(slideDist * easeIn, 0f);

                    yield return null;
                }
            }

            Destroy(loadingCanvas.transform.root.gameObject);
        }
    }
}
