public class CameraEntity : Entity
{
    // Yaw   - rotation around the Y axis (left/right)
    // Pitch - rotation around the X axis (up/down)
    private float Yaw;
    private float Pitch;

    // Tweak these to taste
    private float MoveSpeed = 10f;
    private float MouseSensitivity = 0.0025f;

    private CameraEntity() : base()
    {
        Camera = ID;
    }

    public override void Client()
    {
        UpdateLook();
        UpdateMovement();
    }

    private void UpdateLook()
    {
        if (MouseState.LeftButton == ButtonState.Down)
        {
            // Mouse delta in pixels since last frame
            Vector2 delta = MouseState.Delta;

            // Horizontal mouse movement controls yaw (left/right)
            Yaw += delta.X * MouseSensitivity;

            // Vertical mouse movement controls pitch (up/down)
            // Your InputState already flips Y so positive delta.Y is "move mouse up".
            Pitch += delta.Y * MouseSensitivity;

            // Clamp pitch so we cannot flip over
            const float pitchLimit = 1.553343f; // ~89 degrees in radians
            if (Pitch > pitchLimit) Pitch = pitchLimit;
            if (Pitch < -pitchLimit) Pitch = -pitchLimit;

            // Build rotation quaternion from pitch (X) and yaw (Y)
            // OpenTK's FromEulerAngles expects radians around X, Y, Z
            Rotation = Quaternion.CreateFromYawPitchRoll(Yaw, Pitch, 0f);
        }
    }

    private void UpdateMovement()
    {
        // Forward is -Z in typical OpenGL style
        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, Rotation);
        Vector3 right = Vector3.Transform(Vector3.UnitX, Rotation);

        // We usually do not want vertical component in strafing/forward movement
        forward.Y = 0f;
        right.Y = 0f;

        if (forward.LengthSquared() > 0f)
            forward = Vector3.Normalize(forward);

        if (right.LengthSquared() > 0f)
            right = Vector3.Normalize(right);

        Vector3 move = Vector3.Zero;

        // Basic WASD
        if (KeyboardState.Down[Keys.W]) move += forward;
        if (KeyboardState.Down[Keys.S]) move -= forward;
        if (KeyboardState.Down[Keys.D]) move += right;
        if (KeyboardState.Down[Keys.A]) move -= right;

        // Vertical movement (fly up/down)
        if (KeyboardState.Down[Keys.Space]) move += Vector3.UnitY;
        if (KeyboardState.Down[Keys.LeftControl]) move -= Vector3.UnitY;

        if (move.LengthSquared() <= 0f)
            return;

        move = Vector3.Normalize(move);

        // Simple sprint modifier
        float speed = MoveSpeed;
        if (KeyboardState.Down[Keys.LeftShift])
            speed *= 2f;

        // If you have a delta time available in your engine, replace this with:
        // Position += move * speed * DeltaTime;
        Position += move * speed * DeltaTime;
    }
}
