using System;
using UnityEngine;

namespace TheBlackCat.TrailEffect2D
{
    public class TrailObject : MonoBehaviour
    {
        public TrailProcessor TrailProcessor => processor;

        private float elapsedTime;
        private bool ease;
        private Vector2 start, end;        
        private TrailInstance instance;
        private TrailProcessor processor;
        private SpriteRenderer sr;
        private Func<float, float, float> equation;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
        }

        public void Initialise(TrailProcessor processor, TrailInstance instance, Vector3 spawnPosition, Func<float, float, float> equation)
        {
            this.processor = processor;
            this.instance = instance;
            this.equation = equation;
            ease = equation != null;

            sr.sortingOrder = instance.OrderInLayer;
            sr.sprite = instance.GetSpriteRenderer().sprite;
            sr.color = instance.TrailColour.Evaluate(0);

            Vector2 lossyScale = instance.transform.lossyScale;
            Vector2 size = instance.StartSizeType == SizingType.FOLLOW_REAL_SIZE ? lossyScale * instance.StartMultiplier : instance.StartSize;
            sr.transform.localScale = start = size;

            size = instance.EndSizeType == SizingType.FOLLOW_REAL_SIZE ? lossyScale * instance.EndMultiplier : instance.EndSize;            
            end = size;

            transform.position = spawnPosition;

            elapsedTime = 0;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            elapsedTime = Mathf.Min(elapsedTime + Time.deltaTime, instance.Lifespan);

            float progress = elapsedTime / instance.Lifespan;
            float t = ease ? equation.Invoke(progress, instance.Power) : progress;
            sr.color = instance.TrailColour.Evaluate(Mathf.Lerp(0, 1, instance.AffectedByEasing ? t : progress));
            transform.localScale = Vector2.Lerp(start, end, t);

            if (elapsedTime >= instance.Lifespan)
            {
                processor.DisableTrail();
            }
        }
    }
}