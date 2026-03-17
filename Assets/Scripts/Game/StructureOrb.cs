using System.Collections;
using UnityEngine;
using Mewage.DB;

/// <summary>
/// 적 사망 시 드롭되는 기물 구슬.
/// 플레이어에게 자석처럼 이끌리고, 닿으면 StructurePlacer에 넘깁니다.
/// </summary>
public class StructureOrb : MonoBehaviour
{
    [SerializeField] private float magnetRadius = 4f;
    [SerializeField] private float moveSpeed    = 6f;
    [SerializeField] private float magnetDelay  = 0.5f;

    private DB_기물 data;
    private float   timer;
    private bool    attracted;
    private Vector3 origScale;

    public void Init(DB_기물 orbData)
    {
        data = orbData;
    }

    private void Start()
    {
        origScale = transform.localScale;
        StartCoroutine(SpawnPop());
    }

    private IEnumerator SpawnPop()
    {
        transform.localScale = Vector3.zero;
        float t = 0f;
        while (t < 0.2f)
        {
            t += Time.deltaTime;
            transform.localScale = origScale * Mathf.Lerp(0f, 1.2f, t / 0.2f);
            yield return null;
        }
        t = 0f;
        while (t < 0.1f)
        {
            t += Time.deltaTime;
            transform.localScale = origScale * Mathf.Lerp(1.2f, 1f, t / 0.1f);
            yield return null;
        }
        transform.localScale = origScale;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer < magnetDelay) return;

        var player = PlayerController.Instance;
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.Position);
        if (dist <= magnetRadius || attracted)
        {
            attracted = true;
            transform.position = Vector3.MoveTowards(
                transform.position, player.Position, moveSpeed * Time.deltaTime);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.TryGetComponent<PlayerController>(out _)) return;
        StructureInventory.Shared?.Push(data);
        Destroy(gameObject);
    }
}
