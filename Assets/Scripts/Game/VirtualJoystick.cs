using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 터치 위치에 조이스틱이 나타나고, 드래그 방향으로 이동 벡터를 출력합니다.
/// Canvas (Screen Space - Overlay) 하위에 배치하세요.
/// 구성: Background(RectTransform) > Handle(RectTransform)
/// </summary>
public class VirtualJoystick : MonoBehaviour
{
    public static VirtualJoystick Instance { get; private set; }

    [SerializeField] private RectTransform background;   // 외부 원 (기준점)
    [SerializeField] private RectTransform handle;       // 내부 핸들 (움직이는 원)
    [SerializeField] private float handleRadius = 80f;   // 핸들 최대 이동 반경 (UI 픽셀)

    private Vector2 inputVector;
    private int     activeTouchId = -1;

    public Vector2 Direction => inputVector;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        // 업그레이드 패널 등 pause 상태에서는 조이스틱 비활성
        if (Time.timeScale == 0f)
        {
            if (background.gameObject.activeSelf) EndJoystick();
            return;
        }

        if (Input.touchSupported && Input.touchCount > 0)
            HandleTouch();
        else
            HandleMouse();
    }

    // ── 터치 입력 ──────────────────────────────────────
    private void HandleTouch()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);

            if (t.phase == TouchPhase.Began && activeTouchId == -1)
            {
                activeTouchId = t.fingerId;
                BeginJoystick(t.position);
            }
            else if (t.fingerId == activeTouchId)
            {
                if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                    MoveJoystick(t.position);
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    EndJoystick();
            }
        }
    }

    // ── 마우스 입력 (에디터 / PC) ──────────────────────
    private void HandleMouse()
    {
        if      (Input.GetMouseButtonDown(0)) BeginJoystick(Input.mousePosition);
        else if (Input.GetMouseButton(0))     MoveJoystick(Input.mousePosition);
        else if (Input.GetMouseButtonUp(0))   EndJoystick();
    }

    // ── 조이스틱 상태 제어 ─────────────────────────────
    private void BeginJoystick(Vector2 screenPos)
    {
        handle.anchoredPosition = Vector2.zero;
        inputVector             = Vector2.zero;
    }

    private void MoveJoystick(Vector2 screenPos)
    {
        // background 로컬 좌표계 기준 델타 계산 (Canvas 스케일 자동 보정)
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            background, screenPos, null, out Vector2 localDelta);

        float mag = localDelta.magnitude;
        inputVector = mag > handleRadius
            ? localDelta / mag                  // 정규화
            : localDelta / handleRadius;        // 0~1 범위

        handle.anchoredPosition = inputVector * handleRadius;
    }

    private void EndJoystick()
    {
        activeTouchId           = -1;
        inputVector             = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
        background.gameObject.SetActive(false);
    }
}
