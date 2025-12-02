using OpenTK.Windowing.Common;
using System.Diagnostics;
using OpenTK.Windowing.Desktop;
public partial class Program
{
    public static void Main()
    {
        try
        {
            
            Engine engine = new();
            NativeWindowSettings nativeWindowSettings = new()
            {
                Title = "Loading...",
                API = ContextAPI.OpenGL,
                Profile = ContextProfile.Core,
                APIVersion = new Version(4, 6),
                Flags = ContextFlags.ForwardCompatible
            };
            WriteLine("Starting client.");
            try
            {
                WriteLine("Engine common-load.");
                engine.CommonLoad();
            }
            catch (Exception ex)
            {
                WriteLine("Engine common-load failed: " + ex);
                goto ErrorB;
            }
            {
                using NativeWindow window = new(nativeWindowSettings);
                window.Context.MakeCurrent();
                window.Context.SwapInterval = 1;
                engine.Window = window;
                try
                {
                    WriteLine("Engine client-load.");
                    engine.ClientLoad();
                }
                catch (Exception ex)
                {
                    WriteLine("Engine client-load failed: " + ex);
                    goto ErrorA;
                }
                Stopwatch stopwatch = Stopwatch.StartNew();
                long lastTicks = stopwatch.ElapsedTicks;
                double tickFrequency = 1.0 / Stopwatch.Frequency;
                while (!window.IsExiting)
                {
                    long currentTicks = stopwatch.ElapsedTicks;
                    long deltaTicks = currentTicks - lastTicks;
                    lastTicks = currentTicks;

                    double deltaTime = deltaTicks * tickFrequency;
                    window.ProcessEvents(0);
                    window.Title = "FPS: " + (int)(1.0 / deltaTime);
                    try
                    {
                        engine.Update(deltaTime);
                        engine.Render((Vector2)new TKVector2(window.FramebufferSize.X, window.FramebufferSize.Y));
                    }
                    catch (Engine.CloseException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        WriteLine("Engine update/render failed: " + ex);
                        goto ErrorA;
                    }
                    window.Context.SwapBuffers();
                }
            ErrorA:;
                try
                {
                    WriteLine("Engine client-unload.");
                    engine.ClientUnload();
                }
                catch (Exception ex)
                {
                    WriteLine("Engine client-unload failed: " + ex);
                    throw new Engine.BubbleException();
                }
            }
        ErrorB:;
            try
            {
                WriteLine("Engine common-unload.");
                engine.CommonUnload();
            }
            catch (Exception ex)
            {
                WriteLine("Engine common-unload failed: " + ex);
                throw new Engine.BubbleException();
            }
        }
        catch (Engine.BubbleException)
        {
            WriteLine("Bubbled up.");
        }
        catch (Exception ex)
        {
            WriteLine("Engine failed: " + ex);
        }
        finally
        {
            WriteLine("Done.");
        }
    }
}