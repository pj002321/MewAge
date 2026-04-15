using System;
using System.Collections.Generic;

namespace TheBlackCat.TrailEffect2D
{
    public class EasingManager
    {
        private static Dictionary<EaseType, Func<float, float, float>> easeEquations = new Dictionary<EaseType, Func<float, float, float>>
        {
            { EaseType.SINE_EASE_IN, (t, a) => Equations.SineEaseIn(t, a) },
            { EaseType.SINE_EASE_OUT, (t, a) => Equations.SineEaseOut(t, a) },
            { EaseType.SINE_EASE_IN_OUT, (t, a) => Equations.SineEaseBoth(t, a) },
            { EaseType.EXPONENTIAL_EASE_IN, (t, a) => Equations.ExponentialEaseIn(t, a) },
            { EaseType.EXPONENTIAL_EASE_OUT, (t, a) => Equations.ExponentialEaseOut(t, a) },
            { EaseType.EXPONENTIAL_EASE_IN_OUT, (t, a) => Equations.ExponentialEaseBoth(t, a) },
            { EaseType.CIRC_EASE_IN, (t, a) => Equations.CircEaseIn(t, a) },
            { EaseType.CIRC_EASE_OUT, (t, a) => Equations.CircEaseOut(t, a) },
            { EaseType.CIRC_EASE_IN_OUT, (t, a) => Equations.CircEaseBoth(t, a) },
            { EaseType.ELASTIC_EASE_IN, (t, a) => Equations.ElasticEaseIn(t, a) },
            { EaseType.ELASTIC_EASE_OUT, (t, a) => Equations.ElasticEaseOut(t, a) },
            { EaseType.ELASTIC_EASE_IN_OUT, (t, a) => Equations.ElasticEaseBoth(t, a) }
        };

        public static Func<float, float, float> GetEquation(EaseType type)
        {
            return easeEquations.TryGetValue(type, out var eq) ? eq : null;
        }
    }

}