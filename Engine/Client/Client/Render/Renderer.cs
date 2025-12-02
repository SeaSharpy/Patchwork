using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using static Patchwork.Client.Render.FrameGraph;
namespace Patchwork.Client.Render;

public static partial class Renderer
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Instance
    {
        public Matrix4 Transform;
        public Matrix3 NormalTransform;
        public TKVector4 AlbedoColor;
        public TKVector4 EmissiveColor;
        public TKVector4 MetallicRoughnessAOColor;
        public long Albedo;
        public long Emissive;
        public long MetallicRoughnessAO;
        public long Normal;
        public uint ID;
    }
    private static unsafe int InstanceSize = sizeof(Instance);
    private static int InstanceSSBO = GL.GenBuffer();
    private static Matrix3 NormalMatrix(Matrix4 transform)
    {
        return new Matrix3(transform.Row0.Xyz, transform.Row1.Xyz, transform.Row2.Xyz).Inverted().Transposed();
    }
    public static float DegreesToRadians(float degrees)
    {
        return degrees * (MathF.PI / 180f);
    }
    public static void Render(TKVector2 size)
    {
        GL.ClearColor(0, 0, 0.2f, 1);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        Entity camera = Entity.GetEntity(Camera);
        WriteLine($"Rendering camera {camera.Rotation} at {camera.Position}.");
        Matrix4 view = camera.Transform;
        Matrix4 projection = Matrix4.CreatePerspectiveFieldOfView(DegreesToRadians(FOV), size.X / size.Y, 0.1f, 1000);
        Matrix4 viewProjection = view * projection;
        Matrix4 invViewProjection = viewProjection.Inverted();
        SkyboxShader.Use();
        SkyboxTexture.Bind(0);
        GL.Uniform1(SkyboxShader.Uniform("Skybox"), 0);
        GL.UniformMatrix4(SkyboxShader.Uniform("ViewProjection"), false, ref viewProjection);
        GL.UniformMatrix4(SkyboxShader.Uniform("InvViewProjection"), false, ref invViewProjection);
        FullscreenQuad.Draw();
        foreach (KeyValuePair<BatchSeperator, List<BatchItem>> batch in Batches)
        {
            if (batch.Key.Albedo != null || batch.Key.Emissive != null)
            {
                throw new NotImplementedException();
            }
            else
            {
                MainShader.Use();
                GL.UniformMatrix4(MainShader.Uniform("ViewProjection"), false, ref viewProjection);
                GL.UniformMatrix4(MainShader.Uniform("InvViewProjection"), false, ref invViewProjection);
                GPUMesh mesh = Meshes[batch.Key.Mesh];
                Instance[] data = new Instance[batch.Value.Count];
                for (int i = 0; i < batch.Value.Count; i++)
                {
                    BatchItem entity = batch.Value[i];
                    data[i] = new Instance
                    {
                        Transform = entity.Transform,
                        NormalTransform = NormalMatrix(entity.Transform),
                        AlbedoColor = entity.AlbedoColor,
                        EmissiveColor = entity.EmissiveColor,
                        MetallicRoughnessAOColor = entity.MetallicRoughnessAOColor,
                        Albedo = entity.Albedo,
                        Emissive = entity.Emissive,
                        MetallicRoughnessAO = entity.MetallicRoughnessAO,
                        Normal = entity.Normal,
                        ID = entity.ID
                    };
                }
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, InstanceSSBO);
                GL.BufferData(BufferTarget.ShaderStorageBuffer, data.Length * InstanceSize, data, BufferUsageHint.DynamicDraw);
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, InstanceSSBO);
                GL.BindVertexArray(mesh.Vao);
                GL.DrawElementsInstanced(PrimitiveType.Triangles, mesh.IndexCount, DrawElementsType.UnsignedInt, 0, data.Length);
            }
        }
    }
    public static void Dispose()
    {
        FullscreenQuad.Dispose();
        DisposeShaders();
    }
}