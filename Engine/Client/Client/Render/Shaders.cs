using OpenTK.Graphics.OpenGL4;
namespace Patchwork.Client.Render;

public static partial class Renderer
{
    private static int MainShader = CreateShaderProgram
    (
        @"
struct InstanceStruct
{
    mat4 Transform;
    mat3 NormalTransform;
    vec4 AlbedoColor;
    vec4 EmissiveColor;
    vec4 MetallicRoughnessAOColor;
    uint64_t Albedo;
    uint64_t Emissive;
    uint64_t MetallicRoughnessAO;
    uint64_t Normal;
    uint ID;
};
Buffer(Instance, InstanceStruct, 0);
uniform mat4 ViewProjection;
in vec3 Position;
in vec3 Normal;
in vec4 Tangent;
in vec2 UV;
in vec4 Color;
flat out uint64_t Albedo;
flat out uint64_t Emissive;
flat out uint64_t MetallicRoughnessAO;
flat out uint64_t DataNormal;
flat out uint ID;
flat out vec4 AlbedoColor;
flat out vec4 EmissiveColor;
flat out vec4 MetallicRoughnessAOColor;
out vec3 WorldPosition;
out vec3 OutNormal;
out vec4 OutTangent;
out vec2 OutUV;
out vec4 OutColor;
void main() 
{
    InstanceStruct instanceData = Instance[gl_InstanceID];
    mat4 transform = instanceData.Transform;
    vec4 worldPosition = vec4(Position, 1.0);
    WorldPosition = worldPosition.xyz;
    gl_Position = ViewProjection * worldPosition;
    OutNormal = instanceData.NormalTransform * Normal;
    OutTangent = vec4(instanceData.NormalTransform * Tangent.xyz, Tangent.w);
    OutUV = UV;
    OutColor = Color;
    AlbedoColor = instanceData.AlbedoColor;
    EmissiveColor = instanceData.EmissiveColor;
    MetallicRoughnessAOColor = instanceData.MetallicRoughnessAOColor;
    Albedo = instanceData.Albedo;
    Emissive = instanceData.Emissive;
    MetallicRoughnessAO = instanceData.MetallicRoughnessAO;
    DataNormal = instanceData.Normal;
    ID = instanceData.ID;
}
        ",
        @"
uniform mat4 ViewProjection;
uniform mat4 InvViewProjection;
flat in uint64_t Albedo;
flat in uint64_t Emissive;
flat in uint64_t MetallicRoughnessAO;
flat in uint64_t DataNormal;
flat in uint ID;
flat in vec4 AlbedoColor;
flat in vec4 EmissiveColor;
flat in vec4 MetallicRoughnessAOColor;
in vec3 WorldPosition;
in vec3 OutNormal;
in vec4 OutTangent;
in vec2 OutUV;
in vec4 OutColor;
out vec4 FragColor;
void main()
{
    FragColor = texture(HandleToSampler(Albedo), OutUV);
}
        "
    );

    private static string QuadVertex = @"
in vec2 Position;
in vec2 UV;
out vec2 OutUV;
out vec2 OutPosition;
void main()
{
    gl_Position = vec4(Position, 0.0, 1.0);
    OutUV = UV;
    OutPosition = Position;
}
";
    private static int SkyboxShader = CreateShaderProgram
    (
        QuadVertex,
        @"
uniform mat4 ViewProjection;
uniform mat4 InvViewProjection;
uniform sampler2D Skybox;
in vec2 OutUV;
in vec2 OutPosition;
out vec4 FragColor;
void main()
{
    vec4 clipNear = vec4(OutPosition, -1.0, 1.0);
    vec4 clipFar  = vec4(OutPosition,  1.0, 1.0);

    vec4 worldNear = InvViewProjection * clipNear;
    vec4 worldFar  = InvViewProjection * clipFar;

    worldNear /= worldNear.w;
    worldFar  /= worldFar.w;

    vec3 dir = normalize(worldFar.xyz - worldNear.xyz);
    FragColor = vec4(dir * 0.5 + 0.5, 1.0);
}
        "
    );

    private static void DisposeShaders()
    {
        GL.DeleteProgram(MainShader);
    }
}