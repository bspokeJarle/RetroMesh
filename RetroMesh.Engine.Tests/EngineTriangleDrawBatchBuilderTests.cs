
namespace RetroMesh.Engine.Tests;

[TestClass]
public class EngineTriangleDrawBatchBuilderTests
{
    [TestMethod]
    public void AppendTriangle_GroupsConsecutiveTrianglesSharingTexture()
    {
        var builder = new TriangleDrawBatchBuilder();

        builder.AppendTriangle("grass");
        builder.AppendTriangle("grass");
        builder.AppendTriangle("rock");
        builder.Complete();

        Assert.AreEqual(9, builder.VertexCount);
        Assert.AreEqual(2, builder.Batches.Count);
        Assert.AreEqual(new TriangleDrawBatch("grass", 0, 6), builder.Batches[0]);
        Assert.AreEqual(new TriangleDrawBatch("rock", 6, 3), builder.Batches[1]);
    }

    [TestMethod]
    public void AppendTriangle_StartsNewBatchWhenTextureReturnsAfterAnother()
    {
        var builder = new TriangleDrawBatchBuilder();

        builder.AppendTriangle("grass");
        builder.AppendTriangle("rock");
        builder.AppendTriangle("grass");
        builder.Complete();

        Assert.AreEqual(3, builder.Batches.Count);
        CollectionAssert.AreEqual(
            new[] { "grass", "rock", "grass" },
            builder.Batches.Select(batch => batch.TextureId).ToArray());
    }

    [TestMethod]
    public void AppendTriangle_TreatsUntexturedTrianglesAsTheirOwnBatch()
    {
        var builder = new TriangleDrawBatchBuilder();

        builder.AppendTriangle(null);
        builder.AppendTriangle(null);
        builder.AppendTriangle("grass");
        builder.Complete();

        Assert.AreEqual(2, builder.Batches.Count);
        Assert.IsNull(builder.Batches[0].TextureId);
        Assert.AreEqual(6, builder.Batches[0].VertexCount);
        Assert.AreEqual("grass", builder.Batches[1].TextureId);
    }

    [TestMethod]
    public void Complete_ProducesNoBatchesWhenNothingWasAppended()
    {
        var builder = new TriangleDrawBatchBuilder();

        builder.Complete();

        Assert.AreEqual(0, builder.VertexCount);
        Assert.AreEqual(0, builder.Batches.Count);
    }

    [TestMethod]
    public void Complete_IsIdempotentAndDoesNotDuplicateTheFinalBatch()
    {
        var builder = new TriangleDrawBatchBuilder();

        builder.AppendTriangle("grass");
        builder.Complete();
        builder.Complete();

        Assert.AreEqual(1, builder.Batches.Count);
    }

    [TestMethod]
    public void Reset_ClearsBatchesAndVertexCountForReuseAcrossFrames()
    {
        var builder = new TriangleDrawBatchBuilder();
        builder.AppendTriangle("grass");
        builder.Complete();

        builder.Reset();
        builder.AppendTriangle("rock");
        builder.Complete();

        Assert.AreEqual(3, builder.VertexCount);
        Assert.AreEqual(1, builder.Batches.Count);
        Assert.AreEqual(new TriangleDrawBatch("rock", 0, 3), builder.Batches[0]);
    }
}
