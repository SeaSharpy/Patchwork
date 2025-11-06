using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using Patchwork.PECS;
using Patchwork.Render.Objects;
using static Patchwork.Render.Objects.MeshAtlas;

namespace Patchwork.Render;

public class BuiltinRenderer : IRenderSystem
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DrawElementsIndirectCommand
    {
        public uint Count;
        public uint InstanceCount;
        public uint FirstIndex;
        public uint BaseVertex;
        public uint BaseInstance;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct GpuModelMatrix
    {
        public readonly Matrix4 Model;
        public readonly Matrix4 ModelInverse;
        public readonly Matrix4 NormalMatrix;
        public readonly Matrix4 PreviousModel;

        public GpuModelMatrix(in Matrix4 model,
                              in Matrix4 modelInverse,
                              in Matrix4 normalMatrix,
                              in Matrix4 previousModel)
        {
            Model = model;
            ModelInverse = modelInverse;
            NormalMatrix = normalMatrix;
            PreviousModel = previousModel;
        }
    }

    private readonly nint ModelMatrixSize = Marshal.SizeOf<GpuModelMatrix>();
    private readonly nint IndirectCmdSize = Marshal.SizeOf<DrawElementsIndirectCommand>();

    private int MatricesSsbo = 0;
    private readonly Dictionary<Entity, Matrix4> PreviousModelMatrices = new();
    private readonly Dictionary<Entity, Matrix4> PreviousViewProjectionByCamera = new();

    public Dictionary<Shader, int> IndirectBuffersOpaque = new();
    public Dictionary<Shader, int> IndirectBuffersTransparent = new();

    private readonly Dictionary<Shader, int> MaterialSsboByShader = new();

    private int GBufferFbo = 0;
    private int ColorTexture = 0;
    private int NormalTexture = 0;
    private int GeoNormalTexture = 0;
    private int MaterialIdTexture = 0;
    private int MotionVectorTexture = 0;
    private int DepthTexture = 0;
    private int GBufferWidth = 0;
    private int GBufferHeight = 0;

    private static readonly float[] ClearColorVector = new float[4] { 0f, 0f, 0f, 0f };
    private static readonly uint[] ClearUIntVector = new uint[1] { 0u };

    private void DeleteGBufferTextures()
    {
        void DeleteTexture(ref int handle)
        {
            if (handle != 0)
            {
                GL.DeleteTexture(handle);
                handle = 0;
            }
        }

        DeleteTexture(ref ColorTexture);
        DeleteTexture(ref NormalTexture);
        DeleteTexture(ref GeoNormalTexture);
        DeleteTexture(ref MaterialIdTexture);
        DeleteTexture(ref MotionVectorTexture);
        DeleteTexture(ref DepthTexture);
    }

    private static int CreateAttachmentTexture(int width,
                                               int height,
                                               PixelInternalFormat internalFormat,
                                               PixelFormat format,
                                               PixelType type)
    {
        int texture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, texture);
        GL.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, width, height, 0, format, type, IntPtr.Zero);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.BindTexture(TextureTarget.Texture2D, 0);
        return texture;
    }

    private void EnsureGBuffer(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;

        bool needsCreate = GBufferFbo == 0;
        if (needsCreate)
            GBufferFbo = GL.GenFramebuffer();

        if (!needsCreate && width == GBufferWidth && height == GBufferHeight)
            return;

        GBufferWidth = width;
        GBufferHeight = height;

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, GBufferFbo);

        DeleteGBufferTextures();

        ColorTexture = CreateAttachmentTexture(width, height, PixelInternalFormat.Rgb16f, PixelFormat.Rgb, PixelType.Float);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, ColorTexture, 0);

        NormalTexture = CreateAttachmentTexture(width, height, PixelInternalFormat.Rgba16f, PixelFormat.Rgba, PixelType.Float);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, NormalTexture, 0);

        GeoNormalTexture = CreateAttachmentTexture(width, height, PixelInternalFormat.Rgba16f, PixelFormat.Rgba, PixelType.Float);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment2, TextureTarget.Texture2D, GeoNormalTexture, 0);

        MaterialIdTexture = CreateAttachmentTexture(width, height, PixelInternalFormat.R32ui, PixelFormat.RedInteger, PixelType.UnsignedInt);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment3, TextureTarget.Texture2D, MaterialIdTexture, 0);

        MotionVectorTexture = CreateAttachmentTexture(width, height, PixelInternalFormat.Rg16f, PixelFormat.Rg, PixelType.Float);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment4, TextureTarget.Texture2D, MotionVectorTexture, 0);

        DepthTexture = CreateAttachmentTexture(width, height, PixelInternalFormat.DepthComponent24, PixelFormat.DepthComponent, PixelType.Float);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, TextureTarget.Texture2D, DepthTexture, 0);

        DrawBuffersEnum[] drawBuffers = new[]
        {
            DrawBuffersEnum.ColorAttachment0,
            DrawBuffersEnum.ColorAttachment1,
            DrawBuffersEnum.ColorAttachment2,
            DrawBuffersEnum.ColorAttachment3,
            DrawBuffersEnum.ColorAttachment4
        };

        GL.DrawBuffers(drawBuffers.Length, drawBuffers);

        FramebufferErrorCode status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != FramebufferErrorCode.FramebufferComplete)
            throw new InvalidOperationException($"GBuffer framebuffer incomplete: {status}");

        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    private void RenderFinalOutput(Box viewport)
    {
        GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, GBufferFbo);
        GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
        GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
        GL.BlitFramebuffer(
            0, 0, GBufferWidth, GBufferHeight,
            viewport.X, viewport.Y, viewport.X + viewport.Width, viewport.Y + viewport.Height,
            ClearBufferMask.ColorBufferBit,
            BlitFramebufferFilter.Linear);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Load()
    {
        MatricesSsbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, MatricesSsbo);
        GL.BufferData(BufferTarget.ShaderStorageBuffer, 1, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
    }

    public void Update() { }

    public void Render()
    {
        Box viewport = Helper.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
            return;

        GL.Enable(EnableCap.DepthTest);
        GL.DepthFunc(DepthFunction.Lequal);
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(TriangleFace.Back);
        GL.Enable(EnableCap.Blend);
        GL.BlendFuncSeparate(
            BlendingFactorSrc.SrcAlpha,
            BlendingFactorDest.OneMinusSrcAlpha,
            BlendingFactorSrc.One,
            BlendingFactorDest.OneMinusSrcAlpha
        );
        GL.BlendEquationSeparate(BlendEquationMode.FuncAdd, BlendEquationMode.FuncAdd);
        GL.Enable(IndexedEnableCap.Blend, 0);
        for (int i = 1; i <= 4; i++)
            GL.Disable(IndexedEnableCap.Blend, i);

        EnsureGBuffer(viewport.Width, viewport.Height);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, GBufferFbo);

        GL.ClearBuffer(ClearBuffer.Color, 0, ClearColorVector);
        GL.ClearBuffer(ClearBuffer.Color, 1, ClearColorVector);
        GL.ClearBuffer(ClearBuffer.Color, 2, ClearColorVector);
        GL.ClearBuffer(ClearBuffer.Color, 3, ClearUIntVector);
        GL.ClearBuffer(ClearBuffer.Color, 4, ClearColorVector);
        GL.ClearBuffer(ClearBufferCombined.DepthStencil, 0, 1f, 0);

        MeshRenderer[] meshes = ECS.GetComponents<MeshRenderer>().ToArray();
        int instanceCount = meshes.Length;

        GpuModelMatrix[] matrixData = new GpuModelMatrix[instanceCount];
        HashSet<Entity> activeEntities = new HashSet<Entity>();
        for (int i = 0; i < instanceCount; i++)
        {
            Entity entity = meshes[i].Entity;
            activeEntities.Add(entity);

            Matrix4 model = entity.TransformMatrix;
            Matrix4 modelInverse = model.Inverted();
            Matrix4 normalMatrix = modelInverse;
            normalMatrix.Transpose();

            if (!PreviousModelMatrices.TryGetValue(entity, out Matrix4 previousModel))
                previousModel = model;

            matrixData[i] = new GpuModelMatrix(model, modelInverse, normalMatrix, previousModel);
            PreviousModelMatrices[entity] = model;
        }

        if (PreviousModelMatrices.Count > activeEntities.Count)
        {
            foreach (Entity? entity in PreviousModelMatrices.Keys.ToArray())
            {
                if (!activeEntities.Contains(entity))
                    PreviousModelMatrices.Remove(entity);
            }
        }

        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, MatricesSsbo);
        int matricesBytes = instanceCount * (int)ModelMatrixSize;
        if (instanceCount > 0)
        {
            GCHandle handle = GCHandle.Alloc(matrixData, GCHandleType.Pinned);
            try
            {
                GL.BufferData(BufferTarget.ShaderStorageBuffer, matricesBytes, handle.AddrOfPinnedObject(), BufferUsageHint.DynamicDraw);
            }
            finally { handle.Free(); }
        }
        else
        {
            GL.BufferData(BufferTarget.ShaderStorageBuffer, 1, IntPtr.Zero, BufferUsageHint.DynamicDraw);
        }
        GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, MatricesSsbo);
        GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

        Matrix4 cameraWorld = Helper.Camera.TransformMatrix;
        Matrix4 viewMatrix = cameraWorld.Inverted();
        Matrix4 projection = Helper.CameraProjection;
        Matrix4 viewProjection = viewMatrix * projection;
        if (!PreviousViewProjectionByCamera.TryGetValue(Helper.Camera, out Matrix4 previousViewProjection))
            previousViewProjection = viewProjection;
        PreviousViewProjectionByCamera[Helper.Camera] = viewProjection;

        Vector3 camPos = Helper.Camera.Transform.Position;

        Dictionary<Shader, Type> materialTypeByShader = new Dictionary<Shader, Type>();
        Dictionary<Shader, Array> materialArrayByShader = new Dictionary<Shader, Array>();

        Dictionary<Shader, List<DrawElementsIndirectCommand>> perShaderOpaqueCmds = new Dictionary<Shader, List<DrawElementsIndirectCommand>>();
        Dictionary<Shader, List<DrawElementsIndirectCommand>> perShaderTransparentCmds = new Dictionary<Shader, List<DrawElementsIndirectCommand>>();

        Dictionary<Shader, List<(float dist, DrawElementsIndirectCommand cmd)>> tempOpaque = new Dictionary<Shader, List<(float dist, DrawElementsIndirectCommand cmd)>>();
        Dictionary<Shader, List<(float dist, DrawElementsIndirectCommand cmd)>> tempTransparent = new Dictionary<Shader, List<(float dist, DrawElementsIndirectCommand cmd)>>();

        for (int i = 0; i < instanceCount; i++)
        {
            MeshRenderer mr = meshes[i];

            if (!TryGetSlice(mr.Mesh, out MeshSlice slice))
                throw new KeyNotFoundException($"Mesh '{mr.Mesh}' not found in global MeshAtlas.");

            Shader shader = mr.Shader;

            object matVal = mr.Material ?? throw new Exception(
                $"Material payload is null for instance {i} (shader '{mr.Shader?.Name ?? "<null>"}').");

            Type matType = matVal.GetType();
            if (materialTypeByShader.TryGetValue(shader, out Type? existing))
            {
                if (existing != matType)
                    throw new InvalidCastException($"Shader '{shader.Name}' bound with multiple material types: '{existing}' and '{matType}'. This renderer expects exactly one material struct type per shader.");
            }
            else
            {
                if (!matType.IsValueType)
                    throw new InvalidDataException($"Material type '{matType}' for shader '{shader.Name}' must be a value-type struct.");

                materialTypeByShader[shader] = matType;
                materialArrayByShader[shader] = Array.CreateInstance(matType, instanceCount);
            }
            materialArrayByShader[shader].SetValue(matVal, i);

            DrawElementsIndirectCommand command = new DrawElementsIndirectCommand
            {
                Count = (uint)slice.IndexCount,
                InstanceCount = 1u,
                FirstIndex = (uint)slice.FirstIndex,
                BaseVertex = (uint)slice.BaseVertex,
                BaseInstance = (uint)i,
            };

            Vector3 worldPos = matrixData[i].Model.ExtractTranslation();
            float dist2 = (worldPos - camPos).LengthSquared;

            Dictionary<Shader, List<(float dist, DrawElementsIndirectCommand cmd)>> bucket = mr.Transparent ? tempTransparent : tempOpaque;
            if (!bucket.TryGetValue(shader, out List<(float dist, DrawElementsIndirectCommand cmd)>? list))
                bucket[shader] = list = [];
            list.Add((dist2, command));
        }

        void PruneBuffers(Dictionary<Shader, int> buffers, Dictionary<Shader, List<DrawElementsIndirectCommand>> active)
        {
            if (buffers.Count == 0) return;
            List<Shader> toRemove = [];
            foreach (KeyValuePair<Shader, int> kv in buffers)
                if (!active.ContainsKey(kv.Key))
                    toRemove.Add(kv.Key);
            foreach (Shader s in toRemove)
            {
                GL.DeleteBuffer(buffers[s]);
                buffers.Remove(s);
            }
        }

        void BuildPassBuffers(
            Dictionary<Shader, List<(float dist, DrawElementsIndirectCommand cmd)>> source,
            bool frontToBack,
            Dictionary<Shader, List<DrawElementsIndirectCommand>> outCmds,
            Dictionary<Shader, int> outBuffers)
        {
            outCmds.Clear();

            foreach ((Shader shader, List<(float dist, DrawElementsIndirectCommand cmd)> list) in source)
            {
                if (frontToBack)
                    list.Sort((a, b) => a.dist.CompareTo(b.dist));
                else
                    list.Sort((a, b) => b.dist.CompareTo(a.dist));

                List<DrawElementsIndirectCommand> cmds = list.Select(t => t.cmd).ToList();
                outCmds[shader] = cmds;

                if (!outBuffers.TryGetValue(shader, out int ibo))
                {
                    ibo = GL.GenBuffer();
                    outBuffers[shader] = ibo;
                }

                GL.BindBuffer(BufferTarget.DrawIndirectBuffer, outBuffers[shader]);
                if (cmds.Count > 0)
                {
                    DrawElementsIndirectCommand[] arr = cmds.ToArray();
                    GCHandle handle = GCHandle.Alloc(arr, GCHandleType.Pinned);
                    try
                    {
                        GL.BufferData(BufferTarget.DrawIndirectBuffer,
                                      cmds.Count * (int)IndirectCmdSize,
                                      handle.AddrOfPinnedObject(),
                                      BufferUsageHint.DynamicDraw);
                    }
                    finally { handle.Free(); }
                }
                else
                {
                    GL.BufferData(BufferTarget.DrawIndirectBuffer, 1, IntPtr.Zero, BufferUsageHint.DynamicDraw);
                }
            }

            PruneBuffers(outBuffers, outCmds);

            GL.BindBuffer(BufferTarget.DrawIndirectBuffer, 0);
        }

        if (MaterialSsboByShader.Count > 0)
        {
            List<Shader> stale = MaterialSsboByShader.Keys.Where(s => !materialArrayByShader.ContainsKey(s)).ToList();
            foreach (Shader? s in stale)
            {
                GL.DeleteBuffer(MaterialSsboByShader[s]);
                MaterialSsboByShader.Remove(s);
            }
        }
        foreach (KeyValuePair<Shader, Array> kv in materialArrayByShader)
        {
            Shader shader = kv.Key;
            Array arr = kv.Value;
            Type matType = materialTypeByShader[shader];
            int elemSize = Marshal.SizeOf(matType);
            int byteLen = instanceCount * elemSize;

            if (!MaterialSsboByShader.TryGetValue(shader, out int ssbo))
            {
                ssbo = GL.GenBuffer();
                MaterialSsboByShader[shader] = ssbo;
            }

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo);
            if (instanceCount > 0)
            {
                GCHandle handle = GCHandle.Alloc(arr, GCHandleType.Pinned);
                GL.BufferData(BufferTarget.ShaderStorageBuffer, byteLen, handle.AddrOfPinnedObject(), BufferUsageHint.DynamicDraw);
                handle.Free();
            }
            else
            {
                GL.BufferData(BufferTarget.ShaderStorageBuffer, 1, IntPtr.Zero, BufferUsageHint.DynamicDraw);
            }
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
        }

        Dictionary<Shader, List<DrawElementsIndirectCommand>> opaqueCmds = new Dictionary<Shader, List<DrawElementsIndirectCommand>>();
        Dictionary<Shader, List<DrawElementsIndirectCommand>> transparentCmds = new Dictionary<Shader, List<DrawElementsIndirectCommand>>();

        BuildPassBuffers(tempOpaque, frontToBack: true, outCmds: opaqueCmds, outBuffers: IndirectBuffersOpaque);
        BuildPassBuffers(tempTransparent, frontToBack: false, outCmds: transparentCmds, outBuffers: IndirectBuffersTransparent);

        void DrawGeometry(Dictionary<Shader, List<DrawElementsIndirectCommand>> cmdMap, Dictionary<Shader, int> bufferMap, Action<Shader> setUniforms)
        {
            foreach ((Shader shader, List<DrawElementsIndirectCommand> commands) in cmdMap)
            {
                shader.Use();
                setUniforms(shader);

                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 0, MatricesSsbo);

                if (!MaterialSsboByShader.TryGetValue(shader, out int matSsbo))
                    throw new InvalidOperationException($"Internal: no material buffer found for shader '{shader.Name}'.");
                GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, 1, matSsbo);

                Bind();

                int indirectBo = bufferMap[shader];
                GL.BindBuffer(BufferTarget.DrawIndirectBuffer, indirectBo);

                GL.MultiDrawElementsIndirect(
                    PrimitiveType.Triangles,
                    DrawElementsType.UnsignedInt,
                    IntPtr.Zero,
                    commands.Count,
                    0);

                GL.BindBuffer(BufferTarget.DrawIndirectBuffer, 0);
                GL.BindVertexArray(0);
            }
        }

        GL.Viewport(viewport.X, viewport.Y, viewport.Width, viewport.Height);
        GL.ClearColor(0.2f, 0.3f, 0.3f, 1.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        GL.ColorMask(false, false, false, false);
        GL.DepthMask(true);
        GL.DepthFunc(DepthFunction.Less);
        DrawGeometry(opaqueCmds, IndirectBuffersOpaque, shader =>
        {
            shader.Set("DepthOnly", 1);
            shader.Set("ViewProjection", viewProjection);
            shader.Set("PrevViewProjection", previousViewProjection);
            shader.Set("View", viewMatrix);
            shader.Set("Time", Time);
            shader.Set("CameraPosition", Camera.Transform.Position);
            shader.Set("ViewportSize", new Vector2(viewport.Width, viewport.Height));
        });

        GL.ColorMask(true, true, true, true);
        GL.DepthMask(false);
        GL.DepthFunc(DepthFunction.Equal);
        DrawGeometry(opaqueCmds, IndirectBuffersOpaque, shader =>
        {
            shader.Set("DepthOnly", 0);
            shader.Set("ViewProjection", viewProjection);
            shader.Set("PrevViewProjection", previousViewProjection);
            shader.Set("View", viewMatrix);
            shader.Set("Time", Time);
            shader.Set("CameraPosition", Camera.Transform.Position);
            shader.Set("ViewportSize", new Vector2(viewport.Width, viewport.Height));
        });

        if (transparentCmds.Count > 0)
        {
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            GL.DepthMask(false);
            GL.DepthFunc(DepthFunction.Lequal);

            DrawGeometry(transparentCmds, IndirectBuffersTransparent, shader =>
            {
                shader.Set("DepthOnly", 0);
                shader.Set("ViewProjection", viewProjection);
                shader.Set("PrevViewProjection", previousViewProjection);
                shader.Set("View", viewMatrix);
                shader.Set("Time", Time);
                shader.Set("CameraPosition", Camera.Transform.Position);
                shader.Set("ViewportSize", new Vector2(viewport.Width, viewport.Height));
            });

            GL.Disable(EnableCap.Blend);
        }

        GL.DepthMask(true);
        GL.DepthFunc(DepthFunction.Lequal);

        RenderFinalOutput(viewport);
    }

    public void Dispose()
    {
        if (MatricesSsbo != 0)
        {
            GL.DeleteBuffer(MatricesSsbo);
            MatricesSsbo = 0;
        }

        foreach (KeyValuePair<Shader, int> kv in IndirectBuffersOpaque)
            GL.DeleteBuffer(kv.Value);
        IndirectBuffersOpaque.Clear();

        foreach (KeyValuePair<Shader, int> kv in IndirectBuffersTransparent)
            GL.DeleteBuffer(kv.Value);
        IndirectBuffersTransparent.Clear();

        foreach (KeyValuePair<Shader, int> kv in MaterialSsboByShader)
            GL.DeleteBuffer(kv.Value);
        MaterialSsboByShader.Clear();

        if (GBufferFbo != 0)
        {
            GL.DeleteFramebuffer(GBufferFbo);
            GBufferFbo = 0;
        }

        DeleteGBufferTextures();
        GBufferWidth = 0;
        GBufferHeight = 0;
    }
}

public class MeshRenderer : IDataComponent
{
    public string Mesh;
    public Shader Shader;
    public object Material;
    public bool Transparent;

    public MeshRenderer(string mesh, Shader shader, object material, bool transparent = false)
    {
        Mesh = mesh;
        Shader = shader;
        Material = material;
        Transparent = transparent;
    }
}
