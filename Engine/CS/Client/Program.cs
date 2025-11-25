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
            GameClient client = new GameClient();
            client.PacketReceived += (packetType, reader) =>
            {
                
            };

            client.Connect("127.0.0.1", 4000, "Walt");
            try
            {
                WriteLine("Engine nowindow-load.");
                engine.NoWindowLoad();
            }
            catch (Exception ex)
            {
                WriteLine("Engine nowindow-load failed: " + ex);
                goto ErrorB;
            }
            {
                using NativeWindow window = new(nativeWindowSettings);
                window.Context.MakeCurrent();
                window.Context.SwapInterval = 1;
                try
                {
                    WriteLine("Engine window-load.");
                    engine.WindowLoad();
                }
                catch (Exception ex)
                {
                    WriteLine("Engine window-load failed: " + ex);
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
                    try
                    {
                        engine.Update(deltaTime);
                        engine.Render();
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
                    WriteLine("Engine window-unload.");
                    engine.WindowUnload();
                }
                catch (Exception ex)
                {
                    WriteLine("Engine window-unload failed: " + ex);
                    throw new Engine.BubbleException();
                }
            }
        ErrorB:;
            try
            {
                WriteLine("Engine nowindow-unload.");
                engine.NoWindowUnload();
            }
            catch (Exception ex)
            {
                WriteLine("Engine nowindow-unload failed: " + ex);
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