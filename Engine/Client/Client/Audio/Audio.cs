global using Patchwork.Client.Audio;
namespace Patchwork.Client.Audio;

public sealed class AudioPlayer : IDisposable
{
    public bool Looping
    {
        get => LoopingInternal;
        set
        {
            LoopingInternal = value;
        }
    }

    public float Gain
    {
        get => GainInternal;
        set
        {
            GainInternal = value;
        }
    }

    public float Pitch
    {
        get => PitchInternal;
        set
        {
            PitchInternal = value;
        }
    }

    private bool LoopingInternal = false;
    private float GainInternal = 1f;
    private float PitchInternal = 1f;
    private static readonly HashSet<AudioPlayer> HandledAudios = new();
    public static void Init()
    {
        
    }

    public AudioPlayer(AudioFile file, bool startPlaying = false)
    {
        if (startPlaying)
            Play();
    }

    public AudioPlayer(AudioFile file, float pitchMin, float pitchMax, bool startPlaying = false)
    {
        Pitch = Random.Shared.NextSingle() * (pitchMax - pitchMin) + pitchMin;

        if (startPlaying)
            Play();
    }

    public void Play()
    {
        lock (HandledAudios)
        {
            HandledAudios.Add(this);
        }


    }

    public void Pause()
    {
        
    }

    public void Stop()
    {

    }
    
    public static void Update()
    {
        lock (HandledAudios)
        {
            foreach (AudioPlayer audio in HandledAudios.ToArray())
            {
                
            }
        }
    }

    ~AudioPlayer()
    {
        Dispose();
    }

    public void Dispose()
    {
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
    }
}
