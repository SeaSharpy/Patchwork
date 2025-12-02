using System;
using System.Collections.Generic;
using System.IO;
using Assimp;

public static class ModelConverter
{
    private const uint Magic = 0xFF762496;
    private static readonly HashSet<string> Extensions;
    private static readonly AssimpContext Context = new();

    static ModelConverter()
    {
        Extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string ext in Context.GetSupportedImportFormats())
        {
            if (string.IsNullOrWhiteSpace(ext))
            {
                continue;
            }

            string normalized = ext.StartsWith(".") ? ext : "." + ext;
            Extensions.Add(normalized);
        }
    }

    public static void ConvertDirectory(string root)
    {
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(root);
        }

        foreach (string file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(file);
            if (string.IsNullOrEmpty(extension) || !Extensions.Contains(extension))
            {
                continue;
            }

            string outputPath = Path.ChangeExtension(file, ".pwmdl");

            try
            {
                ConvertSingle(file, outputPath);
                Console.WriteLine($"Converted: {file} -> {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to convert {file}: {ex.Message}");
            }
        }
    }
    private static void ConvertSingle(string inputPath, string outputPath)
    {
        Scene scene = Context.ImportFile(
            inputPath,
            PostProcessSteps.Triangulate |
            PostProcessSteps.JoinIdenticalVertices |
            PostProcessSteps.GenerateSmoothNormals |
            PostProcessSteps.CalculateTangentSpace |
            PostProcessSteps.ImproveCacheLocality |
            PostProcessSteps.SortByPrimitiveType
        );

        if (scene == null || !scene.HasMeshes)
        {
            throw new Exception($"No meshes found in {inputPath}");
        }

        const int componentsPerVertex = 16;

        uint totalVertexCount = 0;
        uint totalIndexCount = 0;

        foreach (Mesh mesh in scene.Meshes)
        {
            if (!mesh.HasVertices)
            {
                continue;
            }

            totalVertexCount += (uint)mesh.VertexCount;

            foreach (Face face in mesh.Faces)
            {
                if (face.IndexCount == 3)
                {
                    totalIndexCount += 3;
                }
            }
        }

        if (totalVertexCount == 0)
        {
            throw new Exception($"All meshes in {inputPath} have no vertices");
        }

        List<uint> indices = new((int)totalIndexCount);
        float[] vertexData = new float[totalVertexCount * componentsPerVertex];

        uint vertexOffset = 0;

        foreach (Mesh mesh in scene.Meshes)
        {
            if (!mesh.HasVertices)
            {
                continue;
            }

            bool hasNormals = mesh.HasNormals;
            bool hasTangents = mesh.HasTangentBasis;
            bool hasUV0 = mesh.TextureCoordinateChannelCount > 0 && mesh.HasTextureCoords(0);
            bool hasColor0 = mesh.VertexColorChannelCount > 0 && mesh.HasVertexColors(0);

            for (int i = 0; i < mesh.VertexCount; i++)
            {
                int globalVertexIndex = (int)(vertexOffset + (uint)i);
                int baseIndex = globalVertexIndex * componentsPerVertex;

                Vector3D pos = mesh.Vertices[i];

                // Position (3)
                vertexData[baseIndex + 0] = pos.X;
                vertexData[baseIndex + 1] = pos.Y;
                vertexData[baseIndex + 2] = pos.Z;

                // Normal (3)
                Vector3D normal = hasNormals ? mesh.Normals[i] : new Vector3D(0, 1, 0);
                vertexData[baseIndex + 3] = normal.X;
                vertexData[baseIndex + 4] = normal.Y;
                vertexData[baseIndex + 5] = normal.Z;

                // Tangent (4)
                Vector3D tangentVec = hasTangents ? mesh.Tangents[i] : new Vector3D(1, 0, 0);
                float tangentW = 1.0f;

                vertexData[baseIndex + 6] = tangentVec.X;
                vertexData[baseIndex + 7] = tangentVec.Y;
                vertexData[baseIndex + 8] = tangentVec.Z;
                vertexData[baseIndex + 9] = tangentW;

                // UV (2)
                float u = 0.0f;
                float v = 0.0f;
                if (hasUV0)
                {
                    Vector3D uv = mesh.TextureCoordinateChannels[0][i];
                    u = uv.X;
                    v = uv.Y;
                }

                vertexData[baseIndex + 10] = u;
                vertexData[baseIndex + 11] = v;

                // Color (4)
                float r = 1.0f, g = 1.0f, b = 1.0f, a = 1.0f;
                if (hasColor0)
                {
                    Color4D c = mesh.VertexColorChannels[0][i];
                    r = c.R;
                    g = c.G;
                    b = c.B;
                    a = c.A;
                }

                vertexData[baseIndex + 12] = r;
                vertexData[baseIndex + 13] = g;
                vertexData[baseIndex + 14] = b;
                vertexData[baseIndex + 15] = a;
            }

            uint baseVertexOffset = vertexOffset;

            foreach (Face face in mesh.Faces)
            {
                if (face.IndexCount != 3)
                {
                    continue;
                }

                indices.Add(baseVertexOffset + (uint)face.Indices[0]);
                indices.Add(baseVertexOffset + (uint)face.Indices[1]);
                indices.Add(baseVertexOffset + (uint)face.Indices[2]);
            }

            vertexOffset += (uint)mesh.VertexCount;
        }

        uint vertexCount = totalVertexCount;
        uint indexCount = (uint)indices.Count;

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        using (FileStream stream = new(outputPath, FileMode.Create, FileAccess.Write))
        using (BinaryWriter writer = new(stream))
        {
            writer.Write(Magic);

            writer.Write(vertexCount);
            writer.Write(indexCount);

            for (int i = 0; i < vertexData.Length; i++)
            {
                writer.Write(vertexData[i]);
            }

            for (int i = 0; i < indices.Count; i++)
            {
                writer.Write(indices[i]);
            }
        }
    }

}
