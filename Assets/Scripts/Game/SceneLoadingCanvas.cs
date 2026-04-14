using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SceneLoadingCanvas : MonoBehaviour
{
    [SerializeField] public Image iconImg;



    [Header("포물선 연출")]
    [SerializeField] private float throwDuration    = 1.0f;
    [SerializeField] private float horizontalForce  = 1.5f;   // Screen.width 배율
    [SerializeField] private float initialUpForce   = 0.55f;  // Screen.height 배율
    [SerializeField] private float gravityForce     = 2.8f;   // Screen.height 배율
    [SerializeField] private float spinSpeed        = 600f;   // 초당 회전(도)
    [SerializeField] private float fadeStartRatio   = 0.55f;  // 이 시점부터 페이드

    /// <summary>아이콘 애니메이션의 tendloading 트리거를 발동시키고 애니메이션이 끝날 때까지 기다립니다. SceneLoadManager에서 yield return합니다.</summary>
    public IEnumerator ThrowOut()
    {
        if (iconImg == null) yield break;

        if (iconImg.TryGetComponent(out Animator anim))
        {
            // 타임스케일이 0이더라도 애니메이션이 무조건 재생되도록 설정
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;
            
            anim.SetTrigger("tendloading");
            
            // 상태 전환 시작을 위해 한 프레임 대기
            yield return null; 

            float elapsed = 0f;
            float timeout = 3.0f; // 최대 3초 대기 (무한 루프 방지)

            // 트랜지션 중이거나 전환된 애니메이션의 재생이 다 끝나지 않은 동안 루프
            while ((anim.IsInTransition(0) || anim.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f) && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        iconImg.gameObject.SetActive(false);
    }
}
