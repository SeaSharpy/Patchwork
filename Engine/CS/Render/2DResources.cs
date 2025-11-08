using System;
using OpenTK.Graphics.OpenGL4;

namespace Patchwork.Render;

public class Resources2D : IDisposable
{
    public int DepthTexture;     // existing
    public int DepthTextureA;    // new extra depth
    public int DepthTextureB;    // new extra depth
    public int DepthFbo;

    public int LightTexArray = GL.GenTexture();   // 2D array, 32 layers
    public int LightFbo = GL.GenFramebuffer();    // Single FBO bound to the whole array

    public int SpriteDataSsbo = GL.GenBuffer();   // Sprite data buffer
    public int LightDataSsbo = GL.GenBuffer();

    public const int LightLayerSize = 1024;
    public const int LightLayerCount = 16;

    private int ScreenWidth;
    private int ScreenHeight;
    private int SpriteDataCapacity;
    private int LightDataCapacity;

    public Shader DepthShader = Shader.Embedded("depth2d");
    public Shader MainShader = Shader.Embedded("main2d");
    public Shader LightShader = Shader.Embedded("light2d");

    public int QuadVao;
    public int QuadVbo;
    public int QuadEbo;

    public Resources2D()
    {
        // Create 2D texture array for lights
        GL.BindTexture(TextureTarget.Texture2DArray, LightTexArray);
        GL.TexImage3D(TextureTarget.Texture2DArray, 0, PixelInternalFormat.R16,
                      LightLayerSize, LightLayerSize, LightLayerCount,
                      0, PixelFormat.Red, PixelType.UnsignedShort, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.NearestMipmapNearest);
        GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapR, (int)TextureWrapMode.ClampToEdge);
        GL.BindTexture(TextureTarget.Texture2DArray, 0);

