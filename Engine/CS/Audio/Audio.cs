using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using OpenTK.Audio.OpenAL;

namespace Patchwork.Audio;

public sealed class AudioPlayer : IDisposable
{
    public int Buffer { get; private set; }

    // Source is created on the audio thread
    public int Source { get; private set; }

    public bool Looping
    {
        get => LoopingInternal;
        set
        {
            LoopingInternal = value;
            AudioThread.Enqueue(() =>
            {
                if (Source != 0)
                    AL.Source(Source, ALSourceb.Looping, LoopingInternal);
            });
        }
    }

    public float Gain
    {
        get => GainInternal;
        set
        {
            GainInternal = value;
            AudioThread.Enqueue(() =>
            {
                if (Source != 0)
                    AL.Source(Source, ALSourcef.Gain, GainInternal);
            });
        }
    }

    public float Pitch
    {
        get => PitchInternal;
        set
        {
            PitchInternal = value;
            AudioThread.Enqueue(() =>
            {
                if (Source != 0)
                    AL.Source(Source, ALSourcef.Pitch, PitchInternal);
            });
        }
    }

    private bool LoopingInternal = false;
    private float GainInternal = 1f;
    private float PitchInternal = 1f;

    // Cached state, updated only on the audio thread
    private ALSourceState StateInternal = ALSourceState.Stopped;

    public ALSourceState State => StateInternal;

    private static readonly HashSet<AudioPlayer> HandledAudios = new();

    public static void Init()
    {
        AudioThread.Init();
    }

