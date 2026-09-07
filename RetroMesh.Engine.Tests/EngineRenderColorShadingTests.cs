
namespace RetroMesh.Engine.Tests;

[TestClass]
public class EngineRenderColorShadingTests
{
    [TestMethod]
    public void GetShadeChannelsFromNormal_MatchesHexStringShading()
    {
        string[] colors = ["#8040C0", "8040C0", "ff0000", "#000000", ""];
        float[] normals = [0f, 0.25f, 0.5f, 1f, -0.75f];

        foreach (string color in colors)
        {
            foreach (float normal in normals)
            {
                var (r, g, b) = RenderColorShading.GetShadeChannelsFromNormal(normal, color);
                string expected = RenderColorShading.GetShadeOfColorFromNormal(normal, color);

                Assert.AreEqual(
                    expected,
                    $"#{r:X2}{g:X2}{b:X2}",
                    $"Channel shading diverged from string shading for '{color}' at normal {normal}.");
            }
        }
    }

    [TestMethod]
    public void GetShadeChannelsFromNormal_ReturnsBlackForTooShortColor()
    {
        var (r, g, b) = RenderColorShading.GetShadeChannelsFromNormal(1f, "#ABC");

        Assert.AreEqual(0, r);
        Assert.AreEqual(0, g);
        Assert.AreEqual(0, b);
    }

    [TestMethod]
    public void GetShadeChannelsFromNormal_ClampsChannelsToByteRange()
    {
        var (r, g, b) = RenderColorShading.GetShadeChannelsFromNormal(10f, "#FFFFFF");

        Assert.AreEqual(255, r);
        Assert.AreEqual(255, g);
        Assert.AreEqual(255, b);
    }
}
