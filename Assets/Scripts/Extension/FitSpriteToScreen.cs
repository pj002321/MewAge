using UnityEngine;

/// <summary>
/// Scales the attached SpriteRenderer to fit the camera view.
/// Use this for backgrounds or full-screen sprites.
/// This component is purely for sprite scaling — it does NOT modify the camera.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class FitSpriteToScreen : MonoBehaviour
{
    public enum FitMode
    {
        Fill,     // 화면을 빈틈없이 채움 (스프라이트 일부 잘릴 수 있음)
        Fit,      // 스프라이트 전체가 보이도록 맞춤 (여백 생길 수 있음)
        Stretch,  // 가로/세로 독립 스케일 (비율 무시)
        FitWidth, // 좌우 끝만 화면에 딱 맞춤 (세로는 비율 유지)
        FitHeight // 상하 끝만 화면에 딱 맞춤 (가로는 비율 유지)
    }

    [SerializeField] private FitMode fitMode = FitMode.Fill;

    /// <summary>Fitted sprite's world-space size (post-scale). Other scripts can read this.</summary>
    public Vector2 FittedWorldSize { get; private set; }

    private float lastAspect = -1f;
    private float lastOrtho = -1f;

    private void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        if (Mathf.Abs(cam.aspect - lastAspect) > 0.001f || Mathf.Abs(cam.orthographicSize - lastOrtho) > 0.001f)
        {
            lastAspect = cam.aspect;
            lastOrtho  = cam.orthographicSize;
            Fit();
        }
    }

    public void Fit()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr.sprite == null) return;

        // 1. 스프라이트의 순수 원본 크기 (스케일 무시)
        Vector2 targetSize = new Vector2(sr.sprite.rect.width, sr.sprite.rect.height) / sr.sprite.pixelsPerUnit;

        // 2. 만약 MovementBounds에서 직접 지정한 하늘색 영역이 있다면, 이 영역이 화면에 꽉 차도록 타겟 크기 교체
        MovementBounds bounds = GetComponentInChildren<MovementBounds>();
        if (bounds != null && (bounds.ViewMode == MovementBounds.CameraViewMode.FixedView || bounds.UseCustomCameraBounds))
        {
            // 에디터에서 핸들로 그린 크기 자체를 타겟으로 삼음
            targetSize = new Vector2(bounds.CameraBoundsRect.width, bounds.CameraBoundsRect.height);
        }

        // zero div 방지
        if (targetSize.x <= 0.001f || targetSize.y <= 0.001f) return;

        float camHeight = cam.orthographicSize * 2f;
        float camWidth  = camHeight * cam.aspect;

        float scaleX = camWidth  / targetSize.x;
        float scaleY = camHeight / targetSize.y;

        if (fitMode == FitMode.Stretch)
        {
            transform.localScale = new Vector3(scaleX, scaleY, 1f);
            FittedWorldSize = new Vector2(camWidth, camHeight);
            return;
        }

        float scale = fitMode switch
        {
            FitMode.Fill      => Mathf.Max(scaleX, scaleY),
            FitMode.Fit       => Mathf.Min(scaleX, scaleY),
            FitMode.FitWidth  => scaleX,
            FitMode.FitHeight => scaleY,
            _                 => 1f
        };

        transform.localScale = new Vector3(scale, scale, 1f);
        FittedWorldSize = targetSize * scale;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 플레이 중에만 실행 — 에디터 정지 상태에서는 카메라가 없어 scale이 오염됨
        if (!Application.isPlaying) return;
        Fit();
    }
#endif
}
