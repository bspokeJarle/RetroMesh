using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public interface ISurfaceGeometryCache
    {
        List<ITriangleMeshWithColorAndTexture> RotatedSurfaceTriangles { get; set; }
        Dictionary<long, ITriangleMeshWithColorAndTexture> RotatedSurfaceTriangleByLandId { get; set; }
        HashSet<long?> LandBasedIds { get; set; }
    }
}
