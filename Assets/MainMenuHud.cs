using UnityEngine;
using UnityEngine.UI;

public class MainMenuHud : MonoBehaviour
{
    [SerializeField] private Button exitButton;

    private void Start()
    {
        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitClicked);
    }

    private void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
