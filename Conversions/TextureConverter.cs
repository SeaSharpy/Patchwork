using System;
using System.IO;
using System.Buffers.Binary;
using System.Collections.Generic;
using StbImageSharp;

public static class TextureConverter
{

    static TextureConverter()
    {
        StbImage.stbi_set_flip_vertically_on_load(1);
    }
    private const uint Magic = 0x694267FF;

    private enum TextureFormat : byte
    {
        Albedo,
        Normal,
        MetallicRoughnessAO,
        Emissive,
    }

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg",
        ".tga",
        ".bmp"
    };

    public static void ConvertDirectory(string root)
    {
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(root);
        }

        foreach (string file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(file);
            if (!ImageExtensions.Contains(extension))
            {
                continue;
            }

            string fileName = Path.GetFileName(file);

            if (!TryGetFormatFromPrefix(fileName, out TextureFormat format))
            {
                // Unknown prefix - skip quietly
                continue;
            }

            string outputPath = Path.ChangeExtension(file, ".pwtex");

            try
            {
                ConvertSingle(file, outputPath, format);
                Console.WriteLine($"Converted: {file} -> {outputPath} ({format})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to convert {file}: {ex.Message}");
            }
        }
    }

    private static bool TryGetFormatFromPrefix(string fileName, out TextureFormat format)
    {
        if (fileName.Length < 2 || fileName[1] != '_')
        {
            format = default;
            return false;
        }

        char prefix = char.ToLowerInvariant(fileName[0]);

        switch (prefix)
        {
            case 'a':
                format = TextureFormat.Albedo;
                return true;
            case 'n':
                format = TextureFormat.Normal;
                return true;
            case 'm':
                format = TextureFormat.MetallicRoughnessAO;
                return true;
            case 'e':
                format = TextureFormat.Emissive;
                return true;
            default:
                format = default;
                return false;
        }
    }
    private static bool SourceHadAlpha(ImageResult image)
    {
        return image.SourceComp == ColorComponents.RedGreenBlueAlpha
            || image.SourceComp == ColorComponents.GreyAlpha;
    }

    private static void ConvertSingle(string inputPath, string outputPath, TextureFormat format)
    {
        using FileStream stream = File.OpenRead(inputPath);

        ImageResult image;
        switch (format)
        {
            case TextureFormat.Albedo:
                image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
                break;
            case TextureFormat.Normal:
            case TextureFormat.MetallicRoughnessAO:
            case TextureFormat.Emissive:
                image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlue);
                break;
            default:
                throw new NotSupportedException($"Unsupported format {format}");
        }

        uint width = (uint)image.Width;
        uint height = (uint)image.Height;

        bool repeat = true;
        bool linear = true;

        byte[] pixelData;

        switch (format)
        {
            case TextureFormat.Albedo:
                {
                    pixelData = image.Data;
                    if (!SourceHadAlpha(image))
                        for (int i = 0; i < pixelData.Length; i += 4)
                            pixelData[i + 3] = byte.MaxValue;
                    break;
                }
            case TextureFormat.Normal:
                {
                    pixelData = image.Data;
                    break;
                }
            case TextureFormat.MetallicRoughnessAO:
                {
                    pixelData = image.Data;
                    break;
                }
            case TextureFormat.Emissive:
                {
                    pixelData = new byte[image.Width * image.Height * 6];
                    for (int i = 0; i < image.Data.Length; i += 3)
                    {
                        float r = image.Data[i + 0] / 255.0f;
                        float g = image.Data[i + 1] / 255.0f;
                        float b = image.Data[i + 2] / 255.0f;
                        int j = i * 2;
                        WriteHalf(pixelData, i + 0, r);
                        WriteHalf(pixelData, i + 2, g);
                        WriteHalf(pixelData, i + 4, b);
                    }
                    break;
                }
            default:
                throw new NotSupportedException($"Unsupported format {format}");
        }

        using FileStream outStream = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        using BinaryWriter writer = new(outStream);

        writer.Write(Magic);
        writer.Write(width);
        writer.Write(height);
        writer.Write((byte)format);
        writer.Write(repeat);
        writer.Write(linear);
        writer.Write(pixelData);
    }
    private static void WriteHalf(byte[] buffer, int index, float value)
    {
        Half half = (Half)value;
        short bits = BitConverter.HalfToInt16Bits(half);
        buffer[index + 0] = (byte)(bits & 0xFF);
        buffer[index + 1] = (byte)((bits >> 8) & 0xFF);
    }
}
