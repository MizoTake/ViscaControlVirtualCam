using UnityEngine;

namespace ViscaControlVirtualCam
{
    /// <summary>
    /// Optional tuning profile to approximate physical PTZ behavior.
    /// </summary>
    [CreateAssetMenu(fileName = "PTZ_Tuning", menuName = "Visca/PTZ Tuning Profile", order = 11)]
    public class PtzTuningProfile : ScriptableObject
    {
        [Header("Acceleration Limits (deg/s^2)")]
        [Tooltip("Enable acceleration limiting for pan/tilt/zoom variable drive")]
        public bool enableAccelerationLimit = true;

        [Min(0f)] public float panAccelDegPerSec2 = 600f;
        [Min(0f)] public float tiltAccelDegPerSec2 = 600f;
        [Min(0f)] public float zoomAccelDegPerSec2 = 300f;

        public void ApplyTo(PtzModel model)
        {
            if (model == null) return;

            model.UseAccelerationLimit = enableAccelerationLimit;
            model.PanAccelDegPerSec2 = panAccelDegPerSec2;
            model.TiltAccelDegPerSec2 = tiltAccelDegPerSec2;
            model.ZoomAccelDegPerSec2 = zoomAccelDegPerSec2;
        }
    }
}
