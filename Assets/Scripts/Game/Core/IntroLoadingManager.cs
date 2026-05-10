using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 또 다른 로딩 씬 (타이틀/인트로 등)에서 사용되는 로딩 매니저.
/// 게이지가 서서히 차오르고, 비동기 씬 로드가 끝나면 다음 씬으로 넘어갑니다.
/// </summary>
public class IntroLoadingManager : MonoBehaviour
{
    [Header("로딩 설정")]
    [Tooltip("로딩이 완료된 후 넘어갈 씬의 이름입니다.")]
    [SerializeField] private string targetSceneName = "home@scene";
    
    [Tooltip("로딩바가 가짜로 차오르는 데 걸리는 최소 시간(초)입니다.")]
    [SerializeField] private float minimumLoadingTime = 3.0f; 

    [Header("UI 연결 (선택 사항)")]
    [SerializeField] private Slider loadingSlider;
    [SerializeField] private Image loadingFillImage;
    [SerializeField] private TMP_Text loadingText;

    private void Start()
    {
        // 씬 진입 시 로딩 코루틴 자동 시작
        this.DLog("Intro/Splash 로딩을 시작합니다.");
        StartCoroutine(LoadingRoutine());
    }

    private IEnumerator LoadingRoutine()
    {
        float elapsed = 0f;

        // 프롤로그를 아직 보지 않은 유저면 home 대신 prologue로 보냄
        string destination = PlayerPrefs.GetInt(PrologueManager.SeenKey, 0) == 0
            ? "prologue@scene"
            : targetSceneName;

        // UI 초기화 세팅
        UpdateUI(0f);

        // 비동기 씬 로드 시작 (에러가 나도 진행될 수 있도록 처리)
        AsyncOperation asyncOp = null;
        try
        {
            asyncOp = SceneManager.LoadSceneAsync(destination);
        }
        catch (System.Exception e)
        {
            this.DError($"씬 로드 실패: {targetSceneName} 이/가 빌드 세팅에 있는지 확인하세요.\n{e.Message}");
            yield break;
        }

        if (asyncOp != null)
        {
            // 로드가 90%(백그라운드 완료) 되어도 자동으로 씬 전환하지 않게 설정
            asyncOp.allowSceneActivation = false;
        }

        // 가짜 대기 시간이 다 지났거나, 실제 로딩 진행도가 0.9 미만인 동안 계속 반복 (asyncOp.progress는 0.9에서 멈춤)
        while (elapsed < minimumLoadingTime || (asyncOp != null && asyncOp.progress < 0.9f))
        {
            elapsed += Time.deltaTime;
            
            // 1. 가짜 진행도: 정해둔 최소 시간에 비례하여 0~1로 증가
            float fakeProgress = Mathf.Clamp01(elapsed / minimumLoadingTime);
            
            // 2. 실제 진행도: asyncOp.progress의 최대치 0.9를 1.0(100%) 기준으로 정규화
            float realProgress = asyncOp != null ? Mathf.Clamp01(asyncOp.progress / 0.9f) : 1f;

            // 좀 더 부드럽고 자연스러운 게이지 진행 연출을 위해 두 진행률 중 더 작은 값을 차용
            float currentProgress = Mathf.Min(fakeProgress, realProgress);

            UpdateUI(currentProgress);

            yield return null;
        }

        // 반복문이 끝났다는 것 = 가짜시간도 다 지났고, 씬 로드도 다 끝났다는 뜻이므로 100% 강제 맞춤
        UpdateUI(1f);

        this.DLog("로딩 100% 완료, 목표 씬으로 전환합니다.");
        yield return new WaitForSeconds(0.2f); // 100%를 찍고 아주 잠깐 대기해야 안정감이 있음

        // 씬 전환 허용 (이제 홈 씬으로 넘어감)
        if (asyncOp != null)
        {
            asyncOp.allowSceneActivation = true;
        }
    }

    /// <summary>
    /// 연결된 UI(게이지 바, 텍스트) 요소를 업데이트합니다.
    /// </summary>
    private void UpdateUI(float progress)
    {
        if (loadingSlider != null) loadingSlider.value = progress;
        if (loadingFillImage != null) loadingFillImage.fillAmount = progress;
        if (loadingText != null) loadingText.text = $"{(progress * 100):F0}%";
    }
}
