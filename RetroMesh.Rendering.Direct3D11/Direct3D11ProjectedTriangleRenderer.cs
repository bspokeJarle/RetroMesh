using RetroMesh.Engine;
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
    private const int SwapChainBufferCount = 2;
    private const string ShaderResourceName = "RetroMesh.Rendering.Direct3D11.Shaders.ProjectedTriangle.hlsl";
    private static readonly int VertexStride = Marshal.SizeOf<GpuVertex>();
    private readonly List<GpuVertex> vertices = new(InitialTriangleCapacity * 3);
    private readonly TriangleDrawBatchBuilder batchBuilder = new();
    private Direct3D11TextureRegistry textureRegistry = null!;
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
    private readonly ID3D11DepthStencilState depthStencilState;
    private ID3D11Texture2D? backBuffer;
    private ID3D11RenderTargetView? renderTarget;
    private ID3D11Texture2D? depthBuffer;
    private ID3D11DepthStencilView? depthStencilView;
    private ID3D11Buffer? vertexBuffer;
    private int vertexCapacity;
    private int width;
    private int height;
    private int projectionWidth;
    private int projectionHeight;
    private bool hasExplicitProjectionSize;
    private bool disposed;
    private int renderingTriangleCount;
    private int nearPlaneTriangleCount;

    public Direct3D11ProjectedTriangleRenderer(nint windowHandle, int width, int height)
    {
        if (windowHandle == 0)
            throw new ArgumentException("A valid child-window handle is required.", nameof(windowHandle));

        this.width = Math.Max(1, width);
        this.height = Math.Max(1, height);
        projectionWidth = this.width;
        projectionHeight = this.height;

        var created = new Stack<IDisposable>();
        try
        {
            factory = CreateDXGIFactory1<IDXGIFactory2>();
            created.Push(factory);
            DeviceCreationFlags flags = DeviceCreationFlags.BgraSupport;
            D3D11CreateDevice(
                IntPtr.Zero,
                DriverType.Hardware,
                flags,
                FeatureLevels,
                out device,
                out FeatureLevel featureLevel,
                out context).CheckError();
            created.Push(context);
            created.Push(device);
            FeatureLevel = featureLevel;

            var swapChainDescription = new SwapChainDescription1
            {
                Width = (uint)this.width,
                Height = (uint)this.height,
                Format = Format.B8G8R8A8_UNorm,
                Stereo = false,
                SampleDescription = new SampleDescription(1, 0),
                BufferUsage = Usage.RenderTargetOutput,
                BufferCount = SwapChainBufferCount,
                Scaling = Scaling.Stretch,
                SwapEffect = SwapEffect.FlipDiscard,
                AlphaMode = AlphaMode.Ignore
            };
            swapChain = factory.CreateSwapChainForHwnd(device, windowHandle, swapChainDescription);
            created.Push(swapChain);
            factory.MakeWindowAssociation(windowHandle, WindowAssociationFlags.IgnoreAltEnter);

            ReadOnlyMemory<byte> vertexBytecode = CompileEmbeddedShader("VSMain", "vs_5_0");
            ReadOnlyMemory<byte> pixelBytecode = CompileEmbeddedShader("PSMain", "ps_5_0");
            vertexShader = device.CreateVertexShader(vertexBytecode.Span);
            created.Push(vertexShader);
            pixelShader = device.CreatePixelShader(pixelBytecode.Span);
            created.Push(pixelShader);
            inputLayout = device.CreateInputLayout(
            [
                new InputElementDescription("POSITION", 0, Format.R32G32_Float, 0, 0),
                new InputElementDescription("COLOR", 0, Format.R32G32B32A32_Float, 8, 0),
                new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 24, 0),
                new InputElementDescription("TEXCOORD", 1, Format.R32_Float, 32, 0),
                new InputElementDescription("TEXCOORD", 2, Format.R32_Float, 36, 0)
            ], vertexBytecode.Span);
            created.Push(inputLayout);

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
            created.Push(sampler);
            rasterizerState = device.CreateRasterizerState(new RasterizerDescription
            {
                FillMode = FillMode.Solid,
                CullMode = CullMode.None,
                DepthClipEnable = false,
                ScissorEnable = false,
                MultisampleEnable = false,
                AntialiasedLineEnable = false
            });
            created.Push(rasterizerState);
            blendState = device.CreateBlendState(BlendDescription.AlphaBlend);
            created.Push(blendState);
            depthStencilState = device.CreateDepthStencilState(new DepthStencilDescription
            {
                DepthEnable = true,
                DepthWriteMask = DepthWriteMask.All,
                DepthFunc = ComparisonFunction.LessEqual,
                StencilEnable = false
            });
            created.Push(depthStencilState);
            textureRegistry = new Direct3D11TextureRegistry(device);
            created.Push(textureRegistry);
            CreateBackBuffer();
        }
        catch
        {
            depthStencilView?.Dispose();
            depthBuffer?.Dispose();
            renderTarget?.Dispose();
            backBuffer?.Dispose();
            while (created.Count > 0)
                created.Pop().Dispose();
            throw;
        }
    }

    private static ReadOnlyMemory<byte> CompileEmbeddedShader(string entryPoint, string profile)
    {
        string source = LoadShaderSource(out string sourceName);
        return Compiler.Compile(source, entryPoint, sourceName, profile, ShaderFlags.EnableStrictness);
    }

    /// <summary>
    /// Prefers a shader file next to the executable so it can be edited and rerun without
    /// a rebuild, and falls back to the embedded copy so the renderer works with no setup.
    /// </summary>
    private static string LoadShaderSource(out string sourceName)
    {
        string shaderPath = Path.Combine(AppContext.BaseDirectory, "Shaders", "ProjectedTriangle.hlsl");
        if (File.Exists(shaderPath))
        {
            sourceName = shaderPath;
            return File.ReadAllText(shaderPath);
        }

        using Stream? stream = typeof(Direct3D11ProjectedTriangleRenderer).Assembly
            .GetManifestResourceStream(ShaderResourceName)
            ?? throw new InvalidOperationException(
                $"No shader found at '{shaderPath}' and the embedded fallback resource " +
                $"'{ShaderResourceName}' is missing from the renderer assembly.");
        using var reader = new StreamReader(stream);
        sourceName = ShaderResourceName;
        return reader.ReadToEnd();
    }

    public FeatureLevel FeatureLevel { get; }
    public Direct3D11TextureRegistry Textures => textureRegistry;
    public bool VerticalSync { get; set; } = true;
    public Color4 ClearColor { get; set; } = new(0.02f, 0.03f, 0.045f, 1f);
    public float ShadeNearZ { get; set; } = -1200f;
    public float ShadeFarZ { get; set; } = 1800f;
    public float MinimumShade { get; set; }

    /// <summary>
    /// When true (the default), triangles with a vertex behind the near plane are rejected
    /// because they cannot be perspective-corrected. Set to false to restore the legacy
    /// behavior of drawing them with a substituted W of 1, which places them at an
    /// approximate depth instead of removing them.
    /// </summary>
    public bool RejectTrianglesBehindNearPlane { get; set; } = true;

    public int GetRenderingTriangleCount() => renderingTriangleCount;

    /// <summary>
    /// Triangles in the last frame that had a vertex behind the near plane. Depending on
    /// <see cref="RejectTrianglesBehindNearPlane"/> these were either rejected or drawn at
    /// an approximate depth. A non-zero value means geometry reached the camera plane.
    /// </summary>
    public int GetNearPlaneTriangleCount() => nearPlaneTriangleCount;

    public void Resize(int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        if (this.width == width && this.height == height)
            return;

        this.width = width;
        this.height = height;
        // Unless the caller pinned a fixed retro resolution via SetProjectionSize, the
        // projection space follows the swap chain so NDC mapping stays correct on resize.
        if (!hasExplicitProjectionSize)
        {
            projectionWidth = width;
            projectionHeight = height;
        }

        context.UnsetRenderTargets();
        depthStencilView?.Dispose();
        depthBuffer?.Dispose();
        renderTarget?.Dispose();
        backBuffer?.Dispose();
        depthStencilView = null;
        depthBuffer = null;
        renderTarget = null;
        backBuffer = null;
        swapChain.ResizeBuffers(SwapChainBufferCount, (uint)width, (uint)height, Format.B8G8R8A8_UNorm, SwapChainFlags.None).CheckError();
        CreateBackBuffer();
    }

    /// <summary>
    /// Pins the projection space used to map projected pixel coordinates into NDC.
    /// Once set, the projection size no longer follows <see cref="Resize"/>.
    /// </summary>
    public void SetProjectionSize(int width, int height)
    {
        projectionWidth = Math.Max(1, width);
        projectionHeight = Math.Max(1, height);
        hasExplicitProjectionSize = true;
    }

    public void RenderTriangles(List<ProjectedTriangleMesh> projectedTriangles)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(projectedTriangles);

        // The depth buffer resolves opaque overlap per pixel, but the blend state is
        // alpha-blended, so translucent triangles must still be submitted back-to-front.
        ProjectedTriangleRenderMath.SortTrianglesByDepth(projectedTriangles);
        BuildVerticesAndBatches(projectedTriangles);
        renderingTriangleCount = vertices.Count / 3;
        EnsureVertexBuffer(vertices.Count);
        UploadVertices();

        context.OMSetRenderTargets(renderTarget!, depthStencilView);
        context.OMSetBlendState(blendState);
        context.OMSetDepthStencilState(depthStencilState);
        context.RSSetState(rasterizerState);
        context.RSSetViewport(0, 0, width, height);
        context.ClearRenderTargetView(renderTarget!, ClearColor);
        context.ClearDepthStencilView(depthStencilView!, DepthStencilClearFlags.Depth, 1f, 0);
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.IASetInputLayout(inputLayout);
        context.IASetVertexBuffer(0, vertexBuffer!, (uint)VertexStride);
        context.VSSetShader(vertexShader);
        context.PSSetShader(pixelShader);
        context.PSSetSampler(0, sampler);

        foreach (TriangleDrawBatch batch in batchBuilder.Batches)
        {
            context.PSSetShaderResource(0, textureRegistry.GetViewOrFallback(batch.TextureId));
            context.Draw((uint)batch.VertexCount, (uint)batch.StartVertex);
        }

        swapChain.Present(VerticalSync ? 1u : 0u, PresentFlags.None).CheckError();
    }

    private void BuildVerticesAndBatches(List<ProjectedTriangleMesh> triangles)
    {
        vertices.Clear();
        batchBuilder.Reset();
        nearPlaneTriangleCount = 0;

        foreach (ProjectedTriangleMesh triangle in triangles)
        {
            // The projection pipeline emits rhw <= 0 for vertices behind the near plane,
            // which cannot be perspective-corrected. By default such triangles are rejected;
            // with RejectTrianglesBehindNearPlane disabled they are drawn with a substituted
            // W of 1, placing them at an approximate depth.
            if (!ProjectedTriangleRenderMath.IsRenderableReciprocalW(triangle.Rhw1) ||
                !ProjectedTriangleRenderMath.IsRenderableReciprocalW(triangle.Rhw2) ||
                !ProjectedTriangleRenderMath.IsRenderableReciprocalW(triangle.Rhw3))
            {
                nearPlaneTriangleCount++;
                if (RejectTrianglesBehindNearPlane)
                    continue;
            }

            string? resolvedTexture = textureRegistry.Contains(triangle.TextureId) ? triangle.TextureId : null;
            batchBuilder.AppendTriangle(resolvedTexture);

            Vector4 color = GetShadedColor(triangle);
            float useTexture = resolvedTexture == null ? 0f : 1f;
            vertices.Add(CreateVertex(triangle.X1, triangle.Y1, triangle.Uv1, triangle.Rhw1, color, useTexture));
            vertices.Add(CreateVertex(triangle.X2, triangle.Y2, triangle.Uv2, triangle.Rhw2, color, useTexture));
            vertices.Add(CreateVertex(triangle.X3, triangle.Y3, triangle.Uv3, triangle.Rhw3, color, useTexture));
        }

        batchBuilder.Complete();
    }

    private GpuVertex CreateVertex(int x, int y, TextureCoordinate uv, float rhw, Vector4 color, float useTexture)
    {
        var (ndcX, ndcY) = ProjectedTriangleRenderMath.ScreenToNormalizedDevice(
            x, y, projectionWidth, projectionHeight);
        float safeRhw = ProjectedTriangleRenderMath.IsRenderableReciprocalW(rhw) ? rhw : 1f;
        return new GpuVertex(ndcX, ndcY, color, uv.U, uv.V, safeRhw, useTexture);
    }

    private Vector4 GetShadedColor(ProjectedTriangleMesh triangle)
    {
        float shadeKey = RenderShadeMath.GetTriangleShadeKey(
            triangle.CalculatedZ, triangle.TriangleAngle, ShadeNearZ, ShadeFarZ);
        shadeKey = Math.Max(Math.Clamp(MinimumShade, 0f, 1f), shadeKey);
        var (r, g, b) = RenderColorShading.GetShadeChannelsFromNormal(shadeKey, triangle.Color);
        return new Vector4(r / 255f, g / 255f, b / 255f, 1f);
    }

    private void EnsureVertexBuffer(int requiredVertices)
    {
        if (requiredVertices <= vertexCapacity)
            return;

        vertexCapacity = Math.Max(requiredVertices, Math.Max(InitialTriangleCapacity * 3, vertexCapacity * 2));
        vertexBuffer?.Dispose();
        vertexBuffer = device.CreateBuffer(new BufferDescription
        {
            ByteWidth = (uint)(vertexCapacity * VertexStride),
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
        depthBuffer = device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.D32_Float,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.DepthStencil
        });
        depthStencilView = device.CreateDepthStencilView(depthBuffer);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        context.ClearState();
        ((IDisposable)textureRegistry).Dispose();
        vertexBuffer?.Dispose();
        depthStencilState.Dispose();
        blendState.Dispose();
        rasterizerState.Dispose();
        sampler.Dispose();
        inputLayout.Dispose();
        pixelShader.Dispose();
        vertexShader.Dispose();
        depthStencilView?.Dispose();
        depthBuffer?.Dispose();
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
}
