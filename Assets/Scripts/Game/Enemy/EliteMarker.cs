/// <summary>
/// 엘리트 몬스터 프리팹에 EnemyController와 함께 부착합니다.
/// 죽으면 static OnEliteDied 이벤트를 발생시킵니다.
/// GameManager가 이 이벤트를 구독해 즉시 웨이브를 클리어합니다.
/// </summary>
public class EliteMarker : UnityEngine.MonoBehaviour
{
    public static event System.Action OnEliteDied;

    private EnemyController _ec;

    void Awake() => _ec = GetComponent<EnemyController>();

    void OnEnable()
    {
        if (_ec != null) _ec.OnDeath += HandleDeath;
    }

    void OnDisable()
    {
        if (_ec != null) _ec.OnDeath -= HandleDeath;
    }

    private void HandleDeath() => OnEliteDied?.Invoke();
}
