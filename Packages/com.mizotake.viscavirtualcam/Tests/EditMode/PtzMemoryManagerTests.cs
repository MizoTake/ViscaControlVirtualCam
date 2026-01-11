using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ViscaControlVirtualCam;

public class PtzMemoryManagerTests
{
    #region Save and Recall Tests

    [Test]
    public void SavePreset_ThenRecall_ReturnsCorrectValues()
    {
        var manager = new PtzMemoryManager();

        manager.SavePreset(0, 10f, 20f, 60f, 1000f, 500f);

        Assert.IsTrue(manager.TryGetPreset(0, out var preset));
        Assert.AreEqual(10f, preset.PanDeg, 0.0001f);
        Assert.AreEqual(20f, preset.TiltDeg, 0.0001f);
        Assert.AreEqual(60f, preset.FovDeg, 0.0001f);
        Assert.AreEqual(1000f, preset.FocusPos, 0.0001f);
        Assert.AreEqual(500f, preset.IrisPos, 0.0001f);
    }

    [Test]
    public void TryGetPreset_NonExistent_ReturnsFalse()
    {
        var manager = new PtzMemoryManager();

        Assert.IsFalse(manager.TryGetPreset(99, out _));
    }

    [Test]
    public void SavePreset_Overwrite_ReturnsNewValues()
    {
        var manager = new PtzMemoryManager();

        manager.SavePreset(0, 10f, 20f, 60f, 1000f, 500f);
        manager.SavePreset(0, 50f, 60f, 90f, 2000f, 1000f);

        Assert.IsTrue(manager.TryGetPreset(0, out var preset));
        Assert.AreEqual(50f, preset.PanDeg, 0.0001f);
        Assert.AreEqual(60f, preset.TiltDeg, 0.0001f);
    }

    [Test]
    public void SavePreset_MultipleSlots_AllRetrievable()
    {
        var manager = new PtzMemoryManager();

        manager.SavePreset(0, 0f, 0f, 60f, 0f, 0f);
        manager.SavePreset(1, 10f, 10f, 70f, 100f, 100f);
        manager.SavePreset(5, 50f, 50f, 80f, 500f, 500f);

        Assert.IsTrue(manager.TryGetPreset(0, out var p0));
        Assert.IsTrue(manager.TryGetPreset(1, out var p1));
        Assert.IsTrue(manager.TryGetPreset(5, out var p5));

        Assert.AreEqual(0f, p0.PanDeg, 0.0001f);
        Assert.AreEqual(10f, p1.PanDeg, 0.0001f);
        Assert.AreEqual(50f, p5.PanDeg, 0.0001f);
    }

    #endregion

    #region Delete Tests

    [Test]
    public void DeletePreset_RemovesFromMemory()
    {
        var manager = new PtzMemoryManager();

        manager.SavePreset(0, 10f, 20f, 60f, 1000f, 500f);
        Assert.IsTrue(manager.HasPreset(0));

        manager.DeletePreset(0);
        Assert.IsFalse(manager.HasPreset(0));
    }

    [Test]
    public void DeletePreset_NonExistent_NoException()
    {
        var manager = new PtzMemoryManager();

        Assert.DoesNotThrow(() => manager.DeletePreset(99));
    }

    #endregion

    #region HasPreset Tests

    [Test]
    public void HasPreset_Exists_ReturnsTrue()
    {
        var manager = new PtzMemoryManager();

        manager.SavePreset(5, 0f, 0f, 60f, 0f, 0f);

        Assert.IsTrue(manager.HasPreset(5));
    }

    [Test]
    public void HasPreset_NotExists_ReturnsFalse()
    {
        var manager = new PtzMemoryManager();

        Assert.IsFalse(manager.HasPreset(5));
    }

    #endregion

    #region GetSavedPresets Tests

    [Test]
    public void GetSavedPresets_Empty_ReturnsEmpty()
    {
        var manager = new PtzMemoryManager();

        var presets = manager.GetSavedPresets().ToList();

        Assert.AreEqual(0, presets.Count);
    }

    [Test]
    public void GetSavedPresets_AfterSave_ReturnsCorrectNumbers()
    {
        var manager = new PtzMemoryManager();

        manager.SavePreset(0, 0f, 0f, 60f, 0f, 0f);
        manager.SavePreset(3, 0f, 0f, 60f, 0f, 0f);
        manager.SavePreset(7, 0f, 0f, 60f, 0f, 0f);

        var presets = manager.GetSavedPresets().OrderBy(x => x).ToList();

        Assert.AreEqual(3, presets.Count);
        CollectionAssert.AreEqual(new byte[] { 0, 3, 7 }, presets);
    }

