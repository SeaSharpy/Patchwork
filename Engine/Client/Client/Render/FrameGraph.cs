global using Patchwork.Client.Render;
using OpenTK.Graphics.OpenGL4;
namespace Patchwork.Client.Render;

public static class FrameGraph
{
    public class GPUTexture : IDisposable
    {
        public const uint Magic = 0x694267FF;
        private readonly int Handle;
        public readonly long Bindless;
        public readonly uint Width;
        public readonly uint Height;
        public readonly TextureFormat Format;
        public enum TextureFormat : byte
        {
            Albedo,
            Normal,
            MetallicRoughnessAO,
            Emissive,
        }

        public GPUTexture(string path, TextureFormat? format = null)
        {
            FileStream data = DriveMounts.FileStream(path);
            using BinaryReader reader = new(data);

            uint magic = reader.ReadUInt32();
            if (magic != Magic)
            {
                throw new Exception("Invalid texture file");
            }

            Width = reader.ReadUInt32();
            Height = reader.ReadUInt32();
            Format = (TextureFormat)reader.ReadByte();
            if (format != null && format != Format)
            {
                throw new InvalidDataException($"Texture file has format {Format} but was expected to be {format}");
            }
            bool repeat = reader.ReadBoolean();
            bool linear = reader.ReadBoolean();

            long remaining = data.Length - data.Position;
            if (remaining <= 0)
            {
                throw new InvalidDataException("Texture file has no pixel data");
            }

            byte[] pixelData = reader.ReadBytes((int)remaining);
            if (pixelData.Length == 0)
            {
                throw new InvalidDataException("Texture file has no pixel data");
            }

            Handle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, Handle);

            GetTextureFormatGL(
                Format,
                out PixelInternalFormat internalFormat,
                out PixelFormat pixelFormat,
                out PixelType pixelType
            );

            GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                internalFormat,
                (int)Width,
                (int)Height,
                0,
                pixelFormat,
                pixelType,
                pixelData
            );

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, linear ? (int)TextureMinFilter.LinearMipmapLinear : (int)TextureMinFilter.NearestMipmapNearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, linear ? (int)TextureMagFilter.Linear : (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, repeat ? (int)TextureWrapMode.Repeat : (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, repeat ? (int)TextureWrapMode.Repeat : (int)TextureWrapMode.ClampToEdge);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            Bindless = GL.Arb.GetTextureHandle(Handle);
            GL.Arb.MakeTextureHandleResident(Bindless);
        }

