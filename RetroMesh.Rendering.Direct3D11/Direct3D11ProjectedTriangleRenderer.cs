using RetroMesh.Engine;
using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DXGI.DXGI;

namespace RetroMesh.Rendering.Direct3D11;

public sealed class Direct3D11ProjectedTriangleRenderer :
    IProjectedTriangleRenderer<ProjectedTriangleMesh>, IDisposable
{
    private static readonly FeatureLevel[] FeatureLevels =
    [
        FeatureLevel.Level_11_1,
        FeatureLevel.Level_11_0,
        FeatureLevel.Level_10_1,
        FeatureLevel.Level_10_0
    ];

    private const int InitialTriangleCapacity = 1024;
    private readonly nint windowHandle;
    private readonly List<GpuVertex> vertices = new(InitialTriangleCapacity * 3);
    private readonly List<DrawBatch> batches = new();
    private readonly IDXGIFactory2 factory;
    private readonly ID3D11Device device;
    private readonly ID3D11DeviceContext context;
    private readonly IDXGISwapChain1 swapChain;
    private readonly ID3D11VertexShader vertexShader;
    private readonly ID3D11PixelShader pixelShader;
    private readonly ID3D11InputLayout inputLayout;
    private readonly ID3D11SamplerState sampler;
    private readonly ID3D11RasterizerState rasterizerState;
    private readonly ID3D11BlendState blendState;
    private ID3D11Texture2D? backBuffer;
    private ID3D11RenderTargetView? renderTarget;
    private ID3D11Buffer? vertexBuffer;
    private int vertexCapacity;
    private int width;
    private int height;
    private int projectionWidth;
    private int projectionHeight;
    private bool disposed;
    private int renderingTriangleCount;

    public Direct3D11ProjectedTriangleRenderer(nint windowHandle, int width, int height)
    {
        if (windowHandle == 0)
            throw new ArgumentException("A valid child-window handle is required.", nameof(windowHandle));

        this.windowHandle = windowHandle;
        this.width = Math.Max(1, width);
        this.height = Math.Max(1, height);
        projectionWidth = this.width;
        projectionHeight = this.height;

        factory = CreateDXGIFactory1<IDXGIFactory2>();
        DeviceCreationFlags flags = DeviceCreationFlags.BgraSupport;
        D3D11CreateDevice(
            IntPtr.Zero,
            DriverType.Hardware,
            flags,
            FeatureLevels,
            out device,
            out FeatureLevel featureLevel,
            out context).CheckError();
        FeatureLevel = featureLevel;

        var swapChainDescription = new SwapChainDescription1
        {
            Width = (uint)this.width,
            Height = (uint)this.height,
            Format = Format.B8G8R8A8_UNorm,
            Stereo = false,
            SampleDescription = new SampleDescription(1, 0),
            BufferUsage = Usage.RenderTargetOutput,
            BufferCount = 2,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = AlphaMode.Ignore
        };
        swapChain = factory.CreateSwapChainForHwnd(device, windowHandle, swapChainDescription);
        factory.MakeWindowAssociation(windowHandle, WindowAssociationFlags.IgnoreAltEnter);

        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "ProjectedTriangle.hlsl");
        ReadOnlyMemory<byte> vertexBytecode = Compiler.CompileFromFile(shaderPath, "VSMain", "vs_5_0", ShaderFlags.EnableStrictness);
        ReadOnlyMemory<byte> pixelBytecode = Compiler.CompileFromFile(shaderPath, "PSMain", "ps_5_0", ShaderFlags.EnableStrictness);
        vertexShader = device.CreateVertexShader(vertexBytecode.Span);
        pixelShader = device.CreatePixelShader(pixelBytecode.Span);
        inputLayout = device.CreateInputLayout(
        [
            new InputElementDescription("POSITION", 0, Format.R32G32_Float, 0, 0),
            new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 8, 0),
            new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 24, 0),
            new InputElementDescription("TEXCOORD", 1, Format.R32_Float, 32, 0),
            new InputElementDescription("TEXCOORD", 2, Format.R32_Float, 36, 0)
        ], vertexBytecode.Span);

        sampler = device.CreateSamplerState(new SamplerDescription
        {
            Filter = Filter.MinMagMipPoint,
            AddressU = TextureAddressMode.Clamp,
            AddressV = TextureAddressMode.Clamp,
            AddressW = TextureAddressMode.Clamp,
            ComparisonFunc = ComparisonFunction.Never,
            MinLOD = 0,
            MaxLOD = float.MaxValue
        });
        rasterizerState = device.CreateRasterizerState(new RasterizerDescription
        {
            FillMode = FillMode.Solid,
            CullMode = CullMode.None,
            DepthClipEnable = false,
            ScissorEnable = false,
            MultisampleEnable = false,
            AntialiasedLineEnable = false
        });
        blendState = device.CreateBlendState(BlendDescription.AlphaBlend);
        Textures = new Direct3D11TextureRegistry(device);
        CreateBackBuffer();
    }

    public FeatureLevel FeatureLevel { get; }
    public Direct3D11TextureRegistry Textures { get; }
    public bool VerticalSync { get; set; } = true;
    public Color4 ClearColor { get; set; } = new(0.02f, 0.03f, 0.045f, 1f);

    public int GetRenderingTriangleCount() => renderingTriangleCount;

    public void Resize(int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (this.width == width && this.height == height)
            return;

        this.width = width;
        this.height = height;
        context.UnsetRenderTargets();
        renderTarget?.Dispose();
        backBuffer?.Dispose();
        renderTarget = null;
        backBuffer = null;
        swapChain.ResizeBuffers(2, (uint)width, (uint)height, Format.B8G8R8A8_UNorm, SwapChainFlags.None).CheckError();
        CreateBackBuffer();
    }

    public void SetProjectionSize(int width, int height)
    {
        projectionWidth = Math.Max(1, width);
        projectionHeight = Math.Max(1, height);
    }

    public void RenderTriangles(List<ProjectedTriangleMesh> projectedTriangles)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(projectedTriangles);

        ProjectedTriangleRenderMath.SortTrianglesByDepth(projectedTriangles);
        renderingTriangleCount = projectedTriangles.Count;
        BuildVerticesAndBatches(projectedTriangles);
        EnsureVertexBuffer(vertices.Count);
        UploadVertices();

        context.OMSetRenderTargets(renderTarget!);
        context.OMSetBlendState(blendState);
        context.RSSetState(rasterizerState);
        context.RSSetViewport(0, 0, width, height);
        context.ClearRenderTargetView(renderTarget!, ClearColor);
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.IASetInputLayout(inputLayout);
        context.IASetVertexBuffer(0, vertexBuffer!, (uint)Marshal.SizeOf<GpuVertex>());
        context.VSSetShader(vertexShader);
        context.PSSetShader(pixelShader);
        context.PSSetSampler(0, sampler);

        foreach (DrawBatch batch in batches)
        {
            Textures.TryGetView(batch.TextureId, out ID3D11ShaderResourceView view);
            context.PSSetShaderResource(0, view);
            context.Draw((uint)batch.VertexCount, (uint)batch.StartVertex);
        }

        swapChain.Present(VerticalSync ? 1u : 0u, PresentFlags.None).CheckError();
    }

    private void BuildVerticesAndBatches(List<ProjectedTriangleMesh> triangles)
    {
        vertices.Clear();
        batches.Clear();
        string? activeTexture = null;
        int batchStart = 0;

        foreach (ProjectedTriangleMesh triangle in triangles)
        {
            string? resolvedTexture = Textures.TryGetView(triangle.TextureId, out _) ? triangle.TextureId : null;
            if (vertices.Count > batchStart && !string.Equals(activeTexture, resolvedTexture, StringComparison.Ordinal))
            {
                batches.Add(new DrawBatch(activeTexture, batchStart, vertices.Count - batchStart));
                batchStart = vertices.Count;
            }
            activeTexture = resolvedTexture;

            Vector4 color = ParseAndShadeColor(triangle);
            float useTexture = resolvedTexture == null ? 0f : 1f;
            vertices.Add(CreateVertex(triangle.X1, triangle.Y1, triangle.Uv1, triangle.Rhw1, color, useTexture));
            vertices.Add(CreateVertex(triangle.X2, triangle.Y2, triangle.Uv2, triangle.Rhw2, color, useTexture));
            vertices.Add(CreateVertex(triangle.X3, triangle.Y3, triangle.Uv3, triangle.Rhw3, color, useTexture));
        }

        if (vertices.Count > batchStart)
            batches.Add(new DrawBatch(activeTexture, batchStart, vertices.Count - batchStart));
    }

    private GpuVertex CreateVertex(int x, int y, TextureCoordinate uv, float rhw, Vector4 color, float useTexture)
    {
        float ndcX = (x / (float)projectionWidth) * 2f - 1f;
        float ndcY = 1f - (y / (float)projectionHeight) * 2f;
        return new GpuVertex(ndcX, ndcY, color, uv.U, uv.V, rhw > 0f ? rhw : 1f, useTexture);
    }

    private static Vector4 ParseAndShadeColor(ProjectedTriangleMesh triangle)
    {
        string shaded = RenderColorShading.GetShadeOfColorFromNormal(
            RenderShadeMath.GetTriangleShadeKey(triangle.CalculatedZ, triangle.TriangleAngle, -1200f, 1800f),
            triangle.Color);
        string hex = shaded.Trim().TrimStart('#');
        if (hex.Length != 6 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
            return new Vector4(0f, 1f, 1f, 1f);
        return new Vector4(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);
    }

    private void EnsureVertexBuffer(int requiredVertices)
    {
        if (requiredVertices <= vertexCapacity)
            return;

        vertexCapacity = Math.Max(requiredVertices, Math.Max(InitialTriangleCapacity * 3, vertexCapacity * 2));
        vertexBuffer?.Dispose();
        vertexBuffer = device.CreateBuffer(new BufferDescription
        {
            ByteWidth = (uint)(vertexCapacity * Marshal.SizeOf<GpuVertex>()),
            Usage = ResourceUsage.Dynamic,
            BindFlags = BindFlags.VertexBuffer,
            CPUAccessFlags = CpuAccessFlags.Write
        });
    }

    private unsafe void UploadVertices()
    {
        if (vertices.Count == 0)
            return;
        MappedSubresource mapped = context.Map(vertexBuffer!, 0, MapMode.WriteDiscard, Vortice.Direct3D11.MapFlags.None);
        ReadOnlySpan<GpuVertex> source = CollectionsMarshal.AsSpan(vertices);
        fixed (GpuVertex* sourcePointer = source)
        {
            long bytes = source.Length * sizeof(GpuVertex);
            Buffer.MemoryCopy(sourcePointer, (void*)mapped.DataPointer, bytes, bytes);
        }
        context.Unmap(vertexBuffer!, 0);
    }

    private void CreateBackBuffer()
    {
        backBuffer = swapChain.GetBuffer<ID3D11Texture2D>(0);
        renderTarget = device.CreateRenderTargetView(backBuffer);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        context.ClearState();
        Textures.Dispose();
        vertexBuffer?.Dispose();
        blendState.Dispose();
        rasterizerState.Dispose();
        sampler.Dispose();
        inputLayout.Dispose();
        pixelShader.Dispose();
        vertexShader.Dispose();
        renderTarget?.Dispose();
        backBuffer?.Dispose();
        swapChain.Dispose();
        context.Dispose();
        device.Dispose();
        factory.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct GpuVertex(
        float X,
        float Y,
        Vector4 Color,
        float U,
        float V,
        float Rhw,
        float UseTexture);

    private readonly record struct DrawBatch(string? TextureId, int StartVertex, int VertexCount);
}
