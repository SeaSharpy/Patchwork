using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace Patchwork.Render;

public class BuiltinRenderer2D : IRenderSystem
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SpriteData
    {
        public Matrix4 Transform;
        public long Texture;
        public float Depth;
        public float Extra;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LightData
    {
        public Matrix4 Matrix;
        public Vector4 Color;
        public Vector2 Position;
        public float Radius;
        private float Extra;
    }


    private SpriteData GetSpriteData(Sprite sprite)
    {
        return new SpriteData
        {
            Transform = sprite.Entity.TransformMatrix,
            Texture = sprite.Texture.BindlessHandle,
            Depth = sprite.Depth
        };
    }

    Resources2D Res = new();

    public BuiltinRenderer2D(int shadowResolution = 1024, int maxShadows = 16, float shadowSharpness = 25f, float aoStrength = 2.5f, float lightingDepthStrength = 2f, Vector3? aoColor = null)
    {
        ShadowSharpness = shadowSharpness;
        AOStrength = aoStrength;
        LightingDepthStrength = lightingDepthStrength;
        AOColor = aoColor ?? new Vector3(0.05f);
        Res = new(shadowResolution, maxShadows);
    }
    public void Load()
    {
    }

    public void Render()
    {
        if (!CameraProjection.Ortho) return;

        Res.ResizeIfNeeded((int)Viewport.Width, (int)Viewport.Height);

        Box box = CameraProjection.Box;
        Sprite[] sprites = ECS.GetComponents<Sprite>().OrderByDescending(s => s.Depth).ToArray();
        SpriteData[] data = sprites.Select(GetSpriteData).ToArray();

        Light[] lights = ECS.GetComponents<Light>()
            .Where(l => l.Box.Intersects(box))
            .OrderBy(_ => Random.Shared.NextDouble())
            .ToArray();

        // Per-light sprite lists and projections
        SpriteData[][] perLight = new SpriteData[lights.Length][];
        Matrix4[] lightProjections = new Matrix4[lights.Length];

        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            perLight[i] = sprites.Where(s => s.Box.Intersects(light.Box))
                                 .Where(s => s.Depth == 0)
                                 .Select(GetSpriteData).ToArray();
            lightProjections[i] = light.Box.ToOrthoMatrix();
        }

        // Build and upload LightData SSBO (binding = 1)
        LightData[] lightData = new LightData[lights.Length];
        for (int i = 0; i < lights.Length; i++)
        {
            Vector2 pos = lights[i].Entity.Transform.Position.Xy;
            lightData[i] = new LightData
            {
                Position = pos,
                Color = lights[i].Color,
                Radius = lights[i].Radius,
                Matrix = lights[i].Box.ToOrthoMatrix()
            };
        }
        Res.UpdateLightData(lightData);

        // Light pass into array layers
        int lightCount = Math.Min(lights.Length, Res.LightLayerCount);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, Res.LightFbo);
        GL.Disable(EnableCap.Blend);

        for (int i = 0; i < lightCount; i++)
        {
            GL.FramebufferTextureLayer(FramebufferTarget.Framebuffer,
                                       FramebufferAttachment.ColorAttachment0,
                                       Res.LightTexArray, 0, i);

            GL.Viewport(0, 0, Res.LightLayerSize, Res.LightLayerSize);
            GL.ClearColor(0f, 0f, 0f, 0f);
            GL.Clear(ClearBufferMask.ColorBufferBit);

            SpriteData[] lightSprites = perLight[i];
            Res.UpdateSpriteData(lightSprites);

            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, Res.SpriteDataSsbo);

            Res.LightShader.Use();
            Res.LightShader.Set("Projection", lightProjections[i]);
            Res.LightShader.Set("SpriteCount", lightSprites.Length);

            GL.BindVertexArray(Res.QuadVao);
            GL.DrawElementsInstanced(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero, lightSprites.Length);
        }

        // Generate mipmaps for the complete light array after rendering all layers
        GL.BindTexture(TextureTarget.Texture2DArray, Res.LightTexArray);
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2DArray);
        GL.BindTexture(TextureTarget.Texture2DArray, 0);

        // Build depth (scene) to Res.DepthTexture
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, Res.DepthFbo);
        GL.Viewport((int)Viewport.X, (int)Viewport.Y, (int)Viewport.Width, (int)Viewport.Height);
        GL.ClearColor(1f, 1f, 1f, 1f);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        Res.DepthShader.Use();
        Res.DepthShader.Set("Projection", CameraProjection.Projection);
        Res.DepthShader.Set("SpriteCount", data.Length);

        Res.UpdateSpriteData(data);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, Res.SpriteDataSsbo);

        GL.BindVertexArray(Res.QuadVao);
        GL.DrawElementsInstanced(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero, data.Length);

        // Blur depth into DepthTextureB via DepthTextureA
        int groupsX = ((int)Viewport.Width + 16 - 1) / 16;
        int groupsY = ((int)Viewport.Height + 16 - 1) / 16;

        Res.Blur.Use();
        Res.Blur.Set("ImageSize", new Vector2i((int)Viewport.Width, (int)Viewport.Height));
        Res.Blur.Set("Direction", new Vector2i(1, 0));
        Res.Blur.Set("Radius", 25);
        Res.Blur.Set("Sigma", 25f / 3f);

        GL.BindImageTexture(0, Res.DepthTexture, 0, false, 0, TextureAccess.ReadOnly, SizedInternalFormat.R32f);
        GL.BindImageTexture(1, Res.DepthTextureA, 0, false, 0, TextureAccess.WriteOnly, SizedInternalFormat.R32f);

        GL.DispatchCompute(groupsX, groupsY, 1);
        GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit);

        Res.Blur.Set("Direction", new Vector2i(0, 1));

        GL.BindImageTexture(0, Res.DepthTextureA, 0, false, 0, TextureAccess.ReadOnly, SizedInternalFormat.R32f);
        GL.BindImageTexture(1, Res.DepthTextureB, 0, false, 0, TextureAccess.WriteOnly, SizedInternalFormat.R32f);

        GL.DispatchCompute(groupsX, groupsY, 1);
        GL.MemoryBarrier(MemoryBarrierFlags.ShaderImageAccessBarrierBit);

        // Composite in main pass: provide light array (with mips), raw depth, and blurred depth
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        GL.Viewport((int)Viewport.X, (int)Viewport.Y, (int)Viewport.Width, (int)Viewport.Height);
        GL.ClearColor(0.5f, 0.55f, 0.6f, 1f);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        Res.MainShader.Use();
        Res.MainShader.Set("Projection", CameraProjection.Projection);
        Res.MainShader.Set("ViewportSize", new Vector2(Viewport.Width, Viewport.Height));
        Res.MainShader.Set("SpriteCount", data.Length);
        Res.MainShader.Set("LightCount", lightCount);

        // SSBOs: sprites at binding 0, lights at binding 1
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, Res.SpriteDataSsbo);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, Res.LightDataSsbo);

        // Samplers: array + depth textures
        // uLightTexArray -> unit 0, uDepthTex -> unit 1, uDepthBlurTex -> unit 2
        Res.MainShader.Set("LightTexArray", 0);
        Res.MainShader.Set("DepthTex", 1);
        Res.MainShader.Set("DepthBlurTex", 2);
        Res.MainShader.Set("MaxLightMip", (int)Math.Floor(Math.Log(Res.LightLayerSize, 2)));
        Res.MainShader.Set("ShadowSoftness", 1f / ShadowSharpness);
        Res.MainShader.Set("AOStrength", 2.5f);
        Res.MainShader.Set("LightingDepthStrength", 3f);
        Res.MainShader.Set("AOColor", new Vector3(0.05f));

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2DArray, Res.LightTexArray);

        GL.ActiveTexture(TextureUnit.Texture1);
        GL.BindTexture(TextureTarget.Texture2D, Res.DepthTexture);

        GL.ActiveTexture(TextureUnit.Texture2);
        GL.BindTexture(TextureTarget.Texture2D, Res.DepthTextureB);

        // Draw sprites again or a fullscreen pass, depending on how your MainShader composites.
        // If MainShader expects per-sprite instancing, keep this:
        GL.BindVertexArray(Res.QuadVao);
        GL.DrawElementsInstanced(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero, data.Length);

        // If instead MainShader is fullscreen, replace with a single non-instanced draw.
    }

    public void Dispose()
    {
        Res.Dispose();
    }
    public float ShadowSharpness = 25f;
    public float AOStrength = 2.5f;
    public float LightingDepthStrength = 2f;
    public Vector3 AOColor = new(0.05f);
    public ShadowMode ShadowMode = ShadowMode.Hard;
}

public enum ShadowMode : byte {
    VolumetricSoft,
    SimpleSoft,
    HardDark,
    Hard
}

public class Sprite : IDataComponent
{
    public TextureBindless Texture;
    public float Depth;
    public bool Transparent;
    public Box Box
    {
        get
        {
            Vector2 size = Entity.Transform.Scale.Xy;
            Vector2 min = Entity.Transform.Position.Xy - size * 0.5f;
            return new(min, size);
        }
    }

    public Sprite(TextureBindless texture, float depth = 0)
    {
        Texture = texture;
        Depth = depth;
    }
}

public class Light : IDataComponent
{
    public Vector4 Color;
    public float Radius;
    public Box Box => new((Entity.Transform.Position - Vector3.One * Radius).Xy, Vector2.One * Radius * 2);

    public Light(Vector4 color, float radius = 0)
    {
        Color = color;
        Radius = radius;
    }
}
