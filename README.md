# Patchwork

A free and open source game engine/framework. Work in progress. Don't expect documentation any time soon.

If you really care, make a folder called Game with a subfolder called CS, put your code in there, create a Program.cs (inside the CS folder) that looks something like this:

```
using Patchwork;
using OpenTK.Windowing.Desktop;
var gameSettings = GameWindowSettings.Default;
var windowSettings = NativeWindowSettings.Default;
using var game = new Engine(gameSettings, windowSettings);
game.Run();
```

and create an Entrypoint.cs which looks something like this:

```
using Patchwork.Engine;
using Patchwork.Render;

public static class Entrypoint
{
    public static void Init()
    {
        Engine.Instance.Title = "Game!";
    }
    public static IRenderSystem Renderer()
    {
	MyRenderSystem system = new();
        ECS.RegisterSystem(system);
        return system;
    }
    public static void Close()
    {

    }
}
```

no more info. figure out the rest yourself.
