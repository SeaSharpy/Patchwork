using OpenTK.Graphics.OpenGL4;
using System.Runtime.InteropServices;
using StbImageSharp;

namespace Patchwork.Render.Objects;

public enum TextureType : byte
{
    TextureBindless,
    Texture2D
}
public record TextureBuildOptions(
    TextureType Type,
    PixelInternalFormat InternalFormat = PixelInternalFormat.Rgba8,
    PixelFormat PixelFormat = PixelFormat.Rgba,
    PixelType PixelType = PixelType.UnsignedByte,
    bool GenerateMips = true,
    Action<Texture>? SetParameter = null,
    Action<Texture>[]? SetParameters = null,
    bool CreateFramebuffer = false,
    bool CreateDepth = false,
    bool DepthAsTexture = false,
    RenderbufferStorage DepthRboFormat = RenderbufferStorage.DepthComponent24,
    PixelInternalFormat DepthTexInternal = PixelInternalFormat.DepthComponent24
);
public interface Texture : IDisposable
{
    public TextureType Type { get; }
    public TextureTarget Target { get; }

    public PixelInternalFormat InternalFormat { get; }
    public PixelFormat PixelFormat { get; }
    public PixelType PixelType { get; }

    public IntPtr Data { get; }
    public int Width { get; }
    public int Height { get; }

    public void Bind(int unit);
    public void Parameter(TextureParameterName name, int value);
}
public sealed class Texture2D : Texture, IDisposable
{
    public int Width { get; private set; }
    public int Height { get; private set; }

    public TextureType Type => TextureType.Texture2D;
    public TextureTarget Target => TextureTarget.Texture2D;

    public PixelInternalFormat InternalFormat { get; private set; }
    public PixelFormat PixelFormat { get; private set; }
    public PixelType PixelType { get; private set; }

    int Id;
    bool GenerateMips;

    public IntPtr Data { get; private set; }

    public int FramebufferId { get; private set; }
    public int DepthRenderbufferId { get; private set; }
    public int DepthTextureId { get; private set; }

    public bool HasFramebuffer => FramebufferId != 0;
    public bool HasDepthBuffer => DepthRenderbufferId != 0 || DepthTextureId != 0;

    public Texture2D(
        int width,
        int height,
        IntPtr data,
        PixelInternalFormat internalFormat = PixelInternalFormat.Rgba8,
        PixelFormat pixelFormat = PixelFormat.Rgba,
        PixelType pixelType = PixelType.UnsignedByte,
        bool generateMips = false,
        Action<Texture>? setParameter = null,
        Action<Texture>[]? setParameters = null,
        bool createFramebuffer = false,
        bool createDepth = false,
        bool depthAsTexture = false,
        RenderbufferStorage depthRboFormat = RenderbufferStorage.DepthComponent24,
        PixelInternalFormat depthTexInternal = PixelInternalFormat.DepthComponent24
    )
    {
        Width = width;
        Height = height;
        Data = data;
        InternalFormat = internalFormat;
        PixelFormat = pixelFormat;
        PixelType = pixelType;
        GenerateMips = generateMips;

        Id = GL.GenTexture();
        GL.BindTexture(Target, Id);

        int levels = GenerateMips ? 1 + (int)Math.Floor(Math.Log(Math.Max(Width, Height), 2)) : 1;
        GL.TexStorage2D(TextureTarget2d.Texture2D, levels, (SizedInternalFormat)InternalFormat, Width, Height);

        SetDefaultParams(GenerateMips);

        if (setParameter != null) setParameter(this);
        if (setParameters != null && setParameters.Length > 0)
        {
            foreach (Action<Texture> param in setParameters)
                param(this);
        }

        if (data != IntPtr.Zero)
        {
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            GL.TexSubImage2D(Target, 0, 0, 0, Width, Height, PixelFormat, PixelType, data);
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
        }

        if (GenerateMips)
            GL.GenerateMipmap((GenerateMipmapTarget)Target);

        if (createFramebuffer)
        {
            FramebufferId = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, FramebufferId);

            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer,
                                    FramebufferAttachment.ColorAttachment0,
                                    TextureTarget.Texture2D,
                                    Id, 0);

