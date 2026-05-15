using UnityEngine;
using UnityEngine.UI;

public class HomeEventPanel : MonoBehaviour
{
    [SerializeField] Button          comingSoonButton;
    [SerializeField] PopupBaseNoti   baseNoti;

    void Awake()
    {
        comingSoonButton?.onClick.AddListener(() => baseNoti?.Show("업데이트 예정입니다"));
    }
}
