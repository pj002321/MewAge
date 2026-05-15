using UnityEngine;
using UnityEngine.UI;

public class HomeEventPanel : MonoBehaviour
{
    [SerializeField] Button          comingSoonButton;
    [SerializeField] MainMenuPanel   mainMenuPanel;

    void Awake()
    {
        comingSoonButton?.onClick.AddListener(() => mainMenuPanel?.baseNoti?.Show("업데이트 예정입니다"));
    }
}
