
namespace RetroMesh.Engine.Tests;

[TestClass]
public class EngineProjectedTriangleScreenMathTests
{
    [TestMethod]
    public void ScreenToNormalizedDevice_MapsCornersAndCentre()
    {
        var topLeft = ProjectedTriangleRenderMath.ScreenToNormalizedDevice(0, 0, 800, 600);
        var bottomRight = ProjectedTriangleRenderMath.ScreenToNormalizedDevice(800, 600, 800, 600);
        var centre = ProjectedTriangleRenderMath.ScreenToNormalizedDevice(400, 300, 800, 600);

        Assert.AreEqual(-1f, topLeft.X, 1e-5f);
        Assert.AreEqual(1f, topLeft.Y, 1e-5f);
        Assert.AreEqual(1f, bottomRight.X, 1e-5f);
        Assert.AreEqual(-1f, bottomRight.Y, 1e-5f);
        Assert.AreEqual(0f, centre.X, 1e-5f);
        Assert.AreEqual(0f, centre.Y, 1e-5f);
    }

    [TestMethod]
    public void ScreenToNormalizedDevice_FlipsYSoScreenDownIsNdcDown()
    {
        var upper = ProjectedTriangleRenderMath.ScreenToNormalizedDevice(0, 150, 800, 600);
        var lower = ProjectedTriangleRenderMath.ScreenToNormalizedDevice(0, 450, 800, 600);

        Assert.IsTrue(upper.Y > lower.Y, "A smaller screen Y must map to a larger NDC Y.");
    }

    [TestMethod]
    public void ScreenToNormalizedDevice_RejectsNonPositiveProjectionSize()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => ProjectedTriangleRenderMath.ScreenToNormalizedDevice(0, 0, 0, 600));
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => ProjectedTriangleRenderMath.ScreenToNormalizedDevice(0, 0, 800, 0));
    }

    [TestMethod]
    public void IsRenderableReciprocalW_AcceptsOnlyPositiveFiniteValues()
    {
        Assert.IsTrue(ProjectedTriangleRenderMath.IsRenderableReciprocalW(0.5f));
        Assert.IsFalse(ProjectedTriangleRenderMath.IsRenderableReciprocalW(0f));
        Assert.IsFalse(ProjectedTriangleRenderMath.IsRenderableReciprocalW(-0.5f));
        Assert.IsFalse(ProjectedTriangleRenderMath.IsRenderableReciprocalW(float.NaN));
        Assert.IsFalse(ProjectedTriangleRenderMath.IsRenderableReciprocalW(float.PositiveInfinity));
    }
}
