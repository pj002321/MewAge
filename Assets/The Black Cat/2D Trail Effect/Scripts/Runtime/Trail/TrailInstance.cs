using UnityEngine;

namespace TheBlackCat.TrailEffect2D
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class TrailInstance : MonoBehaviour
    {
        public int OrderInLayer;
        
        [Tooltip("The colour of the trail object over time.")]
        [GradientUsage(true)]
        public Gradient TrailColour;
        [Tooltip("Whether the colour over time will be affected by the easing equation.")]
        public bool AffectedByEasing;

        [Tooltip("The size type of the starting size.\n\n" +
            "- FOLLOW_REAL_SIZE: The scale will be set to be the world scale of the game object that is spawning the trail.\n\n" +
            "- FIXED_SIZE: The scale will be set to the fixed scale you provided.")]
        public SizingType StartSizeType;
        [Tooltip("The fixed scale.")]
        public Vector2 StartSize;
        [Tooltip("The scale multiplier of the starting size.")]
        public Vector2 StartMultiplier = Vector2.one;

        [Tooltip("The size type of the ending size.\n\n" +
            "- FOLLOW_REAL_SIZE: The scale will be set to the world scale of the game object spawning the trail.\n\n" +
            "- FIXED_SIZE: The scale will be set to the fixed scale you provided.")]
        public SizingType EndSizeType;
        [Tooltip("The fixed scale.")]
        public Vector2 EndSize;
        [Tooltip("The scale multiplier of the ending size.")]
        public Vector2 EndMultiplier = Vector2.one;

        [Tooltip("The type of easing of colour and scale changes.")]
        public EaseType EaseType;
        [Range(2, 5)] public float Power = 2;

        [Tooltip("The amount of time a trail object will live in seconds.")]
        public float Lifespan;
        [Tooltip("Whether to spawn a trail object immediately after the trail starts.")]
        public bool SpawnTrailOnStart;
        [Tooltip("Whether to disable all trail objects of the game object immediately when the trail is stopped.")]
        public bool DisableAllTrailOnStop;
        
        public TrailSpawningCondition SpawnCondition;
        public float DistanceBetweenSpawn;
        public float TimeBetweenSpawn;

        private SpriteRenderer sr;

        private void OnDisable()
        {
            TrailManager.Instance?.StopTrail(gameObject);
        }

        public SpriteRenderer GetSpriteRenderer()
        {
            if (sr == null)
            {
                sr = GetComponent<SpriteRenderer>();
            }
            return sr;
        }
    }
}