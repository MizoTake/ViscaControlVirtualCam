using UnityEngine;

namespace ViscaControlVirtualCam
{
    [CreateAssetMenu(fileName = "PTZ_Settings", menuName = "Visca/PTZ Settings", order = 10)]
    public class PtzSettings : ScriptableObject
    {
        [Header("可動制限")]
        [Tooltip("パンの最大速度(度/秒)")]
        public float panMaxDegPerSec = 120f;

        [Tooltip("チルトの最大速度(度/秒)")]
        public float tiltMaxDegPerSec = 90f;

        [Tooltip("ズーム(FOV)の最大速度(度/秒)")]
        public float zoomMaxFovPerSec = 40f;

        [Tooltip("FOVの最小値(望遠側)")]
        public float minFov = 15f;

        [Tooltip("FOVの最大値(広角側)")]
        public float maxFov = 90f;

        [Header("絶対位置の可動範囲")]
        [Tooltip("パン最小角度(度)")]
        public float panMinDeg = -170f;

        [Tooltip("パン最大角度(度)")]
        public float panMaxDeg = 170f;

        [Tooltip("チルト最小角度(度)")]
        public float tiltMinDeg = -30f;

        [Tooltip("チルト最大角度(度)")]
        public float tiltMaxDeg = 90f;

        [Tooltip("絶対位置移動時の減衰値(大きいほど速く追従)")]
        public float moveDamping = 6f;

        [Header("速度カーブ")]
        [Tooltip("VISCA速度値のカーブ調整(1=線形)")]
        [Range(0.1f, 3f)]
        public float speedGamma = 1.0f;

        [Tooltip("パン速度のVISCA最小/最大値")]
        public byte panVmin = 0x01, panVmax = 0x18;

        [Tooltip("チルト速度のVISCA最小/最大値")]
        public byte tiltVmin = 0x01, tiltVmax = 0x14;

        [Header("制御オプション")]
        [Tooltip("左右の操作を反転する")]
        public bool invertPan;

        [Tooltip("上下の操作を反転する")]
        public bool invertTilt;

        [Tooltip("パンの絶対位置マッピングを反転する")]
        public bool invertPanAbsolute;

        [Tooltip("チルトの絶対位置マッピングを反転する")]
        public bool invertTiltAbsolute;

        [Header("ズーム連動")]
        [Tooltip("望遠時にパン/チルト速度を抑える")]
        public bool enablePanTiltSpeedScaleByZoom = false;

        [Tooltip("望遠端(最小FOV)でのパン/チルト速度倍率")]
        [Range(0.1f, 1.0f)]
        public float panTiltSpeedScaleAtTele = 0.6f;

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
            model.EnablePanTiltSpeedScaleByZoom = enablePanTiltSpeedScaleByZoom;
            model.PanTiltSpeedScaleAtTele = panTiltSpeedScaleAtTele;
        }
    }
}
