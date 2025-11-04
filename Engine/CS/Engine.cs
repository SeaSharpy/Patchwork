using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common;
using OpenTK.Mathematics;
using Patchwork.PECS;
using Patchwork.Render;
using System.Runtime.InteropServices;
using Patchwork.Render.Objects;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Graphics.OpenGL4;
using Monitor = OpenTK.Windowing.GraphicsLibraryFramework.Monitor;
using Patchwork.Audio;
namespace Patchwork;

[StructLayout(LayoutKind.Sequential)]
struct BS
{
    public Vector4 Color;
}
public class Engine : GameWindow
{
    static Engine InstanceInternal = null!;
    public static Engine Instance => InstanceInternal;
    public static Engine I => InstanceInternal;
    public static ECS ECS { get; } = ECS.Instance;
    public static Entity Camera = new("Camera", [], "Camera");
    public static float DeltaTime { get; private set; }
    public static float Time => (float)TimeDouble;
    public static Box Viewport { get; private set; }
    IRenderSystem Renderer = null!;
    static double TimeDouble = 0;
    bool OnTop = false;
    public event Action? PostRender;
    public Engine(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings, bool onTop = false) : base(gameWindowSettings, nativeWindowSettings)
    {
        OnTop = onTop;
    }
    protected override void OnLoad()
    {
        Console.WriteLine("Loading...");
        if (InstanceInternal != null)
            throw new Exception("Engine already initialized.");
        InstanceInternal = this;
        base.OnLoad();
        IncludedFiles.Init();
        UIRenderer.Init();
        AudioPlayer.Init();
        Entrypoint.Init();
        Renderer = Entrypoint.Renderer();
        OnResize(new ResizeEventArgs());
    }

    protected override unsafe void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
        DeltaTime = (float)args.Time;
        TimeDouble += DeltaTime;
        ECS.Update();
        if (OnTop)
        {
            try
            {
                Window* window = WindowPtr;
                GLFW.SetWindowAttrib(window, WindowAttribute.Floating, true);
                GLFW.SetWindowAttrib(window, WindowAttribute.AutoIconify, false);
                GLFW.MaximizeWindow(window);
                GLFW.FocusWindow(window);
                MonitorInfo monitor = Monitors.GetPrimaryMonitor();
                GLFW.SetWindowMonitor(window, monitor.Handle.ToUnsafePtr<Monitor>(), monitor.ClientArea.Min.X, monitor.ClientArea.Min.Y, monitor.ClientArea.Size.X, monitor.ClientArea.Size.Y, GLFW.DontCare);
            }
            catch { }
        }
        if ((KeyboardState.IsKeyDown(Keys.LeftAlt) || KeyboardState.IsKeyDown(Keys.RightAlt)) && (KeyboardState.IsKeyDown(Keys.LeftShift) || KeyboardState.IsKeyDown(Keys.RightShift)) && (KeyboardState.IsKeyDown(Keys.LeftControl) || KeyboardState.IsKeyDown(Keys.RightControl)))
        {
            Close();
        }
    }
    protected override void OnResize(ResizeEventArgs args)
    {
        base.OnResize(args);
        Viewport = new Box
        {
            X = 0,
            Y = 0,
            Width = FramebufferSize.X,
            Height = FramebufferSize.Y
        };
    }
    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        GL.Viewport(Viewport.X, Viewport.Y, Viewport.Width, Viewport.Height);
        ECS.Render();
        PostRender?.Invoke();
        SwapBuffers();
    }
    protected override void OnUnload()
    {
        Console.WriteLine("Unloading...");
        base.OnUnload();
        Entrypoint.Close();
        Quad.Dispose();
        ECS.Dispose();
        TextureFactory.DisposeAll();
        Entity.DisposeAll();
        foreach (var system in ECS.Systems)
            system.Dispose();
        ECS.Systems.Clear();
        UIRenderer.Dispose();
        AudioPlayer.DisposeAll();
    }
}
