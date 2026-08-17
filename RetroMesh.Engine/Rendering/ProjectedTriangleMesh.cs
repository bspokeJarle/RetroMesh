namespace RetroMesh.Engine
{
    public struct ProjectedTriangleMesh : IProjectedTriangle
    {
        public string PartName { get; set; }
        public bool UseEffectRenderingPipeline { get; set; }
        public float CalculatedZ { get; set; }
        public float Normal { get; set; }
        public float TriangleAngle { get; set; }
        public int X1 { get; set; }
        public int Y1 { get; set; }
        public int X2 { get; set; }
        public int Y2 { get; set; }
        public int X3 { get; set; }
        public int Y3 { get; set; }
        public string Color { get; set; }
        public string? TextureId { get; set; }
        public TextureCoordinate Uv1 { get; set; }
        public TextureCoordinate Uv2 { get; set; }
        public TextureCoordinate Uv3 { get; set; }
    }
}
