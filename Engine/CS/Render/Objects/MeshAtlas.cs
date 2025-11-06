using Assimp;
using System.Numerics;
using OpenTK.Graphics.OpenGL4;
using System.Runtime.InteropServices;
using PrimitiveType = OpenTK.Graphics.OpenGL4.PrimitiveType;
using Vector3 = System.Numerics.Vector3;
namespace Patchwork.Render.Objects;

public static class MeshAtlas
{
    public struct MeshSlice
    {
        public string Name;
        public int BaseVertex;
        public int FirstIndex;
        public int IndexCount;
    }

    public static int Vao { get; private set; } = 0;
    public static int Vbo { get; private set; } = 0;
    public static int Ibo { get; private set; } = 0;

    public static readonly int Stride = 16 * sizeof(float);

    private static readonly List<float> Vertices = new(capacity: 1 << 20);
    private static readonly List<uint> Indices = new(capacity: 1 << 20);
    private static readonly List<MeshSlice> Slices = new(capacity: 256);
    public static ReadOnlySpan<float> VertexSpan
        => CollectionsMarshal.AsSpan(Vertices);

    public static ReadOnlySpan<uint> IndexSpan
        => CollectionsMarshal.AsSpan(Indices);
    private static readonly Dictionary<string, int> NameMap = new(StringComparer.Ordinal);

    private static bool Initialized;
    private static bool Disposed;

    public static IReadOnlyList<MeshSlice> Meshes => Slices;

    private static void EnsureInit()
    {
        if (Initialized) return;
        Initialized = true;

        Vao = GL.GenVertexArray();
        Vbo = GL.GenBuffer();
        Ibo = GL.GenBuffer();

        GL.BindVertexArray(Vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, Vbo);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, Ibo);

        GL.BufferData(BufferTarget.ArrayBuffer, 0, IntPtr.Zero, BufferUsageHint.StaticDraw);
        GL.BufferData(BufferTarget.ElementArrayBuffer, 0, IntPtr.Zero, BufferUsageHint.StaticDraw);

        int offset = 0;
        AddAttrib(location: 0, size: 3, Stride, ref offset);
        AddAttrib(location: 1, size: 3, Stride, ref offset);
        AddAttrib(location: 2, size: 4, Stride, ref offset);
        AddAttrib(location: 3, size: 2, Stride, ref offset);
        AddAttrib(location: 4, size: 4, Stride, ref offset);