    public AudioPlayer(AudioFile file, bool startPlaying = false)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file), "AudioPlayer requires a valid AudioFile instance.");

        Buffer = file.Buffer;

        AudioThread.Enqueue(() =>
        {
            Source = AL.GenSource();
            AL.Source(Source, ALSourcei.Buffer, Buffer);
            AL.Source(Source, ALSourceb.SourceRelative, true);
            AL.Source(Source, ALSourcef.Gain, GainInternal);
            AL.Source(Source, ALSourcef.Pitch, PitchInternal);
            AL.Source(Source, ALSourceb.Looping, LoopingInternal);
            UpdateSourceState();
        });

        if (startPlaying)
            Play();
    }

    public AudioPlayer(AudioFile file, float pitchMin, float pitchMax, bool startPlaying = false)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file), "AudioPlayer requires a valid AudioFile instance.");

        Buffer = file.Buffer;
        Pitch = Random.Shared.NextSingle() * (pitchMax - pitchMin) + pitchMin;

        AudioThread.Enqueue(() =>
        {
            Source = AL.GenSource();
            AL.Source(Source, ALSourcei.Buffer, Buffer);
            AL.Source(Source, ALSourceb.SourceRelative, true);
            AL.Source(Source, ALSourcef.Gain, GainInternal);
            AL.Source(Source, ALSourcef.Pitch, PitchInternal);
            AL.Source(Source, ALSourceb.Looping, LoopingInternal);
            UpdateSourceState();
        });

        if (startPlaying)
            Play();
    }

    public void Play()
    {
        lock (HandledAudios)
        {
            HandledAudios.Add(this);
        }

        AudioThread.Enqueue(() =>
        {
            if (Source == 0 || Buffer == 0)
                return;

            AL.Source(Source, ALSourcei.Buffer, Buffer);
            AL.Source(Source, ALSourceb.SourceRelative, true);
            AL.SourcePlay(Source);
            UpdateSourceState();
        });
    }

    public void Pause()
    {
        AudioThread.Enqueue(() =>
        {
            if (Source == 0)
                return;

            AL.SourcePause(Source);
            UpdateSourceState();
        });
    }

    public void Stop()
    {
        AudioThread.Enqueue(() =>
        {
            if (Source == 0)
                return;

            AL.SourceStop(Source);
            UpdateSourceState();
        });
    }

    private void UpdateSourceState()
    {
        if (Source == 0)
        {
            StateInternal = ALSourceState.Stopped;
            return;
        }

        AL.GetSource(Source, ALGetSourcei.SourceState, out int s);
        StateInternal = (ALSourceState)s;
    }

    /// <summary>
    /// Called from the game thread each frame.
    /// Removes stopped audios from the handled set, using cached state.
    /// </summary>
    public static void Update()
    {
        lock (HandledAudios)
        {
            foreach (AudioPlayer audio in HandledAudios.ToArray())
            {
                if (audio.State == ALSourceState.Stopped)
                    HandledAudios.Remove(audio);
            }
        }
    }

    ~AudioPlayer()
    {
        Dispose();
    }

    public void Dispose()
    {
        AudioThread.Enqueue(() =>
        {
            if (Source != 0)
            {
                AL.SourceStop(Source);
                AL.Source(Source, ALSourcei.Buffer, 0);
                AL.DeleteSource(Source);
                Source = 0;
                StateInternal = ALSourceState.Stopped;
            }
        });

        lock (HandledAudios)
        {
            HandledAudios.Remove(this);
        }
    }

    public static void DisposeAll()
    {
        lock (HandledAudios)
        {
            foreach (AudioPlayer audio in HandledAudios.ToArray())
                audio.Dispose();

            HandledAudios.Clear();
        }

        AudioThread.Shutdown();
    }

    /// <summary>
    /// Internal helper that owns the OpenAL device/context and runs all AL calls.
    /// </summary>
    private static class AudioThread
    {
        private static readonly ConcurrentQueue<Action> Queue = new();
        private static readonly AutoResetEvent QueueEvent = new(false);
        private static readonly ManualResetEventSlim ReadyEvent = new(false);

        private static Thread? Thread;
        private static bool Running;
        private static bool Initialized;

        private static ALDevice Device;
        private static ALContext Context;

        public static void Init()
        {
            if (Initialized)
                return;

            Initialized = true;
            Running = true;

            Thread = new Thread(ThreadMain)
            {
                IsBackground = true,
                Name = "AudioThread"
            };
            Thread.Start();

            // Wait until device/context are ready
            ReadyEvent.Wait();
        }

        public static void Enqueue(Action action)
        {
            if (!Initialized)
                Init();

            Queue.Enqueue(action);
            QueueEvent.Set();
        }

        public static void Shutdown()
        {
            if (!Initialized)
                return;

            Running = false;
            QueueEvent.Set();

            if (Thread != null && Thread.IsAlive)
                Thread.Join();

            Initialized = false;
        }

        private static void ThreadMain()
        {
            try
            {
                Device = ALC.OpenDevice(null);
                if (Device.Handle == IntPtr.Zero)
                    throw new InvalidOperationException("Failed to open default OpenAL device.");

                Context = ALC.CreateContext(Device, Array.Empty<int>());
                if (Context.Handle == IntPtr.Zero)
                {
                    ALC.CloseDevice(Device);
                    throw new InvalidOperationException("Failed to create OpenAL context.");
                }

                if (!ALC.MakeContextCurrent(Context))
                {
                    ALC.DestroyContext(Context);
                    ALC.CloseDevice(Device);
                    throw new InvalidOperationException("Failed to make OpenAL context current.");
                }

                AL.Listener(ALListener3f.Position, 0f, 0f, 0f);
                AL.DistanceModel(ALDistanceModel.None);

                ReadyEvent.Set();

                while (Running)
                {
                    while (Queue.TryDequeue(out Action? action))
                    {
                        try
                        {
                            action();
                        }
                        catch (Exception ex)
                        {
                            // Replace with your logging if you want
                            Console.WriteLine($"[AudioThread] Exception in audio action: {ex}");
                        }
                    }

                    // Light wait to avoid burning CPU when idle
                    QueueEvent.WaitOne(5);
                }

                // Cleanup OpenAL on this thread
                ALC.MakeContextCurrent(ALContext.Null);
                if (Context.Handle != IntPtr.Zero)
                {
                    ALC.DestroyContext(Context);
                    Context = default;
                }

                if (Device.Handle != IntPtr.Zero)
                {
                    ALC.CloseDevice(Device);
                    Device = default;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AudioThread] Fatal error: {ex}");
                ReadyEvent.Set(); // Unblock Init even on failure
            }
        }
    }
}
