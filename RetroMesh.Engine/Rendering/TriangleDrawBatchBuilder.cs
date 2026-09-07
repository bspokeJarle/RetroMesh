using System;
using System.Collections.Generic;

namespace RetroMesh.Engine
{
    /// <summary>
    /// A contiguous run of vertices that share a texture and can be drawn in one call.
    /// </summary>
    public readonly record struct TriangleDrawBatch(string? TextureId, int StartVertex, int VertexCount);

    /// <summary>
    /// Groups consecutive triangles that share a texture into draw batches. Triangles are
    /// appended in submission order, so an already depth-sorted list keeps its ordering and
    /// only produces a new batch when the texture actually changes.
    /// </summary>
    public sealed class TriangleDrawBatchBuilder
    {
        private const int VerticesPerTriangle = 3;

        private readonly List<TriangleDrawBatch> batches = new();
        private string? activeTextureId;
        private int batchStartVertex;
        private int vertexCount;

        public IReadOnlyList<TriangleDrawBatch> Batches => batches;

        public int VertexCount => vertexCount;

        public void Reset()
        {
            batches.Clear();
            activeTextureId = null;
            batchStartVertex = 0;
            vertexCount = 0;
        }

        /// <summary>
        /// Appends one triangle drawn with <paramref name="textureId"/>, closing the current
        /// batch first when the texture differs from the previous triangle.
        /// </summary>
        public void AppendTriangle(string? textureId)
        {
            if (vertexCount > batchStartVertex &&
                !string.Equals(activeTextureId, textureId, StringComparison.Ordinal))
            {
                batches.Add(new TriangleDrawBatch(activeTextureId, batchStartVertex, vertexCount - batchStartVertex));
                batchStartVertex = vertexCount;
            }

            activeTextureId = textureId;
            vertexCount += VerticesPerTriangle;
        }

        /// <summary>
        /// Closes the final batch. Must be called after the last <see cref="AppendTriangle"/>.
        /// </summary>
        public void Complete()
        {
            if (vertexCount > batchStartVertex)
            {
                batches.Add(new TriangleDrawBatch(activeTextureId, batchStartVertex, vertexCount - batchStartVertex));
                batchStartVertex = vertexCount;
            }
        }
    }
}
