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
                    pixelType = PixelType.UnsignedByte;
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
        ~GPUTexture()
        {
            Dispose();
        }
    }
    public struct GPUMesh
    {
        public GPUMesh(string path)
        {

        }
    }
    public static Dictionary<string, GPUTexture> Textures = new();
    public static Dictionary<string, GPUMesh> Meshes = new();
    public static bool Build()
    {
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
                Meshes[model.DataPath] = new GPUMesh(model.DataPath);
                return false;
            }
            {
                if (model.Albedo is PathTexture pathTexture)
                    if (!Textures.ContainsKey(pathTexture.Path))
                    {
                        Textures[pathTexture.Path] = new GPUTexture(pathTexture.Path);
                        return false;
                    }
            }
            {
                if (model.Normal is PathTexture pathTexture)
                    if (!Textures.ContainsKey(pathTexture.Path))
                    {
                        Textures[pathTexture.Path] = new GPUTexture(pathTexture.Path);
                        return false;
                    }
            }
            {
                if (model.MetallicRoughnessAO is PathTexture pathTexture)
                    if (!Textures.ContainsKey(pathTexture.Path))
                    {
                        Textures[pathTexture.Path] = new GPUTexture(pathTexture.Path);
                        return false;
                    }
            }
            {
                if (model.Emissive is PathTexture pathTexture)
                    if (!Textures.ContainsKey(pathTexture.Path))
                    {
                        Textures[pathTexture.Path] = new GPUTexture(pathTexture.Path);
                        return false;
                    }
            }
        }
        return true;
    }
}