        // Framebuffer for light rendering
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, LightFbo);
        GL.FramebufferTexture(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, LightTexArray, 0);
        GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

        // Quad geometry setup
        float[] quadVerts =
        {
            -0.5f, -0.5f,  0f, 0f,
             0.5f, -0.5f,  1f, 0f,
             0.5f,  0.5f,  1f, 1f,
            -0.5f,  0.5f,  0f, 1f
        };

        uint[] quadIdx = { 0, 1, 2, 0, 2, 3 };

        QuadVao = GL.GenVertexArray();
        QuadVbo = GL.GenBuffer();
        QuadEbo = GL.GenBuffer();

        GL.BindVertexArray(QuadVao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, QuadVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, quadVerts.Length * sizeof(float), quadVerts, BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, QuadEbo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, quadIdx.Length * sizeof(uint), quadIdx, BufferUsageHint.StaticDraw);

        int stride = 4 * sizeof(float);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);

        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 2 * sizeof(float));

        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
    }
    public Shader Blur = Shader.Compute(@"
layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

// Match the actual texture format: R32F
layout(binding = 0, r32f)  readonly  uniform image2D Src;
layout(binding = 1, r32f)  writeonly uniform image2D Dst;

uniform ivec2 ImageSize;   // (width, height)
uniform ivec2 Direction;   // (1,0) for horizontal, (0,1) for vertical)
uniform int   Radius;      // blur radius
uniform float Sigma;       // Gaussian sigma; if <= 0, box weights are used

void main()
{
    ivec2 gid = ivec2(gl_GlobalInvocationID.xy);
    if (any(greaterThanEqual(gid, ImageSize))) return;

    float accum = 0.0;
    float wsum  = 0.0;

    for (int o = -Radius; o <= Radius; ++o)
    {
        float w = (Sigma > 0.0) ? exp(-0.5 * float(o * o) / (Sigma * Sigma)) : 1.0;
        ivec2 p = clamp(gid + o * Direction, ivec2(0), ImageSize - 1);
        float c = imageLoad(Src, p).r;  // single channel
        accum += c * w;
        wsum  += w;
    }

    float outValue = accum / max(wsum, 1e-8);
    imageStore(Dst, gid, vec4(outValue)); // only .r is used for r32f
}
", "blur");
    public void ResizeIfNeeded(int width, int height)
    {
        if (width == ScreenWidth && height == ScreenHeight)
            return;

        ScreenWidth = width;
        ScreenHeight = height;

        DeleteScreenSized();

        DepthTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, DepthTexture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R32f,
                      width, height, 0, PixelFormat.Red, PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.BindTexture(TextureTarget.Texture2D, 0);

        // Extra depth texture A (same params)
        DepthTextureA = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, DepthTextureA);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R32f,
                      width, height, 0, PixelFormat.Red, PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.BindTexture(TextureTarget.Texture2D, 0);

        // Extra depth texture B (same params)
        DepthTextureB = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, DepthTextureB);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.R32f,
                      width, height, 0, PixelFormat.Red, PixelType.Float, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.BindTexture(TextureTarget.Texture2D, 0);

        // Keep your existing DepthFbo using DepthTexture as-is
        DepthFbo = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, DepthFbo);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, DepthTexture, 0);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }
    public void EnsureSpriteDataCapacity(int byteSize)
    {
        if (byteSize <= SpriteDataCapacity)
            return;

        SpriteDataCapacity = NextPow2(byteSize);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, SpriteDataSsbo);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, SpriteDataCapacity, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
    }

    public void EnsureLightDataCapacity(int byteSize)
    {
        if (byteSize <= LightDataCapacity)
            return;

        LightDataCapacity = NextPow2(byteSize);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, LightDataSsbo);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, LightDataCapacity, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
    }

    public void UpdateSpriteData<T>(T[] data) where T : unmanaged
    {
        int size = data.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>();
        EnsureSpriteDataCapacity(size);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, SpriteDataSsbo);
        GL.BufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, size, data);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
    }

    public void UpdateLightData<T>(T[] data) where T : unmanaged
    {
        int size = data.Length * System.Runtime.InteropServices.Marshal.SizeOf<T>();
        EnsureLightDataCapacity(size);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, LightDataSsbo);
        GL.BufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, size, data);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
    }

    public void Dispose()
    {
        DeleteScreenSized();

        if (SpriteDataSsbo != 0)
        {
            GL.DeleteBuffer(SpriteDataSsbo);
            SpriteDataSsbo = 0;
        }

        if (QuadEbo != 0)
        {
            GL.DeleteBuffer(QuadEbo);
            QuadEbo = 0;
        }
        if (QuadVbo != 0)
        {
            GL.DeleteBuffer(QuadVbo);
            QuadVbo = 0;
        }
        if (QuadVao != 0)
        {
            GL.DeleteVertexArray(QuadVao);
            QuadVao = 0;
        }

        if (LightFbo != 0)
        {
            GL.DeleteFramebuffer(LightFbo);
            LightFbo = 0;
        }

        if (LightTexArray != 0)
        {
            GL.DeleteTexture(LightTexArray);
            LightTexArray = 0;
        }
    }

    private void DeleteScreenSized()
    {
        if (DepthFbo != 0)
        {
            GL.DeleteFramebuffer(DepthFbo);
            DepthFbo = 0;
        }
        if (DepthTexture != 0)
        {
            GL.DeleteTexture(DepthTexture);
            DepthTexture = 0;
        }
        if (DepthTextureA != 0)
        {
            GL.DeleteTexture(DepthTextureA);
            DepthTextureA = 0;
        }
        if (DepthTextureB != 0)
        {
            GL.DeleteTexture(DepthTextureB);
            DepthTextureB = 0;
        }
    }

    private static int NextPow2(int v)
    {
        v--;
        v |= v >> 1;
        v |= v >> 2;
        v |= v >> 4;
        v |= v >> 8;
        v |= v >> 16;
        v++;
        return v;
    }
}
