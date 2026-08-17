namespace RetroMesh.Engine
{
    public interface IProjectedTriangle
    {
        string PartName { get; set; }
        bool UseEffectRenderingPipeline { get; set; }
        float CalculatedZ { get; set; }
        float Normal { get; set; }
        float TriangleAngle { get; set; }
        int X1 { get; set; }
        int Y1 { get; set; }
        int X2 { get; set; }
        int Y2 { get; set; }
        int X3 { get; set; }
        int Y3 { get; set; }
        string Color { get; set; }
        string? TextureId { get; set; }
        TextureCoordinate Uv1 { get; set; }
        TextureCoordinate Uv2 { get; set; }
        TextureCoordinate Uv3 { get; set; }
        float Rhw1 { get; set; }
        float Rhw2 { get; set; }
        float Rhw3 { get; set; }
    }
}
