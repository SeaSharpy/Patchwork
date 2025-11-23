namespace Patchwork.Input;

public static class InputState
{
    public static void Update
    (
        Vector2 position,
        Vector2 previousPosition,
        Vector2 delta,
        ButtonState leftButtonDown,
        ButtonState rightButtonDown,
        ButtonState middleButtonDown,
        ButtonState button4Down,
        ButtonState button5Down,
        Vector2 scroll,
        Vector2 previousScroll,
        Vector2 deltaScroll,
        float Height,
        OpenTK.Windowing.GraphicsLibraryFramework.KeyboardState state
    )
    {
        MouseStateInternal.Position = new Vector2(position.X, Height - position.Y);
        MouseStateInternal.PreviousPosition = new Vector2(previousPosition.X, Height - previousPosition.Y);
        MouseStateInternal.Delta = new Vector2(delta.X, -delta.Y);
        MouseStateInternal.LeftButtonDown = leftButtonDown;
        MouseStateInternal.RightButtonDown = rightButtonDown;
        MouseStateInternal.MiddleButtonDown = middleButtonDown;
        MouseStateInternal.Button4Down = button4Down;
        MouseStateInternal.Button5Down = button5Down;
        MouseStateInternal.Scroll = scroll;
        MouseStateInternal.PreviousScroll = previousScroll;
        MouseStateInternal.DeltaScroll = deltaScroll;
        KeyboardStateInternal.InternalState = state;
    }
}

file static class MouseStateInternal
{
    public static Vector2 Position;
    public static Vector2 PreviousPosition;
    public static Vector2 Delta;
    public static ButtonState LeftButtonDown;
    public static ButtonState RightButtonDown;
    public static ButtonState MiddleButtonDown;
    public static ButtonState Button4Down;
    public static ButtonState Button5Down;
    public static Vector2 Scroll;
    public static Vector2 PreviousScroll;
    public static Vector2 DeltaScroll;
}

public static class MouseState
{
    public static Vector2 Position => MouseStateInternal.Position;
    public static Vector2 PreviousPosition => MouseStateInternal.PreviousPosition;
    public static Vector2 Delta => MouseStateInternal.Delta;
    public static ButtonState LeftButton => MouseStateInternal.LeftButtonDown;
    public static ButtonState RightButton => MouseStateInternal.RightButtonDown;
    public static ButtonState MiddleButton => MouseStateInternal.MiddleButtonDown;
    public static ButtonState Button4 => MouseStateInternal.Button4Down;
    public static ButtonState Button5 => MouseStateInternal.Button5Down;
    public static Vector2 Scroll => MouseStateInternal.Scroll;
    public static Vector2 PreviousScroll => MouseStateInternal.PreviousScroll;
    public static Vector2 DeltaScroll => MouseStateInternal.DeltaScroll;
    public static float X => MouseStateInternal.Position.X;
    public static float Y => MouseStateInternal.Position.Y;
}
file static class KeyboardStateInternal
{
    public static OpenTK.Windowing.GraphicsLibraryFramework.KeyboardState InternalState = null!;
}

public static class KeyboardState
{
    public static bool IsKeyDown(Key key)
    {
        return KeyboardStateInternal.InternalState.IsKeyDown((OpenTK.Windowing.GraphicsLibraryFramework.Keys)key);
    }
    public static bool IsKeyPressed(Key key)
    {
        return KeyboardStateInternal.InternalState.IsKeyPressed((OpenTK.Windowing.GraphicsLibraryFramework.Keys)key);
    }
    public static bool IsKeyReleased(Key key)
    {
        return KeyboardStateInternal.InternalState.IsKeyReleased((OpenTK.Windowing.GraphicsLibraryFramework.Keys)key);
    }
    public sealed class KeyboardDownIndexer
    {
        public bool this[Key key]
            => IsKeyDown(key);
    }

    public sealed class KeyboardPressedIndexer
    {
        public bool this[Key key]
            => IsKeyPressed(key);
    }

    public sealed class KeyboardReleasedIndexer
    {
        public bool this[Key key]
            => IsKeyReleased(key);
    }
    public static readonly KeyboardDownIndexer Down = new KeyboardDownIndexer();
    public static readonly KeyboardPressedIndexer Pressed = new KeyboardPressedIndexer();
    public static readonly KeyboardReleasedIndexer Released = new KeyboardReleasedIndexer();
}
