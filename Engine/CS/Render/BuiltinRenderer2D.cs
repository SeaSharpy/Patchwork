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
        public Vector2 Position;
        public Vector4 Color;
        public float Radius;
        private Vector3 Pad; // keep std430 alignment simple
    }

    private Matrix4 Parallax(Vector3 camera, Entity entity, float depth)
    {
        Vector3 center = entity.Transform.Position;
        Vector3 halfExtents = entity.Transform.Scale * 0.5f;
        Vector3 bottomLeft = center - halfExtents;
        Vector3 topRight = center + halfExtents;
        Vector3 cover = (camera - bottomLeft) / (topRight - bottomLeft);
        cover *= 2;
        cover -= Vector3.One;
        Vector3 scale = Vector3.One * 1f / depth;
        Vector3 realScale = scale * entity.Transform.Scale;
        Vector3 offsetScale = entity.Transform.Scale - realScale;
        return entity.TransformMatrixWith(cover * offsetScale, Vector3.One * 1f / depth);
    }

    private SpriteData GetSpriteData(Sprite sprite)
    {
        return new SpriteData
        {
            Transform = sprite.Parallax != 0
                ? Parallax(CameraEntity.Transform.Position, sprite.Entity, sprite.Depth)
                : sprite.Entity.TransformMatrix,
            Texture = sprite.Texture.BindlessHandle,
            Depth = sprite.Depth
        };
    }

    Resources2D Res = new();

    public void Load()
    {
    }

    public void Render()
    {
        if (!CameraProjection.Ortho) return;

        Res.ResizeIfNeeded(Viewport.Width, Viewport.Height);

        Box box = CameraProjection.Box;
        Sprite[] sprites = ECS.GetComponents<Sprite>().OrderByDescending(s => s.Depth).ToArray();
        SpriteData[] data = sprites.Select(GetSpriteData).ToArray();

        Light[] lights = ECS.GetComponents<Light>()
            .Where(l => l.Box.Contains(box))
            .OrderBy(_ => Random.Shared.NextDouble())
            .ToArray();

        // Per-light sprite lists and projections
        SpriteData[][] perLight = new SpriteData[lights.Length][];
        Matrix4[] lightProjections = new Matrix4[lights.Length];

        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            perLight[i] = sprites.Where(s => s.Box.Contains(light.Box))
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
                Radius = lights[i].Radius
            };
        }
        Res.UpdateLightData(lightData);

        // Light pass into array layers
        int lightCount = Math.Min(lights.Length, Resources2D.LightLayerCount);

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, Res.LightFbo);
        GL.Disable(EnableCap.Blend);

        for (int i = 0; i < lightCount; i++)
        {
            GL.FramebufferTextureLayer(FramebufferTarget.Framebuffer,
                                       FramebufferAttachment.ColorAttachment0,
                                       Res.LightTexArray, 0, i);

            GL.Viewport(0, 0, Resources2D.LightLayerSize, Resources2D.LightLayerSize);
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
        GL.Viewport(Viewport.X, Viewport.Y, Viewport.Width, Viewport.Height);
        GL.ClearColor(1f, 1f, 1f, 1f);
        GL.Clear(ClearBufferMask.ColorBufferBit);

        Res.DepthShader.Use();
        Res.DepthShader.Set("Projection", CameraProjection.Projection);
        Res.DepthShader.Set("View", CameraEntity.TransformMatrix);
        Res.DepthShader.Set("SpriteCount", data.Length);

        Res.UpdateSpriteData(data);
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, Res.SpriteDataSsbo);

        GL.BindVertexArray(Res.QuadVao);
        GL.DrawElementsInstanced(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero, data.Length);

        // Blur depth into DepthTextureB via DepthTextureA
        int groupsX = (Viewport.Width + 16 - 1) / 16;
        int groupsY = (Viewport.Height + 16 - 1) / 16;

        Res.Blur.Use();
        Res.Blur.Set("ImageSize", new Vector2i(Viewport.Width, Viewport.Height));
        Res.Blur.Set("Direction", new Vector2i(1, 0));
        Res.Blur.Set("Radius", 10);
        Res.Blur.Set("Sigma", 10f / 3f);

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
        GL.Viewport(Viewport.X, Viewport.Y, Viewport.Width, Viewport.Height);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        Res.MainShader.Use();
        Res.MainShader.Set("Projection", CameraProjection.Projection);
        Res.MainShader.Set("View", CameraEntity.TransformMatrix);
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
}

public class Sprite : IDataComponent
{
    public TextureBindless Texture;
    public float Depth;
    public float Parallax;
    public bool Transparent;
    public Box Box => new(Entity.Transform.Position.Xy - Entity.Transform.Scale.Xy * 0.5f, Entity.Transform.Position.Xy + Entity.Transform.Scale.Xy * 0.5f);

    public Sprite(TextureBindless texture, float depth = 0, float parallax = 0)
    {
        Texture = texture;
        Depth = depth;
        Parallax = parallax;
    }
}

public class Light : IDataComponent
{
    public Vector4 Color;
    public float Radius;
    public Box Box => new((Entity.Transform.Position - Vector3.One * Radius).Xy, (Entity.Transform.Position + Vector3.One * Radius).Xy);

    public Light(Vector4 color, float radius = 0)
    {
        Color = color;
        Radius = radius;
    }
}
