using System;

namespace ViscaControlVirtualCam
{
    /// <summary>
    ///     Static utility class for PTZ motion calculations.
    ///     Provides reusable math functions for interpolation, clamping, and angle operations.
    /// </summary>
    public static class PtzMathUtils
    {
        /// <summary>
        ///     Linear interpolation between two values.
        /// </summary>
        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
        }

        /// <summary>
        ///     Inverse lerp: returns normalized position of v between a and b.
        ///     Returns 0.5 if a == b to avoid division by zero.
        /// </summary>
        public static float SafeInverseLerp(float a, float b, float v)
        {
            var range = b - a;
            if (Math.Abs(range) < ViscaProtocol.DivisionEpsilon) return 0.5f;
            return (v - a) / range;
        }

        /// <summary>
        ///     Clamp a float value between min and max.
        /// </summary>
        public static float Clamp(float v, float min, float max)
        {
            return v < min ? min : v > max ? max : v;
        }

        /// <summary>
        ///     Clamp an integer value between min and max.
        /// </summary>
        public static int Clamp(int v, int min, int max)
        {
            return v < min ? min : v > max ? max : v;
        }

        /// <summary>
        ///     Exponential damping for smooth motion towards a target.
        /// </summary>
        /// <param name="current">Current value</param>
        /// <param name="target">Target value</param>
        /// <param name="damping">Damping factor (higher = faster)</param>
        /// <param name="dt">Delta time</param>
        /// <returns>New value after damping</returns>
        public static float Damp(float current, float target, float damping, float dt)
        {
            var k = 1f - (float)Math.Exp(-damping * dt);
            return current + (target - current) * k;
        }

        /// <summary>
        ///     Calculate the shortest signed angle difference between two angles.
        ///     Handles wrap-around at 360 degrees.
        /// </summary>
        public static float DeltaAngle(float a, float b)
        {
            var diff = (b - a) % 360f;
            if (diff > 180f) diff -= 360f;
            if (diff < -180f) diff += 360f;
            return diff;
        }

        /// <summary>
        ///     Move current value towards target by at most maxDelta.
        ///     Used for acceleration limiting.
        /// </summary>
        public static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta) return target;
            return current + Math.Sign(target - current) * maxDelta;
        }

        /// <summary>
        ///     Map a VISCA speed byte to degrees per second using gamma curve.
        /// </summary>
        /// <param name="v">Speed byte from VISCA command</param>
        /// <param name="vmin">Minimum valid speed byte</param>
        /// <param name="vmax">Maximum valid speed byte</param>
        /// <param name="maxDegPerSec">Maximum speed in degrees per second</param>
        /// <param name="gamma">Gamma curve exponent (1.0 = linear)</param>
        /// <returns>Speed in degrees per second</returns>
        public static float MapSpeed(byte v, byte vmin, byte vmax, float maxDegPerSec, float gamma)
        {
            if (v == 0x00) v = vmin;
            var t = SafeInverseLerp(vmin, vmax, Clamp(v, vmin, vmax));
            var mapped = (float)Math.Pow(t, Math.Max(0.01f, gamma));
            return mapped * maxDegPerSec;
        }
    }
}
