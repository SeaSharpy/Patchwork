using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Graphics.OpenGL4;
using Monitor = OpenTK.Windowing.GraphicsLibraryFramework.Monitor;
using Patchwork.FileSystem;
using System.Diagnostics;
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
    public IRenderSystem? Renderer = null;
    private double TimeDouble = 0;
    private bool OnTop = false;
    public event Action? PostRender;
    private Task? LoadTask;
    private volatile bool IsEngineLoaded;
    private volatile Exception? LoadException;
    public int LoadingState = 0;
    public Engine(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings, bool onTop = false) : base(gameWindowSettings, nativeWindowSettings)
    {
        OnTop = onTop;
    }
    Stopwatch Stopwatch = new();
    protected override void OnLoad()
    {
        Stopwatch.Start();
        Console.WriteLine("Loading...");
        if (InstanceInternal != null)
            throw new InvalidOperationException("Engine already initialized.");
        InstanceInternal = this;
        base.OnLoad();
        Init(this);
        LoadTask = Task.Run(() => InitializeEngineBackground());
    }
    private void InitializeEngineBackground()
    {
        try
        {
            DriveMounts.Mount("C", new PhysicalFileSystem("."));
            DriveMounts.Mount("A", new ZipFileSystem(Path.Combine(AppContext.BaseDirectory, "Assets.zip")));
            ECS = new();
            Camera = new("Camera", [], "Camera");
            IncludedFiles.Init();
            AudioPlayer.Init();

            Stopwatch.Stop();
            Console.WriteLine($"Loading part one took {Stopwatch.ElapsedMilliseconds}ms.");
            Stopwatch.Restart();
            Interlocked.Increment(ref LoadingState);
        }
        catch (Exception ex)
        {
            LoadException = ex;
            Console.WriteLine("Engine load failed: " + ex);

        }
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
        if (LoadingState == 2) ECS.Update();
        if (LoadingState == 1)
        {
            UIRenderer.Init();
            Entrypoint.Init();
            Stopwatch.Stop();
            Console.WriteLine($"Loading part two took {Stopwatch.ElapsedMilliseconds}ms.");
            Interlocked.Increment(ref LoadingState);
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
        GL.Viewport((int)Viewport.X, (int)Viewport.Y, (int)Viewport.Width, (int)Viewport.Height);
        if (LoadingState == 2)
        {
            if (Renderer == null)
            {
                Renderer = Entrypoint.Renderer();
            }
            ECS.Render();
            PostRender?.Invoke();
            UIRenderer.Flush();
        }
        else
        {
            double darken = 1 / (TimeDouble + 1);
            float opposite = (float)(1 - darken);
            GL.ClearColor(opposite, opposite, opposite, 1);
            GL.Clear(ClearBufferMask.ColorBufferBit);
        }
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
            return new(offset - OrthoSize * 0.5f, OrthoSize);
        }
    }

    public float Size;
    public bool Width;

    // If Width is true, Size is the width, so height = width / aspect.
    // If Width is false, Size is the height, so width = height * aspect.
    public float OrthoWidth => Width ? Size : Size * Aspect;
    public float OrthoHeight => Width ? Size / Aspect : Size;

    public Vector2 OrthoSize => new(OrthoWidth, OrthoHeight);

    public float Near = 0, Far = 1;
    public float FOV;

    // Ensure floating point division and guard against zero height during resize.
    public float Aspect
    {
        get
        {
            float h = Viewport.Height;
            return h > 0 ? Viewport.Width / (float)h : 1f;
        }
    }

    public Matrix4 Projection
    {
        get
        {
            Matrix4 projection = Ortho
                ? Matrix4.CreateOrthographic(OrthoWidth, OrthoHeight, Near, Far)
                : Matrix4.CreatePerspectiveFieldOfView(FOV, Aspect, Near, Far);

            Matrix4 view = CameraEntity.TransformMatrix.Inverted();
            return view * projection;
        }
    }

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
    public static Camera CameraProjection = Camera.CreateOrthoGraphic(false, 8);
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
        get => Instance.Renderer!;
        set => Instance.Renderer = value;
    }
}
