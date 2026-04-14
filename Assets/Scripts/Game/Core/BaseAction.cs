using UnityEngine;

namespace Utility
{
    public enum ActionType
    {
        ROTATE,
        SCALE,
    }

    public class BaseAction : MonoBehaviour
    {
        [SerializeField] private ActionType actionType;

        [SerializeField] private float value;

        private void ActionRotate(float v)
        {
            var t = transform;
            
            transform.Rotate(0,0, v);
        }

        private void Update()
        {
            ActionLoop(actionType);
        }

        private void ActionLoop(ActionType type)
        {
            switch (type)
            {
                case ActionType.ROTATE:
                    ActionRotate(value); break;
            }
        }
    }
}
