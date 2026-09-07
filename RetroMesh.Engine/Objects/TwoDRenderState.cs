namespace RetroMesh.Engine
{
    public sealed class TwoDRenderState
    {
        public required string AssetId { get; set; }
        public string? ActiveAnimationId { get; set; }
        public string? CurrentFrameId { get; set; }
        public int FrameIndex { get; set; }
        public double ElapsedMilliseconds { get; set; }
    }
}
