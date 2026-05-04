using System.Collections;
using EasyTransition;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 전환 매니저.
///
/// 흐름:
///   1. loading@scene 로드 → 캔버스/TransitionManager 찾아 DontDestroy 보호
///   2. openTransition 재생 → cut point에서 로딩 캔버스 표시 → out 완료까지 대기
///   3. 타겟 씬 로드 → 준비 완료 대기
///   4. ThrowOut (아이콘 연출)
///   5. closeTransition 재생 → cut point에서 로딩 캔버스 파괴 → out 완료까지 대기
///   6. TransitionManager 파괴
/// </summary>
public class SceneLoadManager : MonoBehaviour
{
    private static SceneLoadManager instance;
    private string targetSceneName;

    [Header("연출 설정")]
    [SerializeField] private float readyDelay      = 0.3f;
    [SerializeField] private float slideOutDuration = 0.4f;

    public static bool IsWaitingForStage { get; private set; }
    public static bool IsTransitioning   { get; private set; }
    public static bool IsLoading => IsTransitioning;

    public static void NotifyStageReady() => IsWaitingForStage = false;

    public static void LoadScene(string sceneName)
    {
        if (IsTransitioning)
        {
            Debug.LogWarning($"[SceneLoadManager] LoadScene({sceneName}) ignored — already transitioning.");
            return;
        }

        if (instance == null)
        {
            var go = new GameObject("SceneLoadManager");
            instance = go.AddComponent<SceneLoadManager>();
            DontDestroyOnLoad(go);
        }

        instance.targetSceneName = sceneName;
        instance.StartCoroutine(instance.LoadRoutine());
    }

    // ── 메인 루틴 ────────────────────────────────────────────────────

    private IEnumerator LoadRoutine()
    {
        IsTransitioning = true;
        Time.timeScale = 1f;

        // ── 1. loading@scene 로드 ──────────────────────────────────
        yield return SceneManager.LoadSceneAsync("loading@scene", LoadSceneMode.Additive);

        var loadingScene = SceneManager.GetSceneByName("loading@scene");

        Canvas             loadingCanvas    = null;
        SceneLoadingCanvas loadingCanvasComp = null;
        CanvasGroup        canvasGroup      = null;
        TransitionManager  loadingTM        = null;

        foreach (var root in loadingScene.GetRootGameObjects())
        {
            if (loadingCanvas == null)
                loadingCanvas = root.GetComponentInChildren<Canvas>();
            if (loadingTM == null)
                loadingTM = root.GetComponentInChildren<TransitionManager>();
            if (loadingCanvas != null && loadingTM != null) break;
        }

        if (loadingCanvas != null)
        {
            canvasGroup = loadingCanvas.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = loadingCanvas.gameObject.AddComponent<CanvasGroup>();

            loadingCanvas.sortingOrder = 9999;
            loadingCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            DontDestroyOnLoad(loadingCanvas.transform.root.gameObject);

            loadingCanvasComp = loadingCanvas.GetComponentInChildren<SceneLoadingCanvas>();

            foreach (var ws in loadingCanvas.GetComponentsInChildren<WaterScroll>())
                ws.enabled = false;

            // 로딩 캔버스를 처음에는 숨김 — openTransition cut point에서 공개
            canvasGroup.alpha          = 0f;
            canvasGroup.interactable   = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (loadingTM != null)
            DontDestroyOnLoad(loadingTM.gameObject);

        var transSettings = loadingCanvasComp?.transition;

        // ── 2. openTransition: 재생 → cut point에서 캔버스 공개 ───
        yield return StartCoroutine(PlayTransition(
            loadingTM, transSettings,
            onCutPoint: () =>
            {
                // 화면이 덮인 순간 로딩 캔버스 표시
                if (canvasGroup != null)
                {
                    canvasGroup.alpha          = 1f;
                    canvasGroup.blocksRaycasts = true;
                }
            }
        ));

        // transition 없을 때 fallback
        if (loadingTM == null || transSettings == null)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha          = 1f;
                canvasGroup.blocksRaycasts = true;
            }
            yield return new WaitForSecondsRealtime(0.5f);
        }

        // ── 3. 타겟 씬 로드 ────────────────────────────────────────
        var asyncOp = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
        asyncOp.allowSceneActivation = false;

        while (asyncOp.progress < 0.9f)
            yield return null;

        asyncOp.allowSceneActivation = true;
        yield return new WaitUntil(() => asyncOp.isDone);

        if (targetSceneName == "stage@scene")
        {
            IsWaitingForStage = true;
            while (IsWaitingForStage) yield return null;
            yield return new WaitForSecondsRealtime(readyDelay);
        }
        else
        {
            yield return new WaitForSecondsRealtime(0.6f);
        }

        // ── 5. closeTransition: 재생 → cut point에서 캔버스 파괴 ─
        yield return StartCoroutine(PlayTransition(
            loadingTM, transSettings,
            onCutPoint: () =>
            {
                // 화면이 다시 덮인 순간 로딩 캔버스 제거
                if (loadingCanvas != null)
                    Destroy(loadingCanvas.transform.root.gameObject);
            }
        ));

        // transition 없을 때 fallback
        if (loadingTM == null || transSettings == null)
            if (loadingCanvas != null)
                Destroy(loadingCanvas.transform.root.gameObject);

        // ── 6. TransitionManager 제거 ──────────────────────────────
        if (loadingTM != null)
            Destroy(loadingTM.gameObject);

        IsTransitioning = false;
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────

    /// <summary>
    /// TransitionManager로 transition을 재생하고 완전히 끝날 때까지 기다립니다.
    /// onCutPoint: 화면이 완전히 덮이는 순간 호출됩니다.
    /// TM 또는 settings가 null이면 즉시 반환합니다.
    /// </summary>
    private IEnumerator PlayTransition(TransitionManager tm, TransitionSettings settings,
        System.Action onCutPoint = null)
    {
        if (tm == null || settings == null) yield break;

        bool cutReached = false;
        bool ended      = false;

        UnityAction onCut = () => { if (!cutReached) { cutReached = true; onCutPoint?.Invoke(); } };
        UnityAction onEnd = () => ended = true;

        tm.onTransitionCutPointReached += onCut;
        tm.onTransitionEnd             += onEnd;

        tm.Transition(settings, 0f);

        yield return new WaitUntil(() => ended);

        tm.onTransitionCutPointReached -= onCut;
        tm.onTransitionEnd             -= onEnd;
    }

    private IEnumerator FallbackSlideOut(Canvas canvas)
    {
        var uiRect = canvas.transform.childCount > 0
            ? canvas.transform.GetChild(0) as RectTransform
            : canvas.transform as RectTransform;
        if (uiRect == null) yield break;

        Vector2 origPos = uiRect.anchoredPosition;
        float   elapsed = 0f;
        while (elapsed < slideOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t    = Mathf.Clamp01(elapsed / slideOutDuration);
            float ease = t * t * (3f - 2f * t);
            uiRect.anchoredPosition = origPos + Vector2.right * (Screen.width * 1.2f) * ease;
            yield return null;
        }
    }
}