            if (createDepth)
            {
                if (depthAsTexture)
                {
                    DepthTextureId = GL.GenTexture();
                    GL.BindTexture(TextureTarget.Texture2D, DepthTextureId);

                    PixelInternalFormat depthInternal = depthTexInternal;
                    PixelType depthType = depthInternal == PixelInternalFormat.DepthComponent32f
                        ? PixelType.Float
                        : PixelType.UnsignedInt;

                    int depthLevels = 1;
                    GL.TexStorage2D(TextureTarget2d.Texture2D, depthLevels, (SizedInternalFormat)depthInternal, Width, Height);

                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

                    GL.FramebufferTexture2D(FramebufferTarget.Framebuffer,
                                            FramebufferAttachment.DepthAttachment,
                                            TextureTarget.Texture2D,
                                            DepthTextureId, 0);
                }
                else
                {
                    DepthRenderbufferId = GL.GenRenderbuffer();
                    GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, DepthRenderbufferId);
                    GL.RenderbufferStorage(RenderbufferTarget.Renderbuffer, depthRboFormat, Width, Height);
                    GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer,
                                               FramebufferAttachment.DepthAttachment,
                                               RenderbufferTarget.Renderbuffer,
                                               DepthRenderbufferId);
                }
            }

            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);

            FramebufferErrorCode status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
                throw new InvalidOperationException($"Framebuffer incomplete: {status}");

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        GL.BindTexture(Target, 0);
        TextureFactory.Textures.Add(this);
    }

    public void Parameter(TextureParameterName name, int value)
    {
        if (Id == 0)
            throw new InvalidOperationException("Texture not available.");

        GL.BindTexture(Target, Id);
        GL.TexParameter(Target, name, value);
        GL.BindTexture(Target, 0);
    }

    public void Bind(int unit)
    {
        if (Id == 0)
            throw new InvalidOperationException("Texture not available.");

        GL.ActiveTexture(TextureUnit.Texture0 + unit);
        GL.BindTexture(Target, Id);
    }

    public void BindAsRenderTarget()
    {
        if (!HasFramebuffer)
            throw new InvalidOperationException("No framebuffer was created for this texture.");
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, FramebufferId);
        GL.Viewport(0, 0, Width, Height);
    }

    public static void BindDefaultFramebuffer(int backbufferWidth, int backbufferHeight)
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.Viewport(0, 0, backbufferWidth, backbufferHeight);
    }

    public void Dispose()
    {
        if (DepthTextureId != 0)
        {
            GL.DeleteTexture(DepthTextureId);
            DepthTextureId = 0;
        }

        if (DepthRenderbufferId != 0)
        {
            GL.DeleteRenderbuffer(DepthRenderbufferId);
            DepthRenderbufferId = 0;
        }

        if (FramebufferId != 0)
        {
            GL.DeleteFramebuffer(FramebufferId);
            FramebufferId = 0;
        }

        if (Id != 0)
        {
            GL.DeleteTexture(Id);
            Id = 0;
        }

        GC.SuppressFinalize(this);
    }

    void SetDefaultParams(bool mips)
    {
        GL.TexParameter(Target, TextureParameterName.TextureMinFilter,
            (int)(mips ? TextureMinFilter.LinearMipmapLinear : TextureMinFilter.Linear));
        GL.TexParameter(Target, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(Target, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(Target, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        if (mips)
        {
            int maxLevel = (int)Math.Floor(Math.Log(Math.Max(Width, Height), 2));
            GL.TexParameter(Target, TextureParameterName.TextureMaxLevel, maxLevel);
        }
    }
}
public sealed class TextureBindless : Texture, IDisposable
{
    public int Width { get; private set; }
    public int Height { get; private set; }
    public long BindlessHandle { get; private set; }

    public TextureType Type => TextureType.TextureBindless;
    public TextureTarget Target => TextureTarget.Texture2D;

    public IntPtr Data { get; private set; }

    public PixelInternalFormat InternalFormat { get; private set; }
    public PixelFormat PixelFormat { get; private set; }
    public PixelType PixelType { get; private set; }
    int Id;
    bool GenerateMips;
    bool Generating;

    static bool CheckBindlessSupport()
    {
        string? ext = GL.GetString(StringName.Extensions);
        return ext != null && ext.Contains("GL_ARB_bindless_texture");
    }

    public TextureBindless(
        int width,
        int height,
        IntPtr data,
        PixelInternalFormat internalFormat = PixelInternalFormat.Rgba8,
        PixelFormat pixelFormat = PixelFormat.Rgba,
        PixelType pixelType = PixelType.UnsignedByte,
        bool generateMips = true,
        Action<Texture>? setParameter = null,
        Action<Texture>[]? setParameters = null)
    {
        if (data == IntPtr.Zero)
            throw new ArgumentException("Data must be provided.");

        if (!CheckBindlessSupport())
            throw new NotSupportedException("ARB_bindless_texture not supported by the current context/GPU.");

        Width = width;
        Height = height;
        Data = data;
        InternalFormat = internalFormat;
        PixelFormat = pixelFormat;
        PixelType = pixelType;
        GenerateMips = generateMips;

        Id = GL.GenTexture();
        GL.BindTexture(Target, Id);

        int levels = GenerateMips ? 1 + (int)Math.Floor(Math.Log(Math.Max(Width, Height), 2)) : 1;
        GL.TexStorage2D(TextureTarget2d.Texture2D, levels, (SizedInternalFormat)InternalFormat, Width, Height);

        SetDefaultParams(GenerateMips);
        Generating = true;
        if (setParameter != null)
            setParameter(this);
        if (setParameters != null && setParameters.Length > 0)
        {
            foreach (Action<Texture> param in setParameters)
                param(this);
        }
        Generating = false;

        GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        GL.TexSubImage2D(Target, 0, 0, 0, Width, Height, PixelFormat, PixelType, data);
        GL.PixelStore(PixelStoreParameter.UnpackAlignment, 4);

        if (GenerateMips)
            GL.GenerateMipmap((GenerateMipmapTarget)Target);

        GL.BindTexture(Target, 0);

        BindlessHandle = GL.Arb.GetTextureHandle(Id);
        GL.Arb.MakeTextureHandleResident(BindlessHandle);
        TextureFactory.Textures.Add(this);
    }

    public void Parameter(TextureParameterName name, int value)
    {
        if (Id == 0)
            throw new InvalidOperationException("Texture not available.");
        if (!Generating)
            throw new InvalidOperationException("Texture is already generated.");
        GL.TexParameter(Target, name, value);
    }

    public void Bind(int unit)
    {
        if (Id == 0)
            throw new InvalidOperationException("Texture not available.");

        GL.ActiveTexture(TextureUnit.Texture0 + unit);
        GL.BindTexture(Target, Id);
    }

    public void Dispose()
    {
        if (BindlessHandle != 0 && GL.Arb.IsTextureHandleResident(BindlessHandle))
            GL.Arb.MakeTextureHandleNonResident(BindlessHandle);

        BindlessHandle = 0;

        if (Id != 0)
        {
            GL.DeleteTexture(Id);
            Id = 0;
        }

        GC.SuppressFinalize(this);
    }

    void SetDefaultParams(bool mips)
    {
        GL.TexParameter(Target, TextureParameterName.TextureMinFilter, (int)(mips ? TextureMinFilter.LinearMipmapLinear : TextureMinFilter.Linear));
        GL.TexParameter(Target, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexParameter(Target, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(Target, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        if (mips)
        {
            int maxLevel = (int)Math.Floor(Math.Log(Math.Max(Width, Height), 2));
            GL.TexParameter(Target, TextureParameterName.TextureMaxLevel, maxLevel);
        }
    }
}

public static class TextureParamDefaults
{
    public static Action<Texture> Linear = (tex) =>
    {
        tex.Parameter(TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        tex.Parameter(TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
    };

    public static Action<Texture> LinearMipmap = (tex) =>
    {
        tex.Parameter(TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        tex.Parameter(TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
    };

    public static Action<Texture> Nearest = (tex) =>
    {
        tex.Parameter(TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        tex.Parameter(TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
    };

    public static Action<Texture> NearestMipmap = (tex) =>
    {
        tex.Parameter(TextureParameterName.TextureMinFilter, (int)TextureMinFilter.NearestMipmapLinear);
        tex.Parameter(TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
    };

    public static Action<Texture> Clamp = (tex) =>
    {
        tex.Parameter(TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        tex.Parameter(TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
    };

    public static Action<Texture> Repeat = (tex) =>
    {
        tex.Parameter(TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        tex.Parameter(TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
    };

    public static Action<Texture> Mirror = (tex) =>
    {
        tex.Parameter(TextureParameterName.TextureWrapS, (int)TextureWrapMode.MirroredRepeat);
        tex.Parameter(TextureParameterName.TextureWrapT, (int)TextureWrapMode.MirroredRepeat);
    };

    public static Action<Texture> Border = (tex) =>
    {
        tex.Parameter(TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToBorder);
        tex.Parameter(TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToBorder);
    };
}


public static class TextureFactory
{
    public static List<Texture> Textures = [];
    static TextureFactory()
    {
        StbImage.stbi_set_flip_vertically_on_load(1);
    }
    public static Texture BuildFromImage(string path, TextureBuildOptions options)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Image path is null or empty.", nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException("Image file not found.", path);

        using FileStream fs = File.OpenRead(path);
        Texture result = BuildFromStream(fs, options);
        fs.Dispose();
        return result;
    }

    public static Texture BuildFromStream(Stream stream, TextureBuildOptions options)
    {

        ImageResult image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        bool sourceHasAlpha =
        image.SourceComp == ColorComponents.RedGreenBlueAlpha ||
        image.SourceComp == ColorComponents.GreyAlpha;

        if (!sourceHasAlpha)
            for (int i = 3; i < image.Data.Length; i += 4)
                image.Data[i] = 255;

        int width = image.Width;
        int height = image.Height;
        byte[] pixels = image.Data;

        IntPtr unmanagedPtr = Marshal.AllocHGlobal(pixels.Length);
        try
        {
            Marshal.Copy(pixels, 0, unmanagedPtr, pixels.Length);
            switch (options.Type)
            {
                case TextureType.Texture2D:
                    return new Texture2D(
                        width,
                        height,
                        unmanagedPtr,
                        PixelInternalFormat.Rgba8,
                        PixelFormat.Rgba,
                        PixelType.UnsignedByte,
                        options.GenerateMips,
                        options.SetParameter,
                        options.SetParameters,
                        options.CreateFramebuffer,
                        options.CreateDepth,
                        options.DepthAsTexture,
                        options.DepthRboFormat,
                        options.DepthTexInternal
                    );

                case TextureType.TextureBindless:
                    return new TextureBindless(
                        width,
                        height,
                        unmanagedPtr,
                        PixelInternalFormat.Rgba8,
                        PixelFormat.Rgba,
                        PixelType.UnsignedByte,
                        options.GenerateMips,
                        options.SetParameter,
                        options.SetParameters
                    );

                default:
                    throw new NotSupportedException($"Unsupported TextureType: {options.Type}");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(unmanagedPtr);
        }
    }

    public static void DisposeAll()
    {
        foreach (Texture texture in Textures)
            texture.Dispose();
        Textures.Clear();
    }
}
