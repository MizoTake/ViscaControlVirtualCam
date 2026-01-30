using UnityEngine;

namespace ViscaControlVirtualCam
{
    /// <summary>
    ///     Optional tuning profile to approximate physical PTZ behavior.
    /// </summary>
    [CreateAssetMenu(fileName = "PTZ_Tuning", menuName = "Visca/PTZ Tuning Profile", order = 11)]
    public class PtzTuningProfile : ScriptableObject
    {
        [Header("加減速制限 (度/秒^2)")]
        [Tooltip("パン/チルト/ズームの加減速制限を有効化")]
        public bool enableAccelerationLimit = true;

        [Tooltip("パンの最大加速度")]
        [Min(0f)]
        public float panAccelDegPerSec2 = 600f;

        [Tooltip("チルトの最大加速度")]
        [Min(0f)]
        public float tiltAccelDegPerSec2 = 600f;

        [Tooltip("ズームの最大加速度")]
        [Min(0f)]
        public float zoomAccelDegPerSec2 = 300f;

        [Tooltip("パンの最大減速度")]
        [Min(0f)]
        public float panDecelDegPerSec2 = 600f;

        [Tooltip("チルトの最大減速度")]
        [Min(0f)]
        public float tiltDecelDegPerSec2 = 600f;

        [Tooltip("ズームの最大減速度")]
        [Min(0f)]
        public float zoomDecelDegPerSec2 = 300f;

        [Header("目標ブレーキ(絶対位置)")]
        [Tooltip("絶対位置移動をブレーキ方式で制御する")]
        public bool enableTargetBraking = false;

        [Tooltip("パン: 目標に到達したとみなす距離(度)")]
        [Min(0f)]
        public float panStopDistanceDeg = 0.1f;

        [Tooltip("チルト: 目標に到達したとみなす距離(度)")]
        [Min(0f)]
        public float tiltStopDistanceDeg = 0.1f;

        [Tooltip("ズーム(FOV): 目標に到達したとみなす距離(度)")]
        [Min(0f)]
        public float zoomStopDistanceDeg = 0.1f;

        public void ApplyTo(PtzModel model)
        {
            if (model == null) return;

            model.UseAccelerationLimit = enableAccelerationLimit;
            model.PanAccelDegPerSec2 = panAccelDegPerSec2;
            model.TiltAccelDegPerSec2 = tiltAccelDegPerSec2;
            model.ZoomAccelDegPerSec2 = zoomAccelDegPerSec2;
            model.PanDecelDegPerSec2 = panDecelDegPerSec2;
            model.TiltDecelDegPerSec2 = tiltDecelDegPerSec2;
            model.ZoomDecelDegPerSec2 = zoomDecelDegPerSec2;
            model.UseTargetBraking = enableTargetBraking;
            model.PanStopDistanceDeg = panStopDistanceDeg;
            model.TiltStopDistanceDeg = tiltStopDistanceDeg;
            model.ZoomStopDistanceDeg = zoomStopDistanceDeg;
        }
    }
}
