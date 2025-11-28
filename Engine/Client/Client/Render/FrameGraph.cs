global using Patchwork.Client.Render;
namespace Patchwork.Client.Render;

public static class FrameGraph
{
    public struct GPUTexture
    {
        public GPUTexture(string path)
        {

        }

    }
    public struct GPUShader
    {
        public GPUShader(string path)
        {

        }
    }
    public struct GPUMesh
    {
        public GPUMesh(string path)
        {
            
        }
    }
    public static Dictionary<string, GPUTexture> Textures = new();
    public static Dictionary<string, GPUShader> Shaders = new();
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
            if (!Shaders.ContainsKey(model.ShaderPath))
            {
                Shaders[model.ShaderPath] = new GPUShader(model.ShaderPath);
                return false;
            }
            if (!Meshes.ContainsKey(model.DataPath))
            {
                Meshes[model.DataPath] = new GPUMesh(model.DataPath);
                return false;
            }
            foreach (ITexture texture in model.Textures)
                if (texture is PathTexture pathTexture)
                    if (!Textures.ContainsKey(pathTexture.Path))
                    {
                        Textures[pathTexture.Path] = new GPUTexture(pathTexture.Path);
                        return false;
                    }
        }
        return true;
    }
}