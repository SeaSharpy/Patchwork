using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Graphics.OpenGL4;
using Monitor = OpenTK.Windowing.GraphicsLibraryFramework.Monitor;
namespace Patchwork;

public class Engine : GameWindow
{
    private static Engine InstanceInternal = null!;
    public ECS ECS { get; private set; } = null!;
    public Entity Camera = null!;
    public Matrix4 CameraProjection = Matrix4.Identity;
    public float DeltaTime { get; private set; }
    public float Time => (float)TimeDouble;
    public Box Viewport { get; private set; }
    public IRenderSystem Renderer = null!;
    private double TimeDouble = 0;
    private bool OnTop = false;
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
        InputState.Update(MouseState.Position, MouseState.PreviousPosition, MouseState.Delta, GetButtonState(MouseButton.Left), GetButtonState(MouseButton.Right), GetButtonState(MouseButton.Middle), GetButtonState(MouseButton.Button4), GetButtonState(MouseButton.Button5), MouseState.Scroll, MouseState.PreviousScroll, MouseState.ScrollDelta, KeyboardState);
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
            Close();
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

public class Camera
{
    public bool Ortho;
    public Box Box
    {
        get
        {
            Vector2 offset = CameraEntity.Transform.Position.Xy;
            return new(offset - OrthoSize * 0.5f, offset + OrthoSize * 0.5f);
        }
    }
    public float Size;
    public bool Width;
    public float OrthoWidth => Width ? Size : Size * Aspect;
    public float OrthoHeight => Width ? Size * Aspect : Size;
    public Vector2 OrthoSize => new(OrthoWidth, OrthoHeight);
    public float Near = 0, Far = 1;
    public float FOV;
    public float Aspect => Viewport.Width / Viewport.Height;
    public Matrix4 Projection => Ortho ? new Box(-OrthoSize * 0.5f, OrthoSize * 0.5f).ToOrthoMatrix(Near, Far) : Matrix4.CreatePerspectiveFieldOfView(FOV, Aspect, Near, Far);
    private Camera() { }
    public static Camera CreateOrthoGraphic(bool width, float size, float near = 0, float far = 1)
    {
        Camera camera = new();
        camera.Ortho = true;
        camera.Size = size;
        camera.Width = width;
        camera.Near = near;
        camera.Far = far;
        return camera;
    }
    public static Camera CreatePerspective(float fov, float near = 0.01f, float far = 100f)
    {
        Camera camera = new();
        camera.Ortho = false;
        camera.FOV = fov;
        camera.Near = near;
        camera.Far = far;
        return camera;
    }
}

public static class Helper
{
    private static Engine Instance = null!;
    public static void Init(Engine engine)
    {
        if (Instance != null)
            throw new InvalidOperationException("Engine helper already initialized.");
        Instance = engine;
    }
    public static Entity CameraEntity => Instance.Camera;
    public static float Time => Instance.Time;
    public static float DeltaTime => Instance.DeltaTime;
    public static Box Viewport => Instance.Viewport;
    public static string Title
    {
        get => Instance.Title;
        set => Instance.Title = value;
    }
    public static Camera CameraProjection = Patchwork.Camera.CreateOrthoGraphic(false, 16);
    public static void Close() => Instance.Close();
    public static string Clipboard
    {
        get => Instance.ClipboardString;
        set => Instance.ClipboardString = value;
    }
    private static unsafe byte[] PtrToBytes(IntPtr ptr, int length)
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
