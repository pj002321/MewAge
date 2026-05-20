using UnityEngine;
using UnityEngine.UI;

public class HomeEventPanel : MonoBehaviour
{
    [SerializeField] Button comingSoonButton;

    void Awake()
    {
        comingSoonButton?.onClick.AddListener(() => PopupBaseNoti.Shared?.Show("업데이트 예정입니다"));
    }
}