        private static void GetTextureFormatGL(
            TextureFormat format,
            out PixelInternalFormat internalFormat,
            out PixelFormat pixelFormat,
            out PixelType pixelType
        )
        {
            switch (format)
            {
                case TextureFormat.Albedo:
                    internalFormat = PixelInternalFormat.Rgba8;
                    pixelFormat = PixelFormat.Rgba;
                    pixelType = PixelType.UnsignedByte;
                    break;
                case TextureFormat.Normal:
                    internalFormat = PixelInternalFormat.Rgb10A2;
                    pixelFormat = PixelFormat.Rgba;
                    pixelType = PixelType.UnsignedInt1010102;
                    break;
                case TextureFormat.MetallicRoughnessAO:
                    internalFormat = PixelInternalFormat.Rgb8;
                    pixelFormat = PixelFormat.Rgb;
                    pixelType = PixelType.UnsignedByte;
                    break;
                case TextureFormat.Emissive:
                    internalFormat = PixelInternalFormat.Rgb16f;
                    pixelFormat = PixelFormat.Rgb;
                    pixelType = PixelType.HalfFloat;
                    break;
                default:
                    throw new NotSupportedException($"Texture format {format} is not mapped yet, or requires compression support");
            }
        }
        public void Dispose()
        {
            GL.Arb.MakeTextureHandleNonResident(Bindless);
            GL.DeleteTexture(Handle);
        }
    }
    public class GPUMesh : IDisposable
    {
        public const uint Magic = 0xFF762496;

        public int Vao;
        public int Vbo;
        public int Ibo;
        public int IndexCount;

        public GPUMesh(BinaryReader reader)
        {
            uint magic = reader.ReadUInt32();
            if (magic != Magic)
            {
                throw new Exception("Invalid model file");
            }

            int vertexCount = (int)reader.ReadUInt32();
            int indexCount = (int)reader.ReadUInt32();

            IndexCount = indexCount;

            const int componentsPerVertex = 16;
            float[] vertices = new float[vertexCount * componentsPerVertex];

            for (int i = 0; i < vertexCount; i++)
            {
                int baseIndex = i * componentsPerVertex;

                vertices[baseIndex + 0] = reader.ReadSingle();
                vertices[baseIndex + 1] = reader.ReadSingle();
                vertices[baseIndex + 2] = reader.ReadSingle();

                vertices[baseIndex + 3] = reader.ReadSingle();
                vertices[baseIndex + 4] = reader.ReadSingle();
                vertices[baseIndex + 5] = reader.ReadSingle();

                vertices[baseIndex + 6] = reader.ReadSingle();
                vertices[baseIndex + 7] = reader.ReadSingle();
                vertices[baseIndex + 8] = reader.ReadSingle();
                vertices[baseIndex + 9] = reader.ReadSingle();

                vertices[baseIndex + 10] = reader.ReadSingle();
                vertices[baseIndex + 11] = reader.ReadSingle();

                vertices[baseIndex + 12] = reader.ReadSingle();
                vertices[baseIndex + 13] = reader.ReadSingle();
                vertices[baseIndex + 14] = reader.ReadSingle();
                vertices[baseIndex + 15] = reader.ReadSingle();
            }

            int[] indices = new int[indexCount];
            for (int i = 0; i < indexCount; i++)
            {
                indices[i] = (int)reader.ReadUInt32();
            }

            Vao = GL.GenVertexArray();
            GL.BindVertexArray(Vao);

            Vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, Vbo);
            GL.BufferData(
                BufferTarget.ArrayBuffer,
                vertices.Length * sizeof(float),
                vertices,
                BufferUsageHint.StaticDraw
            );

            Ibo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, Ibo);
            GL.BufferData(
                BufferTarget.ElementArrayBuffer,
                indices.Length * sizeof(int),
                indices,
                BufferUsageHint.StaticDraw
            );

            int stride = componentsPerVertex * sizeof(float);

            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(
                0,
                3,
                VertexAttribPointerType.Float,
                false,
                stride,
                0
            );

            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(
                1,
                3,
                VertexAttribPointerType.Float,
                false,
                stride,
                3 * sizeof(float)
            );

            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(
                2,
                4,
                VertexAttribPointerType.Float,
                false,
                stride,
                6 * sizeof(float)
            );

            GL.EnableVertexAttribArray(3);
            GL.VertexAttribPointer(
                3,
                2,
                VertexAttribPointerType.Float,
                false,
                stride,
                10 * sizeof(float)
            );

            GL.EnableVertexAttribArray(4);
            GL.VertexAttribPointer(
                4,
                4,
                VertexAttribPointerType.Float,
                false,
                stride,
                12 * sizeof(float)
            );

            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
        }

        public void Dispose()
        {
            if (Vbo != 0)
            {
                GL.DeleteBuffer(Vbo);
                Vbo = 0;
            }

            if (Ibo != 0)
            {
                GL.DeleteBuffer(Ibo);
                Ibo = 0;
            }

            if (Vao != 0)
            {
                GL.DeleteVertexArray(Vao);
                Vao = 0;
            }
        }
    }

    public static Dictionary<string, GPUTexture> Textures = new();
    public static Dictionary<string, GPUMesh> Meshes = new();
    public static List<string> UsedTextures = new();
    public static List<string> UsedMeshes = new();
    public static List<Camera> Cameras = new();
    public readonly record struct BatchSeperator(string Mesh, RenderTexture? Albedo = null, RenderTexture? Emissive = null);
    public static Dictionary<BatchSeperator, List<uint>> Batches = new();
    public static bool Build()
    {
        UsedTextures.Clear();
        UsedMeshes.Clear();
        Cameras.Clear();
        Batches.Clear();
        foreach (Entity entity in Entity.Entities.Values)
        {
            Model model;
            if (entity.Model is Model model_)
            {
                model = model_;
            }
            else continue;
            if (!Meshes.ContainsKey(model.DataPath))
            {
                using BinaryReader reader = model.DataReader;
                Meshes[model.DataPath] = new GPUMesh(reader);
                return false;
            }
            BatchSeperator batchSeperator = new(model.DataPath);
            UsedMeshes.Add(model.DataPath);
            {
                if (model.Albedo is PathTexture pathTexture)
                {
                    if (!Textures.ContainsKey(pathTexture.Path))
                    {
                        Textures[pathTexture.Path] = new GPUTexture(pathTexture.Path, GPUTexture.TextureFormat.Albedo);
                        return false;
                    }
                    UsedTextures.Add(pathTexture.Path);
                }
                else if (model.Albedo is RenderTexture renderTexture)
                {
                    Entity camera = Entity.GetEntity(renderTexture.Camera);
                    if (camera.Camera == null) throw new InvalidDataException("Camera not found.");
                    Cameras.Add(camera.Camera);
                    batchSeperator = batchSeperator with { Albedo = renderTexture };
                }
            }
            {
                if (model.Normal is PathTexture pathTexture)
                {
                    if (!Textures.ContainsKey(pathTexture.Path))
                    {
                        Textures[pathTexture.Path] = new GPUTexture(pathTexture.Path, GPUTexture.TextureFormat.Normal);
                        return false;
                    }
                    UsedTextures.Add(pathTexture.Path);
                }
                else if (model.Normal is RenderTexture renderTexture) throw new InvalidDataException("Render textures are not supported for normal maps.");
            }
            {
                if (model.MetallicRoughnessAO is PathTexture pathTexture)
                {
                    if (!Textures.ContainsKey(pathTexture.Path))
                    {
                        Textures[pathTexture.Path] = new GPUTexture(pathTexture.Path, GPUTexture.TextureFormat.MetallicRoughnessAO);
                        return false;
                    }
                    UsedTextures.Add(pathTexture.Path);
                }
                else if (model.MetallicRoughnessAO is RenderTexture renderTexture) throw new InvalidDataException("Render textures are not supported for metallic-roughness-ao maps.");
            }
            {
                if (model.Emissive is PathTexture pathTexture)
                {
                    if (!Textures.ContainsKey(pathTexture.Path))
                    {
                        Textures[pathTexture.Path] = new GPUTexture(pathTexture.Path, GPUTexture.TextureFormat.Emissive);
                        return false;
                    }
                    UsedTextures.Add(pathTexture.Path);
                }
                else if (model.Emissive is RenderTexture renderTexture)
                {
                    Entity camera = Entity.GetEntity(renderTexture.Camera);
                    if (camera.Camera == null) throw new InvalidDataException("Camera not found.");
                    Cameras.Add(camera.Camera);
                    batchSeperator = batchSeperator with { Emissive = renderTexture };
                }
            }
            if (!Batches.TryGetValue(batchSeperator, out List<uint>? batch))
            {
                batch = new();
                Batches[batchSeperator] = batch;
            }
            batch.Add(entity.ID);
        }
        return true;
    }
}