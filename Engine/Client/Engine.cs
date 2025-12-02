using OpenTK.Graphics.OpenGL4;
using TKMouseButton = OpenTK.Windowing.GraphicsLibraryFramework.MouseButton;
namespace Patchwork;

public partial class Engine
{
    public OpenTK.Windowing.Desktop.NativeWindow Window;
    public void ClientLoad()
    {
        CameraEntity cameraEntity = (CameraEntity)Entity.Create(typeof(CameraEntity));
        cameraEntity.Name = "Camera";
        cameraEntity.Position = new Vector3(0, 0, 0);
        cameraEntity.Connections = [];
        GameClient.Connect("127.0.0.1", 4000, "Walt");
        DriveMounts.Mount("A", new HttpFileSystem("http://localhost:4001/"));
        Entity.SetupPackets();
    }
    public void ClientUnload()
    {
        Renderer.Dispose();
    }
    public int Loading = 0;
    private ButtonState GetButtonState(TKMouseButton button)
    {
        if (Window.MouseState.IsButtonPressed(button))
            return ButtonState.Pressed;
        if (Window.MouseState.IsButtonReleased(button))
            return ButtonState.Released;
        if (Window.MouseState.IsButtonDown(button))
            return ButtonState.Down;
        return ButtonState.Up;
    }
    public void UpdateExtrasPre()
    {
        InputState.Update(
            (Vector2)Window.MouseState.Position,
            (Vector2)Window.MouseState.PreviousPosition,
            (Vector2)Window.MouseState.Delta,
            GetButtonState(TKMouseButton.Left),
            GetButtonState(TKMouseButton.Right),
            GetButtonState(TKMouseButton.Middle),
            GetButtonState(TKMouseButton.Button4),
            GetButtonState(TKMouseButton.Button5),
            (Vector2)Window.MouseState.Scroll,
            (Vector2)Window.MouseState.PreviousScroll,
            (Vector2)Window.MouseState.ScrollDelta,
            Window.KeyboardState
        );
    }
    public void UpdateExtrasPost()
    {

    }
    public void Render(Vector2 size)
    {
        GL.Viewport(0, 0, (int)size.X, (int)size.Y);
        if (FrameGraph.Build())
        {
            Loading = 0;
            Renderer.Render((TKVector2)size);
        }
        else
        {
            float value = Loading++;
            float color = 1f - (1f / (1f + value));
            GL.ClearColor(color, color, color, 1);
            GL.Clear(ClearBufferMask.ColorBufferBit);
        }
    }
}