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

        [Header("ズーム速度モード")]
        [Tooltip("ズーム速度をズーム位置(正規化)で制御する")]
        public bool useZoomPositionSpeed = false;

        [Tooltip("ズーム位置の最大速度(正規化/秒) (0で自動換算)")]
        public float zoomMaxNormalizedPerSec = 0f;

        [Header("速度下限/プリセット速度")]
        [Tooltip("パンの最小速度(度/秒)")]
        public float panMinDegPerSec = 0f;

        [Tooltip("チルトの最小速度(度/秒)")]
        public float tiltMinDegPerSec = 0f;

        [Tooltip("プリセット呼出し時のパン最大速度(度/秒)")]
        public float panPresetMaxDegPerSec = 0f;

        [Tooltip("プリセット呼出し時のチルト最大速度(度/秒)")]
        public float tiltPresetMaxDegPerSec = 0f;

        [Tooltip("プリセット呼出し時のパン最小速度(度/秒)")]
        public float panPresetMinDegPerSec = 0f;

        [Tooltip("プリセット呼出し時のチルト最小速度(度/秒)")]
        public float tiltPresetMinDegPerSec = 0f;

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

        [Header("スローパン/チルト")]
        [Tooltip("スローパン/チルトモードを使用する")]
        public bool useSlowPanTilt = false;

        [Tooltip("スローパン時のパン最大速度(度/秒)")]
        public float panSlowMaxDegPerSec = 60f;

        [Tooltip("スローパン時のチルト最大速度(度/秒)")]
        public float tiltSlowMaxDegPerSec = 60f;

        [Tooltip("スローパン時のパン最小速度(度/秒)")]
        public float panSlowMinDegPerSec = 0f;

        [Tooltip("スローパン時のチルト最小速度(度/秒)")]
        public float tiltSlowMinDegPerSec = 0f;

        [Header("ズーム連動")]
        [Tooltip("望遠時にパン/チルト速度を抑える")]
        public bool enablePanTiltSpeedScaleByZoom = false;

        [Tooltip("望遠端(最小FOV)でのパン/チルト速度倍率")]
        [Range(0.1f, 1.0f)]
        public float panTiltSpeedScaleAtTele = 0.6f;

        [Header("レンズプロファイル")]
        [Tooltip("ズーム位置からFOVをレンズ仕様で算出する")]
        public bool useLensProfile = false;

        [Tooltip("センサー幅(mm)")]
        public float sensorWidthMm = 0f;

        [Tooltip("センサー高(mm)")]
        public float sensorHeightMm = 0f;

        [Tooltip("焦点距離最小(mm)")]
        public float focalLengthMinMm = 0f;

        [Tooltip("焦点距離最大(mm)")]
        public float focalLengthMaxMm = 0f;

        [Tooltip("ズーム位置の最大値を望遠側として扱う")]
        public bool zoomPositionTeleAtMax = true;

        public void ApplyTo(PtzModel model)
        {
            if (model == null) return;

            model.PanMaxDegPerSec = panMaxDegPerSec;
            model.TiltMaxDegPerSec = tiltMaxDegPerSec;
            model.ZoomMaxFovPerSec = zoomMaxFovPerSec;
            model.UseZoomPositionSpeed = useZoomPositionSpeed;
            model.ZoomMaxNormalizedPerSec = zoomMaxNormalizedPerSec;
            model.PanMinDegPerSec = panMinDegPerSec;
            model.TiltMinDegPerSec = tiltMinDegPerSec;
            model.PanPresetMaxDegPerSec = panPresetMaxDegPerSec;
            model.TiltPresetMaxDegPerSec = tiltPresetMaxDegPerSec;
            model.PanPresetMinDegPerSec = panPresetMinDegPerSec;
            model.TiltPresetMinDegPerSec = tiltPresetMinDegPerSec;
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
            model.UseSlowPanTilt = useSlowPanTilt;
            model.PanSlowMaxDegPerSec = panSlowMaxDegPerSec;
            model.TiltSlowMaxDegPerSec = tiltSlowMaxDegPerSec;
            model.PanSlowMinDegPerSec = panSlowMinDegPerSec;
            model.TiltSlowMinDegPerSec = tiltSlowMinDegPerSec;
            model.EnablePanTiltSpeedScaleByZoom = enablePanTiltSpeedScaleByZoom;
            model.PanTiltSpeedScaleAtTele = panTiltSpeedScaleAtTele;
            model.UseLensProfile = useLensProfile;
            model.SensorWidthMm = sensorWidthMm;
            model.SensorHeightMm = sensorHeightMm;
            model.FocalLengthMinMm = focalLengthMinMm;
            model.FocalLengthMaxMm = focalLengthMaxMm;
            model.ZoomPositionTeleAtMax = zoomPositionTeleAtMax;
        }
    }
}
