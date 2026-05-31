using System.Collections;
using UnityEngine;

public class SpriteSliceFX : MonoBehaviour
{
    public static void Play(SpriteRenderer source, Vector2 deathDir, int cols = 2, int rows = 2)
    {
        if (source == null || source.sprite == null) return;
        var go = new GameObject("SpriteSliceFX");
        go.AddComponent<SpriteSliceFX>().Initialize(source, deathDir, cols, rows);
    }

    private void Initialize(SpriteRenderer source, Vector2 deathDir, int cols, int rows)
    {
        Destroy(gameObject, 1.5f);

        var sprite = source.sprite;
        float ppu = sprite.pixelsPerUnit;

        float pieceLocalW = sprite.rect.width  / cols / ppu;
        float pieceLocalH = sprite.rect.height / rows / ppu;
        float spriteLocalW = sprite.rect.width  / ppu;
        float spriteLocalH = sprite.rect.height / ppu;

        Vector3 srcPos   = source.transform.position;
        Vector3 srcScale = source.transform.lossyScale;

        for (int xi = 0; xi < cols; xi++)
        {
            for (int yi = 0; yi < rows; yi++)
            {
                var rect = new Rect(
                    sprite.rect.x + xi * (sprite.rect.width  / cols),
                    sprite.rect.y + yi * (sprite.rect.height / rows),
                    sprite.rect.width  / cols,
                    sprite.rect.height / rows
                );
                var sub = Sprite.Create(sprite.texture, rect, Vector2.one * 0.5f, ppu);

                float localX = (xi + 0.5f) * pieceLocalW - spriteLocalW * 0.5f;
                float localY = (yi + 0.5f) * pieceLocalH - spriteLocalH * 0.5f;

                var piece = new GameObject($"p{xi}_{yi}");
                piece.transform.position = new Vector3(
                    srcPos.x + localX * srcScale.x,
                    srcPos.y + localY * srcScale.y,
                    srcPos.z
                );
                piece.transform.localScale = new Vector3(
                    Mathf.Abs(srcScale.x),
                    Mathf.Abs(srcScale.y),
                    1f
                );
                piece.transform.SetParent(transform, true);

                var sr = piece.AddComponent<SpriteRenderer>();
                sr.sprite         = sub;
                sr.color          = Color.red;
                sr.sortingLayerID = source.sortingLayerID;
                sr.sortingOrder   = source.sortingOrder;

                StartCoroutine(AnimatePiece(piece.transform, deathDir));
            }
        }
    }

    private IEnumerator AnimatePiece(Transform t, Vector2 deathDir)
    {
        float angle    = Random.Range(-25f, 25f);
        var   dir      = (Vector2)(Quaternion.Euler(0f, 0f, angle) * deathDir);
        var   vel      = dir * Random.Range(12f, 18f);
        float rotSpeed = Random.Range(-600f, 600f);
        float duration = Random.Range(0.2f, 0.35f);
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            t.position += new Vector3(vel.x, vel.y) * Time.deltaTime;
            t.Rotate(0f, 0f, rotSpeed * Time.deltaTime);

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(t.gameObject);
    }
}
