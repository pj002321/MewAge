namespace TheBlackCat.TrailEffect2D
{
    public enum SizingType
    {
        FOLLOW_REAL_SIZE,
        FIXED_SIZE
    }

    public enum TrailSpawningCondition
    {
        TIME,
        DISTANCE_SINCE_PREV,
        DISTANCE_MOVED
    }

    public enum EaseType
    {
        NONE,
        EXPONENTIAL_EASE_OUT,
        EXPONENTIAL_EASE_IN,
        EXPONENTIAL_EASE_IN_OUT,
        SINE_EASE_OUT,
        SINE_EASE_IN,
        SINE_EASE_IN_OUT,
        CIRC_EASE_OUT,
        CIRC_EASE_IN,
        CIRC_EASE_IN_OUT,
        ELASTIC_EASE_OUT,
        ELASTIC_EASE_IN,
        ELASTIC_EASE_IN_OUT
    }
}