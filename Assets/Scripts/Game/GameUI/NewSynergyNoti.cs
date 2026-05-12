using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NewSynergyNoti : MonoBehaviour
{
    public Image synergyIconImg;
    public TextMeshProUGUI synergyNameText;

    [SerializeField] private float displayDuration = 2.5f;

    private Coroutine _hideCoroutine;
    private Animator  _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        gameObject.SetActive(false);
    }

    public void Show(Sprite icon, string name)
    {
        if (synergyIconImg != null)
        {
            synergyIconImg.sprite  = icon;
            synergyIconImg.enabled = icon != null;
        }
        if (synergyNameText != null)
            synergyNameText.text = name ?? string.Empty;

        gameObject.SetActive(true);

        if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
        _hideCoroutine = StartCoroutine(AutoHideRoutine());
    }

    private IEnumerator AutoHideRoutine()
    {
        if (_animator != null)
        {
            _animator.Play(0);
            yield return null;
            float clipLen = _animator.GetCurrentAnimatorStateInfo(0).length;
            yield return new WaitForSecondsRealtime(clipLen > 0.1f ? clipLen : displayDuration);
        }
        else
        {
            yield return new WaitForSecondsRealtime(displayDuration);
        }
        gameObject.SetActive(false);
        _hideCoroutine = null;
    }
}
