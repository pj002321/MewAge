using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스프라이트 목록을 프레임 단위로 재생하는 컴포넌트.
/// Image 또는 SpriteRenderer에 붙여서 사용합니다.
/// </summary>
public class SpriteAnimation : MonoBehaviour
{
    [SerializeField] List<Sprite> frames;
    [SerializeField] float        fps      = 12f;
    [SerializeField] bool         loop     = true;
    [SerializeField] bool         playOnEnable = true;

    Image          _image;
    SpriteRenderer _sr;
    Coroutine      _co;

    void Awake()
    {
        _image = GetComponent<Image>();
        _sr    = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        if (playOnEnable) Play();
    }

    void OnDisable() => Stop();

    public void Play()
    {
        Stop();
        if (frames == null || frames.Count == 0) return;
        _co = StartCoroutine(Run());
    }

    public void Stop()
    {
        if (_co != null) { StopCoroutine(_co); _co = null; }
    }

    IEnumerator Run()
    {
        float interval = 1f / Mathf.Max(fps, 0.01f);
        int   index    = 0;

        while (true)
        {
            SetSprite(frames[index]);
            yield return new WaitForSecondsRealtime(interval);

            index++;
            if (index >= frames.Count)
            {
                if (loop) index = 0;
                else break;
            }
        }
        _co = null;
    }

    void SetSprite(Sprite sprite)
    {
        if (_image  != null) _image.sprite  = sprite;
        if (_sr     != null) _sr.sprite     = sprite;
    }
}
