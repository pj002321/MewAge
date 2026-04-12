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
///   5. 로딩 Canvas를 페이드 아웃.
///   6. 전환 연출이 끝난 로딩 Canvas를 파괴합니다.
/// 
/// # 조건:
///   - Build Settings에 "loading@scene"이 등록되어 있어야 합니다.
///   - loading@scene 안 최상단에 Canvas 객체가 하나 있어야 정상 작동합니다.
/// </summary>
public class SceneLoadManager : MonoBehaviour
{
    private static SceneLoadManager instance;
    private string targetSceneName;

    [Header("연출 설정")]
    [SerializeField] private float fadeOutDuration = 0.5f;
    [SerializeField] private float readyDelay = 0.3f;
    [SerializeField] private float slideOutDuration = 0.4f;

    public static bool IsWaitingForStage { get; private set; }
    public static bool IsTransitioning { get; private set; }
    public static bool IsLoading => IsTransitioning;

    public static void NotifyStageReady()
    {
        IsWaitingForStage = false;
    }

    public static void LoadScene(string sceneName)
    {
        if (instance == null)
        {
            var go = new GameObject("SceneLoadManager");
            instance = go.AddComponent<SceneLoadManager>();
            DontDestroyOnLoad(go);
        }

        instance.targetSceneName = sceneName;
        instance.StartCoroutine(instance.LoadRoutine());
    }

    private IEnumerator LoadRoutine()
    {
        IsTransitioning = true;
        Time.timeScale = 1f;

        yield return SceneManager.LoadSceneAsync("loading@scene", LoadSceneMode.Additive);

        var loadingScene = SceneManager.GetSceneByName("loading@scene");
        Canvas loadingCanvas = null;
        CanvasGroup canvasGroup = null;

        foreach (var root in loadingScene.GetRootGameObjects())
        {
            loadingCanvas = root.GetComponentInChildren<Canvas>();
            if (loadingCanvas != null) break;
        }

        if (loadingCanvas != null)
        {
            canvasGroup = loadingCanvas.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = loadingCanvas.gameObject.AddComponent<CanvasGroup>();

            loadingCanvas.sortingOrder = 9999;
            loadingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            DontDestroyOnLoad(loadingCanvas.transform.root.gameObject);

            yield return new WaitForSecondsRealtime(0.5f);
        }

        var asyncOp = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
        asyncOp.allowSceneActivation = false;

        while (asyncOp.progress < 0.9f)
            yield return null;

        asyncOp.allowSceneActivation = true;
        yield return new WaitUntil(() => asyncOp.isDone);

        if (targetSceneName == "stage@scene")
        {
            IsWaitingForStage = true;
            while (IsWaitingForStage)
                yield return null;
            
            yield return new WaitForSecondsRealtime(readyDelay);
        }
        else
        {
            yield return new WaitForSecondsRealtime(0.6f);
        }

        // 6. 로딩 Canvas 옆으로 쓸 이동 (부드러운 전환)
        if (loadingCanvas != null)
        {
            RectTransform uiRect = loadingCanvas.transform.childCount > 0 
                ? loadingCanvas.transform.GetChild(0) as RectTransform 
                : loadingCanvas.transform as RectTransform;

            if (uiRect != null)
            {
                Vector2 origPos = uiRect.anchoredPosition;
                float slideDist = Screen.width * 1.2f;
                float elapsed = 0f;
                
                while (elapsed < slideOutDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = elapsed / slideOutDuration;
                    float ease = t * t * (3f - 2f * t);
                    uiRect.anchoredPosition = origPos + Vector2.right * slideDist * ease;
                    yield return null;
                }
            }
        }

        // 7. 로딩 Canvas 제거
        if (loadingCanvas != null)
            Destroy(loadingCanvas.transform.root.gameObject);

        IsTransitioning = false;
    }
}