using UnityEngine;

namespace ViscaControlVirtualCam
{
    [CreateAssetMenu(fileName = "PTZ_Settings", menuName = "Visca/PTZ Settings", order = 10)]
    public class PtzSettings : ScriptableObject
    {
        [Header("Motion Limits")] public float panMaxDegPerSec = 120f;
        public float tiltMaxDegPerSec = 90f;
        public float zoomMaxFovPerSec = 40f;
        public float minFov = 15f;
        public float maxFov = 90f;

        [Header("Absolute Ranges")] public float panMinDeg = -170f;
        public float panMaxDeg = 170f;
        public float tiltMinDeg = -30f;
        public float tiltMaxDeg = 90f;
        public float moveDamping = 6f;

        [Header("Speed Mapping")] [Range(0.1f, 3f)] public float speedGamma = 1.0f;
        [Tooltip("Pan speed min/max VISCA values")] public byte panVmin = 0x01, panVmax = 0x18;
        [Tooltip("Tilt speed min/max VISCA values")] public byte tiltVmin = 0x01, tiltVmax = 0x14;

        [Header("Control Options")]
        [Tooltip("Invert horizontal control (Left/Right)")]
        public bool invertPan;

        [Tooltip("Invert vertical control (Up/Down)")]
        public bool invertTilt;

        [Tooltip("Invert Pan absolute position mapping (Lerp is flipped)")]
        public bool invertPanAbsolute;

        [Tooltip("Invert Tilt absolute position mapping (Lerp is flipped)")]
        public bool invertTiltAbsolute;

        public void ApplyTo(PtzModel model)
        {
            if (model == null) return;

            model.PanMaxDegPerSec = panMaxDegPerSec;
            model.TiltMaxDegPerSec = tiltMaxDegPerSec;
            model.ZoomMaxFovPerSec = zoomMaxFovPerSec;
            model.MinFov = minFov;
            model.MaxFov = maxFov;
            model.PanMinDeg = panMinDeg;
            model.PanMaxDeg = panMaxDeg;
            model.TiltMinDeg = tiltMinDeg;
            model.TiltMaxDeg = tiltMaxDeg;
            model.MoveDamping = moveDamping;
            model.SpeedGamma = speedGamma;
            model.PanVmin = panVmin;
            model.PanVmax = panVmax;
            model.TiltVmin = tiltVmin;
            model.TiltVmax = tiltVmax;
            model.InvertPan = invertPan;
            model.InvertTilt = invertTilt;
            model.InvertPanAbsolute = invertPanAbsolute;
            model.InvertTiltAbsolute = invertTiltAbsolute;
        }
    }
}
