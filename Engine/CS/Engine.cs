using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Graphics.OpenGL4;
using Monitor = OpenTK.Windowing.GraphicsLibraryFramework.Monitor;
using System.Dynamic;
namespace Patchwork;

public class Engine : GameWindow
{
    static Engine InstanceInternal = null!;
    public ECS ECS { get; private set; } = null!;
    public Entity Camera = null!;
    public Matrix4 CameraProjection = Matrix4.Identity;
    public float DeltaTime { get; private set; }
    public float Time => (float)TimeDouble;
    public Box Viewport { get; private set; }
    public IRenderSystem Renderer = null!;
    double TimeDouble = 0;
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
            throw new InvalidOperationException("Engine already initialized.");
        InstanceInternal = this;
        base.OnLoad();
        Init(this);
        ECS = new();
        Camera = new("Camera", [], "Camera");
        IncludedFiles.Init();
        UIRenderer.Init();
        AudioPlayer.Init();
        Entrypoint.Init();
        Renderer = Entrypoint.Renderer();
        OnResize(new ResizeEventArgs());
    }
    private ButtonState GetButtonState(MouseButton button)
    {
        if (MouseState.IsButtonPressed(button))
            return ButtonState.Pressed;
        if (MouseState.IsButtonReleased(button))
            return ButtonState.Released;
        if (MouseState.IsButtonDown(button))
            return ButtonState.Down;
        return ButtonState.Up;
    }
    protected override unsafe void OnUpdateFrame(FrameEventArgs args)
    {
        base.OnUpdateFrame(args);
        DeltaTime = (float)args.Time;
        TimeDouble += DeltaTime;
        ECS.Update();
        InputState.Update(MouseState.Position, MouseState.PreviousPosition, MouseState.Delta, GetButtonState(MouseButton.Left), GetButtonState(MouseButton.Right), GetButtonState(MouseButton.Middle), GetButtonState(MouseButton.Button4), GetButtonState(MouseButton.Button5), MouseState.Scroll, MouseState.PreviousScroll, MouseState.ScrollDelta, KeyboardState);
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
        UIRenderer.Flush();
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
        foreach (ISystem system in ECS.Systems)
            system.Dispose();
        ECS.Systems.Clear();
        UIRenderer.Dispose();
        AudioPlayer.DisposeAll();
    }
}

public static class Helper
{
    static Engine Instance = null!;
    public static void Init(Engine engine)
    {
        if (Instance != null)
            throw new InvalidOperationException("Engine helper already initialized.");
        Instance = engine;
    }
    public static Entity Camera => Instance.Camera;
    public static float Time => Instance.Time;
    public static float DeltaTime => Instance.DeltaTime;
    public static Box Viewport => Instance.Viewport;
    public static string Title
    {
        get => Instance.Title;
        set => Instance.Title = value;
    }
    public static Matrix4 CameraProjection
    {
        get => Instance.CameraProjection;
        set => Instance.CameraProjection = value;
    }
    public static void Close() => Instance.Close();
    public static string Clipboard
    {
        get => Instance.ClipboardString;
        set => Instance.ClipboardString = value;
    }
    static unsafe byte[] PtrToBytes(IntPtr ptr, int length)
    {
        byte[] data = new byte[length];
        fixed (byte* dest = data)
        {
            System.Buffer.MemoryCopy((void*)ptr, dest, length, length);
        }
        return data;
    }
    public static Texture Icon
    {
        set
        {
            Instance.Icon = new([new(value.Width, value.Height, PtrToBytes(value.Data, value.Width * value.Height * 4))]);
        }
    }
    public static IRenderSystem Renderer
    {
        get => Instance.Renderer;
        set => Instance.Renderer = value;
    }
}