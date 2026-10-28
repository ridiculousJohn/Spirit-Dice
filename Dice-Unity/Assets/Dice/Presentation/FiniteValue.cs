using UnityEngine;

namespace Dice.Presentation
{
    /** Finite-number checks shared by the validation of authored presentation values. */
    public static class FiniteValue
    {
        public static bool Is(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool Is(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public static bool Is(Vector2 value) => Is(value.x) && Is(value.y);
        public static bool Is(Vector3 value) => Is(value.x) && Is(value.y) && Is(value.z);
        public static bool Is(Color value) => Is(value.r) && Is(value.g) && Is(value.b) && Is(value.a);
    }
}
