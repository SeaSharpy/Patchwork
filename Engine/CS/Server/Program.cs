using System.Diagnostics;
public partial class Program
{
    public static void Main()
    {
        try
        {
            Engine engine = new();
            Console.TreatControlCAsInput = true;
            WriteLine("Starting server.");
            GameServer server = new();
            server.PacketReceived += (playerName, packetType, reader) =>
            {
                if (packetType == 0)
                {
                    string log = reader.ReadString();
                    WriteLine(log);
                }
            };
            _ = server.StartAsync(4000);
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