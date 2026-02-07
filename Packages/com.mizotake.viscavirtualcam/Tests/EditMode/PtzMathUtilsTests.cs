using NUnit.Framework;
using ViscaControlVirtualCam;

public class PtzMathUtilsTests
{
    #region Lerp Tests

    [Test]
    public void Lerp_AtZero_ReturnsA()
    {
        var result = PtzMathUtils.Lerp(10f, 20f, 0f);
        Assert.AreEqual(10f, result, 0.0001f);
    }

    [Test]
    public void Lerp_AtOne_ReturnsB()
    {
        var result = PtzMathUtils.Lerp(10f, 20f, 1f);
        Assert.AreEqual(20f, result, 0.0001f);
    }

    [Test]
    public void Lerp_AtHalf_ReturnsMidpoint()
    {
        var result = PtzMathUtils.Lerp(0f, 100f, 0.5f);
        Assert.AreEqual(50f, result, 0.0001f);
    }

    #endregion

    #region SafeInverseLerp Tests

    [Test]
    public void SafeInverseLerp_NormalRange_ReturnsCorrectValue()
    {
        var result = PtzMathUtils.SafeInverseLerp(0f, 100f, 50f);
        Assert.AreEqual(0.5f, result, 0.0001f);
    }

    [Test]
    public void SafeInverseLerp_AtA_ReturnsZero()
    {
        var result = PtzMathUtils.SafeInverseLerp(10f, 20f, 10f);
        Assert.AreEqual(0f, result, 0.0001f);
    }

    [Test]
    public void SafeInverseLerp_AtB_ReturnsOne()
    {
        var result = PtzMathUtils.SafeInverseLerp(10f, 20f, 20f);
        Assert.AreEqual(1f, result, 0.0001f);
    }

    [Test]
    public void SafeInverseLerp_EqualAB_ReturnsHalf()
    {
        var result = PtzMathUtils.SafeInverseLerp(10f, 10f, 10f);
        Assert.AreEqual(0.5f, result, 0.0001f);
    }

    #endregion

    #region Clamp Tests

    [Test]
    public void Clamp_Float_WithinRange_ReturnsValue()
    {
        var result = PtzMathUtils.Clamp(5f, 0f, 10f);
        Assert.AreEqual(5f, result);
    }

    [Test]
    public void Clamp_Float_BelowMin_ReturnsMin()
    {
        var result = PtzMathUtils.Clamp(-5f, 0f, 10f);
        Assert.AreEqual(0f, result);
    }

    [Test]
    public void Clamp_Float_AboveMax_ReturnsMax()
    {
        var result = PtzMathUtils.Clamp(15f, 0f, 10f);
        Assert.AreEqual(10f, result);
    }

    [Test]
    public void Clamp_Int_WithinRange_ReturnsValue()
    {
        var result = PtzMathUtils.Clamp(5, 0, 10);
        Assert.AreEqual(5, result);
    }

    [Test]
    public void Clamp_Int_BelowMin_ReturnsMin()
    {
        var result = PtzMathUtils.Clamp(-5, 0, 10);
        Assert.AreEqual(0, result);
    }

    [Test]
    public void Clamp_Int_AboveMax_ReturnsMax()
    {
        var result = PtzMathUtils.Clamp(15, 0, 10);
        Assert.AreEqual(10, result);
    }

    #endregion

    #region Damp Tests

    [Test]
    public void Damp_ApproachesTarget()
    {
        var current = 0f;
        var target = 100f;
        var damping = 5f;
        var dt = 0.1f;

        var result = PtzMathUtils.Damp(current, target, damping, dt);

        Assert.Greater(result, current, "Should move towards target");
        Assert.Less(result, target, "Should not overshoot");
    }

    [Test]
    public void Damp_AtTarget_StaysAtTarget()
    {
        var result = PtzMathUtils.Damp(100f, 100f, 5f, 0.1f);
        Assert.AreEqual(100f, result, 0.0001f);
    }

    [Test]
    public void Damp_HigherDamping_FasterApproach()
    {
        var current = 0f;
        var target = 100f;
        var dt = 0.1f;

        var lowDamp = PtzMathUtils.Damp(current, target, 1f, dt);
        var highDamp = PtzMathUtils.Damp(current, target, 10f, dt);

        Assert.Greater(highDamp, lowDamp, "Higher damping should approach faster");
    }

    #endregion

    #region DeltaAngle Tests

