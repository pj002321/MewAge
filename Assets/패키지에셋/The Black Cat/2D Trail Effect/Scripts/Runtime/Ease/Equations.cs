using UnityEngine;

namespace TheBlackCat.TrailEffect2D
{
    public class Equations
    {
        public static float SineEaseIn(float progress, float amplitude)
        {
            return 1 - Mathf.Cos(progress * Mathf.PI / 2);
        }

        public static float SineEaseOut(float progress, float amplitude)
        {
            return Mathf.Sin(progress * Mathf.PI / 2);
        }

        public static float SineEaseBoth(float progress, float amplitude)
        {
            return -(Mathf.Cos(Mathf.PI * progress) - 1) / 2;
        }

        public static float ExponentialEaseIn(float progress, float amplitude)
        {
            return Mathf.Pow(progress, amplitude);
        }

        public static float ExponentialEaseOut(float progress, float amplitude)
        {
            return 1 - Mathf.Pow(1 - progress, amplitude);
        }

        public static float ExponentialEaseBoth(float progress, float amplitude)
        {
            return progress < 0.5f ? Mathf.Pow(2, amplitude - 1) * Mathf.Pow(progress, amplitude) : 1 - Mathf.Pow(-2 * progress + 2, amplitude) / 2;
        }

        public static float CircEaseIn(float progress, float amplitude)
        {            
            return 1 - Mathf.Sqrt(1 - Mathf.Pow(progress, 2));
        }

        public static float CircEaseOut(float progress, float amplitude)
        {
            return Mathf.Sqrt(1 - Mathf.Pow(progress - 1, 2));
        }

        public static float CircEaseBoth(float progress, float amplitude)
        {
            return progress < 0.5f ? (1 - Mathf.Sqrt(1 - Mathf.Pow(2 * progress, 2))) / 2 : (Mathf.Sqrt(1 - Mathf.Pow(-2 * progress + 2, 2)) + 1) / 2;
        }

        public static float ElasticEaseIn(float progress, float amplitude)
        {
            return progress > 0 && progress < 1 ? -Mathf.Pow(2, 10 * progress - 10) * Mathf.Sin((progress * 10 - 10.75f) * (2 * Mathf.PI / 3)) : progress;
        }

        public static float ElasticEaseOut(float progress, float amplitude)
        {
            return progress > 0 && progress < 1 ? Mathf.Pow(2, -10 * progress) * Mathf.Sin((progress * 10 - 0.75f) * (2 * Mathf.PI / 3)) + 1 : progress;
        }

        public static float ElasticEaseBoth(float progress, float amplitude)
        {
            return progress == 0 || progress == 1 ? progress : progress < 0.5f ? -(Mathf.Pow(2, 20 * progress - 10) * Mathf.Sin((20 * progress - 11.125f) * (2 * Mathf.PI / 4.5f))) / 2 : (Mathf.Pow(2, -20 * progress + 10) * Mathf.Sin((20 * progress - 11.125f) * (2 * Mathf.PI / 4.5f))) / 2 + 1;
        }
    }

}