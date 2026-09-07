using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public interface I3dObjectPart
    {
        List<ITriangleMeshWithColorAndTexture> Triangles { get; set; }
        string? PartName { get; set; }
        bool IsVisible { get; set; }
    }
}
