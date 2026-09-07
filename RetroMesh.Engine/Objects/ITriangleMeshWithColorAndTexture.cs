namespace RetroMesh.Engine
{
    public interface ITriangleMeshWithColorAndTexture : ITriangleMesh
    {
        string? Color { get; set; }
        string? TextureId { get; set; }
        TextureCoordinate Uv1 { get; set; }
        TextureCoordinate Uv2 { get; set; }
        TextureCoordinate Uv3 { get; set; }
    }
}
