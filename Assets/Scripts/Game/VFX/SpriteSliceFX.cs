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

    /// <summary>폭발 전용 — 조각이 중심에서 방사형으로 흩어지며 중력+페이드아웃됩니다.</summary>
    public static void PlayExplosion(SpriteRenderer source, int cols = 3, int rows = 3)
    {
        if (source == null || source.sprite == null) return;
        var go = new GameObject("SpriteSliceFX");
        go.AddComponent<SpriteSliceFX>().InitializeExplosion(source, cols, rows);
    }

    private void Initialize(SpriteRenderer source, Vector2 deathDir, int cols, int rows)
    {
        Destroy(gameObject, 1.5f);

        var sprite = source.sprite;
        float ppu = sprite.pixelsPerUnit;

        float pieceLocalW  = sprite.rect.width  / cols / ppu;
        float pieceLocalH  = sprite.rect.height / rows / ppu;
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

    private void InitializeExplosion(SpriteRenderer source, int cols, int rows)
    {
        Destroy(gameObject, 2f);

        var sprite = source.sprite;
        float ppu = sprite.pixelsPerUnit;

        float pieceLocalW  = sprite.rect.width  / cols / ppu;
        float pieceLocalH  = sprite.rect.height / rows / ppu;
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

                // 중심에서 바깥 방향
                var radialDir = new Vector2(localX * srcScale.x, localY * srcScale.y);
                if (radialDir.sqrMagnitude < 0.0001f)
                    radialDir = Random.insideUnitCircle.normalized;
                else
                    radialDir = radialDir.normalized;

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
                sr.color          = source.color;
                sr.sortingLayerID = source.sortingLayerID;
                sr.sortingOrder   = source.sortingOrder;

                StartCoroutine(AnimateExplosionPiece(piece.transform, sr, radialDir));
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

    private IEnumerator AnimateExplosionPiece(Transform t, SpriteRenderer sr, Vector2 radialDir)
    {
        float   speed      = Random.Range(4f, 9f);
        float   rotSpeed   = Random.Range(-360f, 360f);
        float   duration   = Random.Range(0.45f, 0.7f);
        float   elapsed    = 0f;
        const float gravity   = -12f;
        const float scalePeak = 2.2f; // 최대 크기 배율

        Vector2 vel        = radialDir * speed;
        Color   startColor = sr.color;
        Vector3 baseScale  = t.localScale;

        while (elapsed < duration)
        {
            vel.y += gravity * Time.deltaTime;
            t.position += new Vector3(vel.x, vel.y) * Time.deltaTime;
            t.Rotate(0f, 0f, rotSpeed * Time.deltaTime);

            float ratio = elapsed / duration;
            // 초반에 빠르게 커졌다가 페이드와 함께 유지
            float scaleMult = Mathf.Lerp(1f, scalePeak, Mathf.Sqrt(ratio));
            t.localScale = baseScale * scaleMult;

            float alpha = Mathf.Lerp(1f, 0f, ratio);
            sr.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(t.gameObject);
    }
}
