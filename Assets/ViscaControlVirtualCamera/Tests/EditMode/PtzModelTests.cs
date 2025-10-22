using NUnit.Framework;
using ViscaControlVirtualCam;

public class PtzModelTests
{
    [Test]
    public void PanTiltDrive_RightUp_ProducesPositiveYawPitch()
    {
        var m = new PtzModel
        {
            PanMaxDegPerSec = 100f,
            TiltMaxDegPerSec = 80f,
            SpeedGamma = 1.0f,
            PanVmin = 0x01, PanVmax = 0x18,
            TiltVmin = 0x01, TiltVmax = 0x14
        };
        m.CommandPanTiltVariable(0x10, 0x10, AxisDirection.Positive, AxisDirection.Positive);
        var step = m.Step(0f, 0f, 60f, 0.1f);
        Assert.Greater(step.DeltaYawDeg, 0f, "Yaw should increase to the right");
        Assert.Greater(step.DeltaPitchDeg, 0f, "Pitch should increase for up");
    }

    [Test]
    public void Zoom_Tele_DecreasesFov()
    {
        var m = new PtzModel { ZoomMaxFovPerSec = 40f, MinFov = 15f, MaxFov = 90f, SpeedGamma = 1.0f };
        // ZZ = 0x2p (Tele), p=7 max
        m.CommandZoomVariable(0x27);
        var step = m.Step(0f, 0f, 60f, 0.1f);
        Assert.IsTrue(step.HasNewFov);
        Assert.Less(step.NewFovDeg, 60f);
    }

    [Test]
    public void Absolute_Move_ReachesTarget()
    {
        var m = new PtzModel { PanMinDeg = -170, PanMaxDeg = 170, TiltMinDeg = -30, TiltMaxDeg = 90, MoveDamping = 100f };
        m.CommandPanTiltAbsolute(0x10, 0x10, 0x8000, 0x8000); // center
        var yaw = -45f; var pitch = -10f; var fov = 60f;
        for (int i = 0; i < 20; i++)
        {
            var s = m.Step(yaw, pitch, fov, 0.05f);
            yaw += s.DeltaYawDeg;
            pitch += s.DeltaPitchDeg;
        }
        Assert.That(yaw, Is.InRange(-1.0f, 1.0f));
        Assert.That(pitch, Is.InRange(-1.0f, 1.0f));
    }
}

