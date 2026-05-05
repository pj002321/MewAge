using System.Collections;
using EasyTransition;
using UnityEngine;
using UnityEngine.UI;

public class SceneLoadingCanvas : MonoBehaviour
{

    [Header("트랜지션 (EasyTransitions)")]
    [SerializeField] public TransitionSettings transition;  // transitionIn/Out이 모두 포함된 설정 하나
}
