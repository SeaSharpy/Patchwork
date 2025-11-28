
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec4 aTangent;
layout(location = 3) in vec2 aTexCoord;
layout(location = 4) in vec4 aColor;

layout(location = 0) out VertO {
    vec3 position;
    vec3 geoNormal;
    vec4 tangent;
    vec2 uv;
    vec4 color;
} VSOutBuiltin;

layout(location = 5) flat out int InstanceIndex;

#disallow InstanceIndex

layout(location = 6) out vec4 vCurrClip;
layout(location = 7) out vec4 vPrevClip;

#disallow vCurrClip
#disallow vPrevClip

#include default_shared.glsl
#include default_sharednoray.glsl
#include default_sharedvertex.glsl

void main()
{
    InstanceIndex = int(gl_BaseInstance + gl_InstanceID);
    ModelMatrix modelMatrix = ModelMatrices[InstanceIndex];
    vec4 worldPosition = modelMatrix.model * vec4(aPosition, 1.0);
    vCurrClip = ViewProjection * worldPosition;
    vPrevClip = PrevViewProjection * modelMatrix.previousModel * vec4(aPosition, 1.0);
    MaterialData material = Materials[InstanceIndex];
    mat4 model = modelMatrix.model;
    mat3 normalMatrix = mat3(modelMatrix.normal);
    Vert v;
    v.position = worldPosition.xyz;
    v.normal = normalize(normalMatrix * aNormal);
    v.tangent = vec4(normalMatrix * aTangent.xyz, aTangent.w);
    v.uv = aTexCoord;
    v.color = aColor;
    VertexShader(material, v);
    VSOutBuiltin.position = v.position;
    VSOutBuiltin.geoNormal = v.normal;
    VSOutBuiltin.tangent = v.tangent;
    VSOutBuiltin.uv = v.uv;
    VSOutBuiltin.color = v.color;

    gl_Position = vCurrClip;
}
