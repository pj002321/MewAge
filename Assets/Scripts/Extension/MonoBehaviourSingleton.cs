using UnityEngine;

public class MonoBehaviourSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T shared;
    public static T Shared => shared;

    protected virtual bool DontDestroy => true;

    protected virtual void Awake()
    {
        if (shared != null && shared != this)
        {
            Debug.LogWarning($"[{typeof(T).Name}] 중복 인스턴스 감지! 제거합니다: {gameObject.name}");
            Destroy(gameObject);
            return;
        }

        shared = this as T;

        if (DontDestroy)
        {
            Debug.Log($"[{typeof(T).Name}] DontDestroyOnLoad 적용: {gameObject.name}");
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Debug.Log($"[{typeof(T).Name}] DontDestroy가 false입니다: {gameObject.name}");
        }
    }

    public static T Create(string name = null, Transform parent = null)
    {
        if (shared != null) return shared;

        var g = new GameObject(name ?? typeof(T).ToString());
        if (parent != null)
            g.transform.SetParent(parent, false);

        shared = g.AddComponent<T>();
        return shared;
    }

    protected virtual void OnDestroy()
    {
        if (shared == this)
            shared = null;
    }
}
