using System.Collections;
using UnityEngine;

/// <summary>
/// 부착된 RectTransform을 SafeAreaAnchor를 무시하고 지정한 조상(fillTarget)까지 채웁니다.
/// 배경 이미지처럼 safe area 바깥까지 그려야 하는 UI에 사용합니다.
///
/// 사용법:
///   1. 배경 Image 오브젝트에 이 컴포넌트를 추가합니다.
///   2. Fill Target에 AspectContainer를 직접 연결합니다. (Inspector에서 반드시 지정)
///      비워두면 Canvas 루트를 자동 사용합니다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class FullScreenFiller : MonoBehaviour
{
    [Tooltip("배경이 채울 조상 RectTransform. 반드시 Inspector에서 AspectContainer를 직접 연결하세요.")]
    [SerializeField] private RectTransform fillTarget;

    [Header("마진 (fillTarget 기준, 양수 = 안쪽으로 줄임)")]
    [SerializeField] private float marginLeft   = 0f;
    [SerializeField] private float marginRight  = 0f;
    [SerializeField] private float marginBottom = 0f;
    [SerializeField] private float marginTop    = 0f;

    private RectTransform _rt;
    private RectTransform _parentRT;
    private Rect          _lastSafeArea;

    void Awake()
    {
        _rt       = GetComponent<RectTransform>();
        _parentRT = _rt.parent as RectTransform;

        // fillTarget 미지정 시 Canvas 루트까지 올라가서 자동 설정
        if (fillTarget == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                fillTarget = canvas.GetComponent<RectTransform>();
        }
    }

    void Start()
    {
        // 레이아웃 계산이 완료된 뒤 적용 (AspectRatioFitter가 먼저 실행되어야 함)
        StartCoroutine(ApplyNextFrame());
    }

    void OnEnable()
    {
        // 패널이 활성화될 때마다 재적용 (탭 전환 등)
        StartCoroutine(ApplyNextFrame());
    }

    void Update()
    {
        if (Screen.safeArea != _lastSafeArea)
            Apply();
    }

    private IEnumerator ApplyNextFrame()
    {
        yield return null; // 1프레임 대기 → Unity 레이아웃 패스 완료 후 적용
        Apply();
    }

    private void Apply()
    {
        _lastSafeArea = Screen.safeArea;

        if (_parentRT == null || fillTarget == null) return;

        // 앵커를 부모 전체로 리셋 (offset 기준점 통일)
        _rt.anchorMin = Vector2.zero;
        _rt.anchorMax = Vector2.one;
        _rt.offsetMin = Vector2.zero;
        _rt.offsetMax = Vector2.zero;

        // fillTarget과 부모의 코너 차이를 부모 로컬 좌표로 환산해 offset에 반영
        var parentCorners = new Vector3[4];
        var targetCorners = new Vector3[4];
        _parentRT.GetWorldCorners(parentCorners);
        fillTarget.GetWorldCorners(targetCorners);

        // corners[0]=좌하단, corners[2]=우상단
        Vector2 parentBL = _parentRT.InverseTransformPoint(parentCorners[0]);
        Vector2 parentTR = _parentRT.InverseTransformPoint(parentCorners[2]);
        Vector2 targetBL = _parentRT.InverseTransformPoint(targetCorners[0]);
        Vector2 targetTR = _parentRT.InverseTransformPoint(targetCorners[2]);

        Vector2 offsetMin = targetBL - parentBL;
        Vector2 offsetMax = targetTR - parentTR;

        // 마진 적용 (fillTarget 기준 안쪽으로 줄임)
        offsetMin.x += marginLeft;
        offsetMin.y += marginBottom;
        offsetMax.x -= marginRight;
        offsetMax.y -= marginTop;

        _rt.offsetMin = offsetMin;
        _rt.offsetMax = offsetMax;
    }
}
