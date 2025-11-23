using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Graphics.OpenGL4;
using Patchwork.FileSystem;
using System.Diagnostics;
namespace Patchwork;

public class Engine
{
#if HEADLESS
    public class Game
    {
        public Engine Engine { get; init; }
        public Game() => Engine = new() { Env = this, Headless = true };

        public void Run()
        {
            try
            {
                InputState.Update(default, default, default, default, default, default, default, default, default, default, default, default, (OpenTK.Windowing.GraphicsLibraryFramework.KeyboardState)Activator.CreateInstance(typeof(OpenTK.Windowing.GraphicsLibraryFramework.KeyboardState), true)!);
                Engine.LoadSync();
                _ = Task.Run(() => Engine.LoadHeadlessAsync());
                Stopwatch sw = new();
                double last = sw.Elapsed.TotalSeconds;
                while (true)
                {
                    double elapsed = sw.Elapsed.TotalSeconds;
                    Engine.Update(elapsed - last);
                    last = elapsed;
                }
            }
            catch (CloseException)
            {
                Console.WriteLine("Engine forcefully closed.");
            }
            finally
            {
                Engine.Unload();
            }
        }
    }
#else
    public class Game : GameWindow
    {
        public Box2i Viewport { get; private set; }
        public Engine Engine { get; init; }
        public Game() : base(GameWindowSettings.Default, NativeWindowSettings.Default) => Engine = new() { Env = this };
        protected override void OnLoad()
        {
            base.OnLoad(); 
            try
            {
                Engine.LoadSync();
                _ = Task.Run(() => Engine.LoadHeadlessAsync());
                _ = Task.Run(() => Engine.LoadHeadAsync());
            }
            catch (CloseException)
            {
                Close();
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
        protected override void OnUpdateFrame(FrameEventArgs args)
        {
            base.OnUpdateFrame(args);

            InputState.Update(MouseState.Position, MouseState.PreviousPosition, MouseState.Delta, GetButtonState(MouseButton.Left), GetButtonState(MouseButton.Right), GetButtonState(MouseButton.Middle), GetButtonState(MouseButton.Button4), GetButtonState(MouseButton.Button5), MouseState.Scroll, MouseState.PreviousScroll, MouseState.ScrollDelta, Viewport.Size.Y, KeyboardState);
            try
            {
                Engine.Update(args.Time);
            }
            catch (CloseException)
            {
                Close();
            }
        }

        protected override void OnResize(ResizeEventArgs args)
        {
            base.OnResize(args);
            Viewport = new Box2i(0, 0, FramebufferSize.X, FramebufferSize.Y);
        }
        protected override void OnRenderFrame(FrameEventArgs args)
        {
            base.OnRenderFrame(args);
            try
            {
                Engine.Render(Viewport);
            }
            catch (CloseException)
            {
                Close();
            }
            SwapBuffers();
        }
        protected override void OnUnload()
        {
            base.OnUnload();
            Engine.Unload();
        }
    }
#endif
    private static Engine InstanceInternal = null!;
    public Entity Camera = null!;
    public Matrix4 CameraProjection = Matrix4.Identity;
    public float DeltaTime { get; private set; }
    public float Time => (float)TimeDouble;
    private double TimeDouble = 0;
    public required Game Env { get; init; }
    public bool Loaded = false;
    public int LoadingError = 0;
    public int LoadedAsync = 0;
    public bool Headless { get; init; } = false;
    public Engine()
    {
    }
    public void LoadSync()
    {
        Console.WriteLine("Loading...");
        if (InstanceInternal != null)
            throw new InvalidOperationException("Engine already initialized.");
        InstanceInternal = this;
        Init(this);
    }
    private void LoadHeadlessAsync()
    {
        try
        {
            Stopwatch stopwatch = new();
            stopwatch.Start();
            DriveMounts.Mount("C", new PhysicalFileSystem("."));
            DriveMounts.Mount("A", new ZipFileSystem(Path.Combine(AppContext.BaseDirectory, "Assets.zip")));
            IncludedFiles.Init();

            stopwatch.Stop();
            Console.WriteLine($"Loading async head took {stopwatch.ElapsedMilliseconds}ms.");
            Interlocked.Increment(ref LoadedAsync);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Engine load failed: " + ex);
            Interlocked.And(ref LoadingError, 1);
        }
    }
    private void LoadHeadAsync()
    {
        try
        {

            Stopwatch stopwatch = new();
            stopwatch.Start();
            AudioPlayer.Init();

            stopwatch.Stop();
            Console.WriteLine($"Loading async headless took {stopwatch.ElapsedMilliseconds}ms.");
            Interlocked.Increment(ref LoadedAsync);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Engine load failed: " + ex);
            Interlocked.And(ref LoadingError, 1);
        }
    }
    public void Update(double dt)
    {
        if (LoadingError == 1) Close();
        DeltaTime = (float)dt;
        TimeDouble += DeltaTime;
        if (Loaded) World.TickAll();
        else if (LoadedAsync == 2)
        {
            Stopwatch stopwatch = new();
            if (!Headless)
            {
                UIRenderer.Init();
            }
            stopwatch.Stop();
            Console.WriteLine($"Loading sync took {stopwatch.ElapsedMilliseconds}ms.");
            Loaded = true;
        }
    }
    public void Render(Box2i Viewport)
    {
        GL.Viewport(Viewport.Min.X, Viewport.Min.Y, Viewport.Size.X, Viewport.Size.Y);
        if (Loaded)
        {
            UIRenderer.Flush();
        }
        else
        {
            double darken = 1 / (TimeDouble + 1);
            float opposite = (float)(1 - darken);
            GL.ClearColor(opposite, opposite, opposite, 1);
            GL.Clear(ClearBufferMask.ColorBufferBit);
        }
    }
    public void Unload()
    {
        Console.WriteLine("Unloading...");
        Entrypoint.Close();
        Quad.Dispose();
        TextureFactory.DisposeAll();

        UIRenderer.Dispose();
        AudioPlayer.DisposeAll();
    }
    public class CloseException() : Exception("Engine is closed.");
    public void Close() => throw new CloseException();
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
    public static float Time => Instance.Time;
    public static float DeltaTime => Instance.DeltaTime;
    public static void Close() => Instance.Close();
}
