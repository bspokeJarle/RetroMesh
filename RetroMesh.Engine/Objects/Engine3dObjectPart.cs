using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public class Engine3dObjectPart : I3dObjectPart
    {
        public List<ITriangleMeshWithColorAndTexture> Triangles { get; set; } = new();
        public string? PartName { get; set; }
        public bool IsVisible { get; set; }
    }
}