        GL.BindVertexArray(0);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
    }

    private static void AddAttrib(int location, int size, int stride, ref int offsetBytes)
    {
        GL.EnableVertexAttribArray(location);
        GL.VertexAttribPointer(location, size, VertexAttribPointerType.Float, false, stride, offsetBytes);
        offsetBytes += size * sizeof(float);
    }

    private static void ReuploadGL()
    {
        EnsureInit();

        GL.BindVertexArray(Vao);

        GL.BindBuffer(BufferTarget.ArrayBuffer, Vbo);
        int vBytes = Vertices.Count * sizeof(float);
        if (vBytes > 0)
            GL.BufferData(BufferTarget.ArrayBuffer, vBytes, Vertices.ToArray(), BufferUsageHint.StaticDraw);
        else
            GL.BufferData(BufferTarget.ArrayBuffer, 0, IntPtr.Zero, BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, Ibo);
        int iBytes = Indices.Count * sizeof(uint);
        if (iBytes > 0)
            GL.BufferData(BufferTarget.ElementArrayBuffer, iBytes, Indices.ToArray(), BufferUsageHint.StaticDraw);
        else
            GL.BufferData(BufferTarget.ElementArrayBuffer, 0, IntPtr.Zero, BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
        GL.BindVertexArray(0);
    }

    public static void Bind() => GL.BindVertexArray(Vao);

    public static bool TryGetSlice(string name, out MeshSlice slice)
    {
        if (NameMap.TryGetValue(name, out int idx))
        {
            slice = Slices[idx];
            return true;
        }
        slice = default;
        return false;
    }

    public static MeshSlice GetSlice(string name)
        => NameMap.TryGetValue(name, out int idx) ? Slices[idx] :
           throw new KeyNotFoundException($"Mesh slice '{name}' not found in global atlas.");

    public static void Draw(string name)
    {
        MeshSlice s = GetSlice(name);
        GL.DrawElementsBaseVertex(
            PrimitiveType.Triangles,
            s.IndexCount,
            DrawElementsType.UnsignedInt,
            s.FirstIndex * sizeof(uint),
            s.BaseVertex);
    }

    public static IReadOnlyList<MeshSlice> Add(params string[] paths)
        => AddFilesInternal(paths);

    public static IReadOnlyList<MeshSlice> Add(
        ReadOnlySpan<float> vertexData,
        ReadOnlySpan<uint> indexData,
        IEnumerable<MeshSlice> localSlices,
        BufferUsageHint usage = BufferUsageHint.StaticDraw,
        bool validate = true)
        => AddRawInternal(vertexData, indexData, localSlices, usage, validate);

    public static void Dispose()
    {
        if (Disposed) return;
        Disposed = true;

        if (Vao != 0) GL.DeleteVertexArray(Vao);
        if (Vbo != 0) GL.DeleteBuffer(Vbo);
        if (Ibo != 0) GL.DeleteBuffer(Ibo);
        Vao = Vbo = Ibo = 0;

        Vertices.Clear();
        Indices.Clear();
        Slices.Clear();
        NameMap.Clear();
        Initialized = false;
    }

    private static readonly AssimpContext Assimp = new();

    private static IReadOnlyList<MeshSlice> AddFilesInternal(IEnumerable<string> paths)
    {
        EnsureInit();

        List<MeshSlice> added = new List<MeshSlice>();
        int baseVertex = Vertices.Count / 16;
        int firstIndex = Indices.Count;

        bool flipUVs = false;
        PostProcessSteps pp =
            PostProcessSteps.Triangulate |
            PostProcessSteps.JoinIdenticalVertices |
            PostProcessSteps.PreTransformVertices |
            PostProcessSteps.GenerateNormals |
            PostProcessSteps.CalculateTangentSpace;

        foreach (string path in paths)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Model file not found: {path}");

            Scene scene = Assimp.ImportFile(path, pp);
            if (scene == null || scene.MeshCount == 0)
                continue;

            for (int mi = 0; mi < scene.MeshCount; ++mi)
            {
                Mesh m = scene.Meshes[mi];
                int vcount = m.VertexCount;

                for (int v = 0; v < vcount; ++v)
                {
                    Vector3D p = m.Vertices[v];
                    Vertices.Add(p.X); Vertices.Add(p.Y); Vertices.Add(p.Z);

                    Vector3 n = m.HasNormals
                        ? new Vector3(m.Normals[v].X, m.Normals[v].Y, m.Normals[v].Z)
                        : Vector3.UnitY;
                    n = Vector3.Normalize(n);
                    Vertices.Add(n.X); Vertices.Add(n.Y); Vertices.Add(n.Z);

                    Vector3 t = Vector3.UnitX; float w = 1f;
                    if (m.HasTangentBasis && m.HasNormals)
                    {
                        Vector3D tt = m.Tangents[v];
                        Vector3D bb = m.BiTangents[v];
                        Vector3D nn = m.Normals[v];

                        t = new Vector3(tt.X, tt.Y, tt.Z);
                        Vector3 b = new Vector3(bb.X, bb.Y, bb.Z);
                        Vector3 nrm = new Vector3(nn.X, nn.Y, nn.Z);
                        w = Vector3.Dot(Vector3.Cross(nrm, t), b) < 0 ? -1f : 1f;
                    }
                    t = Vector3.Normalize(t);
                    Vertices.Add(t.X); Vertices.Add(t.Y); Vertices.Add(t.Z); Vertices.Add(w);

                    float u = 0f, vtex = 0f;
                    if (m.TextureCoordinateChannelCount > 0 && m.HasTextureCoords(0))
                    {
                        Vector3D uv = m.TextureCoordinateChannels[0][v];
                        u = uv.X;
                        vtex = flipUVs ? (1f - uv.Y) : uv.Y;
                    }
                    Vertices.Add(u); Vertices.Add(vtex);

                    float r = 1f, g = 1f, bcol = 1f, a = 1f;
                    if (m.VertexColorChannelCount > 0 && m.HasVertexColors(0))
                    {
                        Color4D c = m.VertexColorChannels[0][v];
                        r = c.R; g = c.G; bcol = c.B; a = c.A;
                    }
                    Vertices.Add(r); Vertices.Add(g); Vertices.Add(bcol); Vertices.Add(a);
                }

                int firstIdxThisMesh = Indices.Count;
                for (int f = 0; f < m.FaceCount; ++f)
                {
                    Face face = m.Faces[f];
                    if (face.IndexCount != 3) continue;
                    Indices.Add((uint)face.Indices[0]);
                    Indices.Add((uint)face.Indices[1]);
                    Indices.Add((uint)face.Indices[2]);
                }

                int indexCount = Indices.Count - firstIdxThisMesh;
                MeshSlice slice = new MeshSlice
                {
                    Name = (Path.GetDirectoryName(path) ?? "") + "/" + Path.GetFileName(path) + (mi == 0 ? "" : $"#{mi + 1}"),
                    BaseVertex = baseVertex,
                    FirstIndex = firstIndex,
                    IndexCount = indexCount
                };

                int globalIndex = Slices.Count;
                Slices.Add(slice);
                NameMapAddOrThrow(slice.Name, globalIndex);

                added.Add(slice);

                baseVertex += vcount;
                firstIndex += indexCount;
            }
        }
        ReuploadGL();
        return added;
    }

    public static void Validate(ReadOnlySpan<float> vertexData, ReadOnlySpan<uint> indexData, IReadOnlyList<MeshSlice> slices)
    {
        if (vertexData.Length % 16 != 0)
            throw new ArgumentException($"Vertex buffer length ({vertexData.Length}) is not a multiple of 16 floats (P3 N3 T4 UV2 C4).");

        int vertexCount = vertexData.Length / 16;

        foreach (MeshSlice s in slices)
        {
            if (s.BaseVertex < 0 || s.BaseVertex > vertexCount)
                throw new ArgumentOutOfRangeException(nameof(s.BaseVertex), $"Slice '{s.Name}' BaseVertex {s.BaseVertex} out of range [0,{vertexCount}].");

            if (s.FirstIndex < 0 || s.IndexCount < 0 || (s.FirstIndex + s.IndexCount) > indexData.Length)
                throw new ArgumentOutOfRangeException(nameof(s.FirstIndex), $"Slice '{s.Name}' index window [{s.FirstIndex},{s.FirstIndex + s.IndexCount}) out of range [0,{indexData.Length}).");
        }
    }

    private static IReadOnlyList<MeshSlice> AddRawInternal(
        ReadOnlySpan<float> vertexData,
        ReadOnlySpan<uint> indexData,
        IEnumerable<MeshSlice> localSlices,
        BufferUsageHint usage,
        bool validate)
    {
        EnsureInit();

        MeshSlice[] localSliceArray = localSlices.ToArray();
        if (validate) Validate(vertexData, indexData, localSliceArray);

        int baseVertexOffset = Vertices.Count / 16;
        int firstIndexOffset = Indices.Count;

        Vertices.AddRange(vertexData.ToArray());
        Indices.AddRange(indexData.ToArray());

        List<MeshSlice> added = new List<MeshSlice>(localSliceArray.Length);
        foreach (MeshSlice sLocal in localSliceArray)
        {
            MeshSlice sGlobal = new MeshSlice
            {
                Name = sLocal.Name,
                BaseVertex = sLocal.BaseVertex + baseVertexOffset,
                FirstIndex = sLocal.FirstIndex + firstIndexOffset,
                IndexCount = sLocal.IndexCount
            };

            int globalIndex = Slices.Count;
            Slices.Add(sGlobal);
            NameMapAddOrThrow(sGlobal.Name, globalIndex);

            added.Add(sGlobal);

        }

        ReuploadGL();
        return added;
    }

    private static void NameMapAddOrThrow(string name, int idx)
    {
        if (NameMap.TryGetValue(name, out int existing) && existing != idx)
            throw new Exception($"Mesh name '{name}' is already used in the global atlas.");
        NameMap[name] = idx;
    }
}