    [Test]
    public void DeltaAngle_SameAngle_ReturnsZero()
    {
        var result = PtzMathUtils.DeltaAngle(45f, 45f);
        Assert.AreEqual(0f, result, 0.0001f);
    }

    [Test]
    public void DeltaAngle_SimplePositive_ReturnsCorrect()
    {
        var result = PtzMathUtils.DeltaAngle(0f, 90f);
        Assert.AreEqual(90f, result, 0.0001f);
    }

    [Test]
    public void DeltaAngle_SimpleNegative_ReturnsCorrect()
    {
        var result = PtzMathUtils.DeltaAngle(90f, 0f);
        Assert.AreEqual(-90f, result, 0.0001f);
    }

    [Test]
    public void DeltaAngle_WrapPositive_TakesShortestPath()
    {
        // From 350 to 10 should be +20, not -340
        var result = PtzMathUtils.DeltaAngle(350f, 10f);
        Assert.AreEqual(20f, result, 0.0001f);
    }

    [Test]
    public void DeltaAngle_WrapNegative_TakesShortestPath()
    {
        // From 10 to 350 should be -20, not +340
        var result = PtzMathUtils.DeltaAngle(10f, 350f);
        Assert.AreEqual(-20f, result, 0.0001f);
    }

    #endregion

    #region MoveTowards Tests

    [Test]
    public void MoveTowards_WithinDelta_ReturnsTarget()
    {
        var result = PtzMathUtils.MoveTowards(95f, 100f, 10f);
        Assert.AreEqual(100f, result, 0.0001f);
    }

    [Test]
    public void MoveTowards_BeyondDelta_MovesMaxDelta()
    {
        var result = PtzMathUtils.MoveTowards(0f, 100f, 10f);
        Assert.AreEqual(10f, result, 0.0001f);
    }

    [Test]
    public void MoveTowards_Negative_MovesCorrectDirection()
    {
        var result = PtzMathUtils.MoveTowards(100f, 0f, 10f);
        Assert.AreEqual(90f, result, 0.0001f);
    }

    [Test]
    public void MoveTowards_AtTarget_StaysAtTarget()
    {
        var result = PtzMathUtils.MoveTowards(50f, 50f, 10f);
        Assert.AreEqual(50f, result, 0.0001f);
    }

    #endregion

    #region MapSpeed Tests

    [Test]
    public void MapSpeed_MinSpeed_ReturnsZero()
    {
        // At vmin, t=0 so result is 0 (this is expected behavior)
        var result = PtzMathUtils.MapSpeed(0x01, 0x01, 0x18, 120f, 1f);
        Assert.AreEqual(0f, result, 0.0001f);
    }

    [Test]
    public void MapSpeed_MinSpeedFloor_IsApplied()
    {
        var result = PtzMathUtils.MapSpeed(0x01, 0x01, 0x18, 0.5f, 120f, 1f);
        Assert.AreEqual(0.5f, result, 0.0001f);
    }

    [Test]
    public void MapSpeed_MidSpeed_ReturnsPositive()
    {
        // Mid-range speed should return positive value
        var result = PtzMathUtils.MapSpeed(0x0C, 0x01, 0x18, 120f, 1f);
        Assert.Greater(result, 0f);
        Assert.Less(result, 120f);
    }

    [Test]
    public void MapSpeed_MaxSpeed_ReturnsMaxDegPerSec()
    {
        var result = PtzMathUtils.MapSpeed(0x18, 0x01, 0x18, 120f, 1f);
        Assert.AreEqual(120f, result, 0.0001f);
    }

    [Test]
    public void MapSpeed_ZeroInput_TreatedAsMin()
    {
        var resultZero = PtzMathUtils.MapSpeed(0x00, 0x01, 0x18, 120f, 1f);
        var resultMin = PtzMathUtils.MapSpeed(0x01, 0x01, 0x18, 120f, 1f);
        Assert.AreEqual(resultMin, resultZero, 0.0001f);
    }

    [Test]
    public void MapSpeed_Gamma_AffectsMapping()
    {
        var linear = PtzMathUtils.MapSpeed(0x0C, 0x01, 0x18, 120f, 1f);
        var gamma2 = PtzMathUtils.MapSpeed(0x0C, 0x01, 0x18, 120f, 2f);

        // With gamma > 1, mid values should map to lower speeds
        Assert.Less(gamma2, linear);
    }

    #endregion
}
