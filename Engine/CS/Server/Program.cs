using System.Diagnostics;
public partial class Program
{
    public static async Task Main()
    {
        try
        {
            Engine engine = new();
            Console.TreatControlCAsInput = true;
            WriteLine("Starting server.");
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
                try
                {
                    WriteLine("Engine server-load.");
                    engine.ServerLoad();
                }
                catch (Exception ex)
                {
                    WriteLine("Engine server-load failed: " + ex);
                    goto ErrorA;
                }
                Stopwatch stopwatch = Stopwatch.StartNew();
                long lastTicks = stopwatch.ElapsedTicks;
                double tickFrequency = 1.0 / Stopwatch.Frequency;
                while (true)
                {
                    long currentTicks = stopwatch.ElapsedTicks;
                    long deltaTicks = currentTicks - lastTicks;
                    lastTicks = currentTicks;

                    double deltaTime = deltaTicks * tickFrequency;

                    try
                    {
                        engine.Update(deltaTime);
                    }
                    catch (Engine.CloseException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        WriteLine("Engine update failed: " + ex);
                        goto ErrorA;
                    }
                    if (Console.KeyAvailable)
                    {
                        ConsoleKeyInfo key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.C && key.Modifiers == ConsoleModifiers.Control)
                            break;
                    }
                }
            ErrorA:;
                try
                {
                    WriteLine("Engine server-unload.");
                    engine.ServerUnload();
                }
                catch (Exception ex)
                {
                    WriteLine("Engine server-unload failed: " + ex);
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
            GameServer.Dispose();
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