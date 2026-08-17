namespace RetroMesh.Engine.Tests;

[TestClass]
public class EngineObjectClonerTests
{
    [TestMethod]
    public void CopyRenderableObjects_ReusesResultListAndRunsAdditionalStateCallback()
    {
        var source = CreateRenderableObject();
        var result = new List<Engine3dObject>
        {
            CreateRenderableObject()
        };
        int originalCapacity = result.Capacity;
        string? copiedTag = null;

        EngineObjectCloner.CopyRenderableObjects(
            new[] { source },
            result,
            static objectId => new Engine3dObject { ObjectId = objectId },
            static () => new Engine3dObjectPart(),
            static () => new EngineTriangleMeshWithColor(),
            static vector => new EngineVector3(vector.x, vector.y, vector.z),
            copyCrashboxes: true,
            (original, copy) =>
            {
                copiedTag = $"{original.ObjectName}:{copy.ObjectId}";
                copy.ObjectName = "CopiedByCallback";
            });

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result.Capacity >= originalCapacity);
        Assert.AreEqual("Source:12", copiedTag);
        Assert.AreEqual("CopiedByCallback", result[0].ObjectName);
        Assert.AreEqual(RetroMeshObjType.TwoD, result[0].ObjectType);
        var copiedTwoDState = result[0].TwoDState;
        Assert.IsNotNull(copiedTwoDState);
        Assert.AreNotSame(source.TwoDState, copiedTwoDState);
        Assert.AreEqual("teo.animations", copiedTwoDState.AssetId);
        Assert.AreEqual("walk.right", copiedTwoDState.ActiveAnimationId);
        Assert.AreEqual("teo.walk.right.03", copiedTwoDState.CurrentFrameId);
        Assert.AreEqual(2, copiedTwoDState.FrameIndex);
        Assert.AreEqual(42.5, copiedTwoDState.ElapsedMilliseconds);
        Assert.AreNotSame(source.CrashBoxes, result[0].CrashBoxes);
        Assert.AreNotSame(source.CrashBoxes[0][0], result[0].CrashBoxes[0][0]);
        var copiedTriangle = result[0].ObjectParts[0].Triangles[0];
        Assert.AreEqual("teo.core.atlas", copiedTriangle.TextureId);
        Assert.AreEqual(new TextureCoordinate(0f, 0f), copiedTriangle.Uv1);
        Assert.AreEqual(new TextureCoordinate(1f, 0f), copiedTriangle.Uv2);
        Assert.AreEqual(new TextureCoordinate(1f, 1f), copiedTriangle.Uv3);
    }

    private static Engine3dObject CreateRenderableObject() =>
        new()
        {
            ObjectId = 12,
            ObjectName = "Source",
            ObjectType = RetroMeshObjType.TwoD,
            TwoDState = new TwoDRenderState
            {
                AssetId = "teo.animations",
                ActiveAnimationId = "walk.right",
                CurrentFrameId = "teo.walk.right.03",
                FrameIndex = 2,
                ElapsedMilliseconds = 42.5
            },
            ObjectParts = new List<I3dObjectPart>
            {
                new Engine3dObjectPart
                {
                    PartName = "Body",
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColorAndTexture>
                    {
                        new EngineTriangleMeshWithColor
                        {
                            Color = "ffffff",
                            TextureId = "teo.core.atlas",
                            Uv1 = new TextureCoordinate(0f, 0f),
                            Uv2 = new TextureCoordinate(1f, 0f),
                            Uv3 = new TextureCoordinate(1f, 1f),
                            vert1 = new EngineVector3(1, 2, 3),
                            vert2 = new EngineVector3(4, 5, 6),
                            vert3 = new EngineVector3(7, 8, 9),
                            normal1 = new EngineVector3(0, 0, 1),
                            normal2 = new EngineVector3(0, 1, 0),
                            normal3 = new EngineVector3(1, 0, 0)
                        }
                    }
                }
            },
            CrashBoxes = new List<List<IVector3>>
            {
                new()
                {
                    new EngineVector3(1, 2, 3),
                    new EngineVector3(4, 5, 6)
                }
            }
        };

    private sealed class EngineTriangleMeshWithColor : EngineTriangleMesh, ITriangleMeshWithColorAndTexture
    {
        public string? Color { get; set; }
        public string? TextureId { get; set; }
        public TextureCoordinate Uv1 { get; set; }
        public TextureCoordinate Uv2 { get; set; }
        public TextureCoordinate Uv3 { get; set; }
    }
}
