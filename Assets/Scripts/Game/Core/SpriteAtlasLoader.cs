using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// 앱 시작 시 SpriteAtlas 런타임 로드 핸들러를 등록합니다.
/// Atlas 에셋은 Resources/Atlas/ 폴더에 위치해야 합니다.
/// [RuntimeInitializeOnLoadMethod]로 씬 로드 전 자동 실행됩니다.
/// </summary>
public static class SpriteAtlasLoader
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterAtlasHandler()
    {
        SpriteAtlasManager.atlasRequested += OnAtlasRequested;
    }

    private static void OnAtlasRequested(string tag, System.Action<SpriteAtlas> callback)
    {
        var atlas = Resources.Load<SpriteAtlas>($"Atlas/{tag}");
        if (atlas == null)
            atlas = Resources.Load<SpriteAtlas>(tag);
        callback(atlas);
    }
}