    [Test]
    public void GetSavedPresets_AfterDelete_ExcludesDeleted()
    {
        var manager = new PtzMemoryManager();

        manager.SavePreset(0, 0f, 0f, 60f, 0f, 0f);
        manager.SavePreset(1, 0f, 0f, 60f, 0f, 0f);
        manager.SavePreset(2, 0f, 0f, 60f, 0f, 0f);
        manager.DeletePreset(1);

        var presets = manager.GetSavedPresets().OrderBy(x => x).ToList();

        Assert.AreEqual(2, presets.Count);
        CollectionAssert.AreEqual(new byte[] { 0, 2 }, presets);
    }

    #endregion

    #region ClearAll Tests

    [Test]
    public void ClearAll_RemovesAllPresets()
    {
        var manager = new PtzMemoryManager();

        manager.SavePreset(0, 0f, 0f, 60f, 0f, 0f);
        manager.SavePreset(1, 0f, 0f, 60f, 0f, 0f);
        manager.SavePreset(2, 0f, 0f, 60f, 0f, 0f);

        manager.ClearAll();

        Assert.AreEqual(0, manager.GetSavedPresets().Count());
        Assert.IsFalse(manager.HasPreset(0));
        Assert.IsFalse(manager.HasPreset(1));
        Assert.IsFalse(manager.HasPreset(2));
    }

    #endregion

    #region Persistence Tests

    [Test]
    public void WithPlayerPrefs_SavePreset_CallsPlayerPrefs()
    {
        var mockPrefs = new MockPlayerPrefsAdapter();
        var manager = new PtzMemoryManager(mockPrefs);

        manager.SavePreset(0, 10f, 20f, 60f, 1000f, 500f);

        Assert.IsTrue(mockPrefs.HasKey("ViscaPtz_Mem0_Pan"));
        Assert.AreEqual(10f, mockPrefs.GetFloat("ViscaPtz_Mem0_Pan", 0f), 0.0001f);
        Assert.AreEqual(20f, mockPrefs.GetFloat("ViscaPtz_Mem0_Tilt", 0f), 0.0001f);
    }

    [Test]
    public void WithPlayerPrefs_DeletePreset_RemovesFromPlayerPrefs()
    {
        var mockPrefs = new MockPlayerPrefsAdapter();
        var manager = new PtzMemoryManager(mockPrefs);

        manager.SavePreset(0, 10f, 20f, 60f, 1000f, 500f);
        Assert.IsTrue(mockPrefs.HasKey("ViscaPtz_Mem0_Pan"));

        manager.DeletePreset(0);
        Assert.IsFalse(mockPrefs.HasKey("ViscaPtz_Mem0_Pan"));
    }

    [Test]
    public void WithPlayerPrefs_LoadsPresetsOnConstruction()
    {
        var mockPrefs = new MockPlayerPrefsAdapter();
        mockPrefs.SetFloat("ViscaPtz_Mem0_Pan", 45f);
        mockPrefs.SetFloat("ViscaPtz_Mem0_Tilt", 30f);
        mockPrefs.SetFloat("ViscaPtz_Mem0_Fov", 75f);
        mockPrefs.SetFloat("ViscaPtz_Mem0_Focus", 500f);
        mockPrefs.SetFloat("ViscaPtz_Mem0_Iris", 250f);

        var manager = new PtzMemoryManager(mockPrefs);

        Assert.IsTrue(manager.TryGetPreset(0, out var preset));
        Assert.AreEqual(45f, preset.PanDeg, 0.0001f);
        Assert.AreEqual(30f, preset.TiltDeg, 0.0001f);
    }

    [Test]
    public void WithCustomPrefix_UsesCorrectPrefix()
    {
        var mockPrefs = new MockPlayerPrefsAdapter();
        var manager = new PtzMemoryManager(mockPrefs, "CustomPrefix_");

        manager.SavePreset(1, 10f, 20f, 60f, 1000f, 500f);

        Assert.IsTrue(mockPrefs.HasKey("CustomPrefix_Mem1_Pan"));
        Assert.IsFalse(mockPrefs.HasKey("ViscaPtz_Mem1_Pan"));
    }

    #endregion

    #region Mock PlayerPrefs

    private class MockPlayerPrefsAdapter : IPlayerPrefsAdapter
    {
        private readonly Dictionary<string, float> _floatStore = new();

        public float GetFloat(string key, float defaultValue = 0f)
        {
            return _floatStore.TryGetValue(key, out var v) ? v : defaultValue;
        }

        public void SetFloat(string key, float value)
        {
            _floatStore[key] = value;
        }

        public bool HasKey(string key)
        {
            return _floatStore.ContainsKey(key);
        }

        public void DeleteKey(string key)
        {
            _floatStore.Remove(key);
        }

        public void Save()
        {
            // No-op for mock
        }
    }

    #endregion
}
