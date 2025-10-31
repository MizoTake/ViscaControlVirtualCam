using NUnit.Framework;
using ViscaControlVirtualCam;
using System.Collections.Generic;

/// <summary>
/// Mock PlayerPrefs adapter for testing
/// </summary>
public class MockPlayerPrefsAdapter : IPlayerPrefsAdapter
{
    private readonly Dictionary<string, float> _data = new Dictionary<string, float>();

    public void SetFloat(string key, float value) => _data[key] = value;
    public float GetFloat(string key, float defaultValue) => _data.TryGetValue(key, out var v) ? v : defaultValue;
    public bool HasKey(string key) => _data.ContainsKey(key);
    public void DeleteKey(string key) => _data.Remove(key);
    public void Save() { }
}

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
        m.CommandPanTiltAbsolute(0x10, 0x10, 0x8000, 0x8000); // center: pan=0, tilt=30
        var yaw = -45f; var pitch = -10f; var fov = 60f;
        for (int i = 0; i < 20; i++)
        {
            var s = m.Step(yaw, pitch, fov, 0.05f);
            yaw += s.DeltaYawDeg;
            pitch += s.DeltaPitchDeg;
        }
        // Target: pan=0deg (center of -170 to 170), tilt=30deg (center of -30 to 90)
        Assert.That(yaw, Is.InRange(-1.0f, 1.0f), "Pan should reach 0 degrees");
        Assert.That(pitch, Is.InRange(29.0f, 31.0f), "Tilt should reach 30 degrees");
    }

    [Test]
    public void MemorySet_SavesPreset()
    {
        var prefs = new MockPlayerPrefsAdapter();
        var m = new PtzModel(prefs, "Test_");

        // Set current state
        m.Step(10f, 20f, 45f, 0.01f); // Update current state

        // Save to memory 1
        m.CommandMemorySet(1);

        // Verify preset was saved to PlayerPrefs
        Assert.IsTrue(prefs.HasKey("Test_Mem1_Pan"));
        Assert.AreEqual(10f, prefs.GetFloat("Test_Mem1_Pan", 0f), 0.01f);
        Assert.AreEqual(20f, prefs.GetFloat("Test_Mem1_Tilt", 0f), 0.01f);
        Assert.AreEqual(45f, prefs.GetFloat("Test_Mem1_Fov", 0f), 0.01f);
    }

    [Test]
    public void MemoryRecall_LoadsPreset()
    {
        var prefs = new MockPlayerPrefsAdapter();
        var m = new PtzModel(prefs, "Test_");

        // Set current state and save
        m.Step(10f, 20f, 45f, 0.01f);
        m.CommandMemorySet(1);

        // Change state
        m.Step(50f, 60f, 70f, 0.01f);

        // Recall memory 1
        m.CommandMemoryRecall(1);

        // Step to reach target
        float yaw = 50f, pitch = 60f, fov = 70f;
        for (int i = 0; i < 50; i++)
        {
            var s = m.Step(yaw, pitch, fov, 0.05f);
            yaw += s.DeltaYawDeg;
            pitch += s.DeltaPitchDeg;
            if (s.HasNewFov) fov = s.NewFovDeg;
        }

        // Should reach recalled preset
        Assert.That(yaw, Is.InRange(9f, 11f), "Pan should reach preset value");
        Assert.That(pitch, Is.InRange(19f, 21f), "Tilt should reach preset value");
        Assert.That(fov, Is.InRange(44f, 46f), "FOV should reach preset value");
    }

    [Test]
    public void MemoryPreset_PersistsAcrossInstances()
    {
        var prefs = new MockPlayerPrefsAdapter();

        // First instance: save preset
        var m1 = new PtzModel(prefs, "Test_");
        m1.Step(15f, 25f, 50f, 0.01f);
        m1.CommandMemorySet(2);

        // Second instance: should load preset
        var m2 = new PtzModel(prefs, "Test_");
        m2.CommandMemoryRecall(2);

        // Verify preset was loaded
        float yaw = 0f, pitch = 0f, fov = 60f;
        for (int i = 0; i < 50; i++)
        {
            var s = m2.Step(yaw, pitch, fov, 0.05f);
            yaw += s.DeltaYawDeg;
            pitch += s.DeltaPitchDeg;
            if (s.HasNewFov) fov = s.NewFovDeg;
        }

        Assert.That(yaw, Is.InRange(14f, 16f), "Pan should load from persisted preset");
        Assert.That(pitch, Is.InRange(24f, 26f), "Tilt should load from persisted preset");
        Assert.That(fov, Is.InRange(49f, 51f), "FOV should load from persisted preset");
    }

    [Test]
    public void DeletePreset_RemovesFromMemoryAndPlayerPrefs()
    {
        var prefs = new MockPlayerPrefsAdapter();
        var m = new PtzModel(prefs, "Test_");

        // Save preset
        m.Step(10f, 20f, 45f, 0.01f);
        m.CommandMemorySet(3);

        // Verify saved
        Assert.IsTrue(prefs.HasKey("Test_Mem3_Pan"));

        // Delete preset
        m.DeletePreset(3);

        // Verify removed
        Assert.IsFalse(prefs.HasKey("Test_Mem3_Pan"));
        Assert.IsFalse(prefs.HasKey("Test_Mem3_Tilt"));
        Assert.IsFalse(prefs.HasKey("Test_Mem3_Fov"));
    }
}
