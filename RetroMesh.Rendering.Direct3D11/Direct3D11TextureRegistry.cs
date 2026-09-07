using Vortice.Direct3D11;
using Vortice.DXGI;

namespace RetroMesh.Rendering.Direct3D11;

/// <summary>
/// Registry of GPU textures owned by <see cref="Direct3D11ProjectedTriangleRenderer"/>.
/// The renderer owns the lifetime of every registered texture and disposes them, so
/// <see cref="IDisposable.Dispose"/> is implemented explicitly and is not part of the
/// normal surface area. Consumers should only call <see cref="RegisterBgra32"/>.
/// </summary>
public sealed class Direct3D11TextureRegistry : IDisposable
{
    private readonly ID3D11Device device;
    private readonly Dictionary<string, TextureResource> textures = new(StringComparer.Ordinal);
    private readonly TextureResource fallbackTexture;

    internal Direct3D11TextureRegistry(ID3D11Device device)
    {
        this.device = device;
        fallbackTexture = CreateTexture(1, 1, [255, 255, 255, 255]);
    }

    public void RegisterBgra32(string textureId, int width, int height, ReadOnlySpan<byte> pixels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(textureId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (pixels.Length != checked(width * height * 4))
            throw new ArgumentException("BGRA32 pixel data must contain exactly width * height * 4 bytes.", nameof(pixels));

        TextureResource resource = CreateTexture(width, height, pixels);

        if (textures.Remove(textureId, out TextureResource? previous))
            previous.Dispose();
        textures.Add(textureId, resource);
    }

    /// <summary>
    /// Returns whether a texture is registered under <paramref name="textureId"/>.
    /// </summary>
    internal bool Contains(string? textureId)
        => textureId != null && textures.ContainsKey(textureId);

    /// <summary>
    /// Returns the view registered under <paramref name="textureId"/>, or the opaque
    /// white 1x1 fallback view when the id is null or unregistered. Never returns null,
    /// so an unregistered id renders as untextured vertex color rather than failing.
    /// </summary>
    internal ID3D11ShaderResourceView GetViewOrFallback(string? textureId)
    {
        if (textureId != null && textures.TryGetValue(textureId, out TextureResource? resource))
            return resource.View;

        return fallbackTexture.View;
    }

    private unsafe TextureResource CreateTexture(int width, int height, ReadOnlySpan<byte> pixels)
    {
        fixed (byte* data = pixels)
        {
            var description = new Texture2DDescription
            {
                Width = (uint)width,
                Height = (uint)height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Immutable,
                BindFlags = BindFlags.ShaderResource,
                CPUAccessFlags = CpuAccessFlags.None
            };
            var initialData = new SubresourceData(data, (uint)(width * 4));
            ID3D11Texture2D texture = device.CreateTexture2D(description, [initialData]);
            ID3D11ShaderResourceView view = device.CreateShaderResourceView(texture);
            return new TextureResource(texture, view);
        }
    }

    void IDisposable.Dispose()
    {
        foreach (TextureResource texture in textures.Values)
            texture.Dispose();
        textures.Clear();
        fallbackTexture.Dispose();
    }

    private sealed class TextureResource(ID3D11Texture2D texture, ID3D11ShaderResourceView view) : IDisposable
    {
        public ID3D11ShaderResourceView View { get; } = view;

        public void Dispose()
        {
            View.Dispose();
            texture.Dispose();
        }
    }
}
