using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace SG
{
    public class Utility_DestroyAfterTime : MonoBehaviour
    {
        [SerializeField] float timeUntilDestroyed = 5;

        public bool autoDestory = false;
        private void Awake()
        {
           if(autoDestory) Destroy(gameObject, timeUntilDestroyed);
        }

        public void AnimationEnd()
        {
            Destroy(gameObject);
        }
    }

}