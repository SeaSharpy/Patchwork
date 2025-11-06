using OpenTK.Audio.OpenAL;
namespace Patchwork.Audio;

public sealed class AudioPlayer : IDisposable
{
    public int Buffer { get; private set; }
    public int Source { get; private set; } = AL.GenSource();
    public bool Looping
    {
        get => LoopingInternal;
        set { LoopingInternal = value; AL.Source(Source, ALSourceb.Looping, LoopingInternal); }
    }

    public float Gain
    {
        get => GainInternal;
        set { GainInternal = value; AL.Source(Source, ALSourcef.Gain, GainInternal); }
    }

    public float Pitch
    {
        get => PitchInternal;
        set { PitchInternal = value; AL.Source(Source, ALSourcef.Pitch, PitchInternal); }
    }

    bool LoopingInternal = false;
    float GainInternal = 1f;
    float PitchInternal = 1f;
    private static ALDevice Device;
    private static ALContext Context;
    public static void Init()
    {
        Device = ALC.OpenDevice(null);
        if (Device.Handle == IntPtr.Zero)
            throw new InvalidOperationException("Failed to open default OpenAL device.");

        Context = ALC.CreateContext(Device, [0]);
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
    }
    public AudioPlayer(AudioFile file, bool startPlaying = false)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));
        Buffer = file.Buffer;
        AL.Source(Source, ALSourcei.Buffer, Buffer);

        if (startPlaying)
            Play();
    }
    public AudioPlayer(AudioFile file, float pitchMin, float pitchMax, bool startPlaying = false)
    {
        if (file == null)
            throw new ArgumentNullException(nameof(file));
        Buffer = file.Buffer;
        AL.Source(Source, ALSourcei.Buffer, Buffer);
        Pitch = Random.Shared.NextSingle() * (pitchMax - pitchMin) + pitchMin;

        if (startPlaying)
            Play();
    }

    public void Play()
    {
        AL.Source(Source, ALSourcei.Buffer, Buffer);
        AL.Source(Source, ALSourceb.SourceRelative, true);
        AL.SourcePlay(Source);
        HandledAudios.Add(this);
    }

    public void Pause()
    {
        AL.SourcePause(Source);
    }

    public void Stop()
    {
        AL.SourceStop(Source);
    }

    public ALSourceState State
    {
        get
        {
            AL.GetSource(Source, ALGetSourcei.SourceState, out int s);
            return (ALSourceState)s;
        }
    }
    private static readonly HashSet<AudioPlayer> HandledAudios = new();
    public static void Update()
    {
        foreach (AudioPlayer? audio in HandledAudios.ToArray())
            if (audio.State == ALSourceState.Stopped)
                HandledAudios.Remove(audio);
    }
    ~AudioPlayer()
    {
        Dispose();
    }
    public void Dispose()
    {
        try
        {
            if (Source != 0)
            {
                AL.SourceStop(Source);
                AL.Source(Source, ALSourcei.Buffer, 0);
                AL.DeleteSource(Source);
                Source = 0;
            }
        }
        finally
        {
            if (Buffer != 0)
                Buffer = 0;
        }
    }
    public static void DisposeAll()
    {
        foreach (AudioPlayer? audio in HandledAudios.ToArray())
            audio.Dispose();
        HandledAudios.Clear();
        ALC.DestroyContext(Context);
        ALC.CloseDevice(Device);
    }
}
