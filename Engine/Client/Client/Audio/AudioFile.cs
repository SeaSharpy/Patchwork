using System.Text;
namespace Patchwork.Client.Audio;

public sealed class AudioFile : IDisposable
{
    public float[] Samples { get; }
    public int SampleRate { get; }
    public bool Stereo { get; }

    public int Channels => Stereo ? 2 : 1;
    public int Frames => Samples.Length / Channels;
    public AudioFile(float[] samples, int sampleRate, bool stereo)
    {
        if (samples == null) throw new ArgumentNullException("samples", "AudioFile requires a valid samples buffer.");
        if (sampleRate <= 0) throw new ArgumentOutOfRangeException("sampleRate", sampleRate, "Sample rate must be greater than zero.");
        if (samples.Length == 0) throw new ArgumentException("Audio samples array must contain data.", "samples");
        if (samples.Length % (stereo ? 2 : 1) != 0)
            throw new ArgumentException("Samples length must be a multiple of the channel count.", "samples");

        Samples = samples;
        SampleRate = sampleRate;
        Stereo = stereo;
    }
    public static AudioFile FromWav(string path)
    {
        using FileStream fs = DriveMounts.FileStream(path);
        return FromWav(fs);
    }

    public static AudioFile FromWav(Stream stream)
    {
        if (stream == null) throw new ArgumentNullException("stream", "Source stream cannot be null when loading WAV audio.");
        using BinaryReader br = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        string riff = new string(br.ReadChars(4));
        if (riff != "RIFF") throw new InvalidDataException("Not a RIFF file.");
        br.ReadInt32();
        string wave = new string(br.ReadChars(4));
        if (wave != "WAVE") throw new InvalidDataException("Not a WAVE file.");

        short audioFormat = 0;
        short channels = 0;
        int sampleRate = 0;
        short bitsPerSample = 0;
        int byteRate = 0;
        short blockAlign = 0;

        byte[]? dataChunk = null;

        while (br.BaseStream.Position + 8 <= br.BaseStream.Length)
        {
            string chunkId = new string(br.ReadChars(4));
            int chunkSize = br.ReadInt32();

            if (chunkSize < 0 || br.BaseStream.Position + chunkSize > br.BaseStream.Length)
                throw new InvalidDataException("Corrupted WAV chunk size.");

            if (chunkId == "fmt ")
            {
                audioFormat = br.ReadInt16();
                channels = br.ReadInt16();
                sampleRate = br.ReadInt32();
                byteRate = br.ReadInt32();
                blockAlign = br.ReadInt16();
                bitsPerSample = br.ReadInt16();

                int fmtRead = 16;
                if (chunkSize > fmtRead)
                    br.BaseStream.Position += (chunkSize - fmtRead);
            }
            else if (chunkId == "data")
                dataChunk = br.ReadBytes(chunkSize);
            else
                br.BaseStream.Position += chunkSize;

            if ((chunkSize & 1) == 1 && br.BaseStream.Position < br.BaseStream.Length)
                br.BaseStream.Position += 1;
        }

        if (channels is < 1 or > 2)
            throw new NotSupportedException("Only mono or stereo WAV files are supported.");

        if (dataChunk == null)
            throw new InvalidDataException("WAV file has no data chunk.");

        float[] samples = audioFormat switch
        {
            1 => PcmToFloat(dataChunk, bitsPerSample, channels),
            3 => FloatToFloat(dataChunk, channels),
            _ => throw new NotSupportedException($"Unsupported WAV audio format code: {audioFormat}")
        };

        bool stereo = channels == 2;
        return new AudioFile(samples, sampleRate, stereo);
    }
    private static float[] PcmToFloat(byte[] bytes, int bitsPerSample, int channels)
    {
        if (bitsPerSample != 8 && bitsPerSample != 16 && bitsPerSample != 24 && bitsPerSample != 32)
            throw new NotSupportedException($"Unsupported PCM bit depth: {bitsPerSample}");

        int frameSizeBytes = bitsPerSample / 8 * channels;
        if (frameSizeBytes <= 0 || bytes.Length % frameSizeBytes != 0)
            throw new InvalidDataException("Data chunk size is not aligned to frames.");

        int frames = bytes.Length / frameSizeBytes;
        float[] outSamples = new float[frames * channels];

        int bi = 0;
        int si = 0;

        if (bitsPerSample == 8)
        {
            for (int i = 0; i < frames * channels; i++)
            {
                byte u = bytes[bi++];
                outSamples[si++] = (u - 128) / 128f;
            }
        }
        else if (bitsPerSample == 16)
        {
            for (int i = 0; i < frames * channels; i++)
            {
                short s = (short)(bytes[bi] | (bytes[bi + 1] << 8));
                bi += 2;
                outSamples[si++] = s / 32767f;
            }
        }
        else if (bitsPerSample == 24)
        {
            for (int i = 0; i < frames * channels; i++)
            {
                int b0 = bytes[bi++];
                int b1 = bytes[bi++];
                int b2 = bytes[bi++];
                int sample = (b2 << 24) | (b1 << 16) | (b0 << 8);
                sample >>= 8;
                outSamples[si++] = Math.Clamp(sample / 8388607f, -1f, 1f);
            }
        }
        else
        {
            for (int i = 0; i < frames * channels; i++)
            {
                int s =
                    (bytes[bi]) |
                    (bytes[bi + 1] << 8) |
                    (bytes[bi + 2] << 16) |
                    (bytes[bi + 3] << 24);
                bi += 4;
                outSamples[si++] = Math.Clamp(s / 2147483647f, -1f, 1f);
            }
        }

        return outSamples;
    }

    private static float[] FloatToFloat(byte[] bytes, int channels)
    {
        if (bytes.Length % 4 != 0)
            throw new InvalidDataException("Float data size is not a multiple of 4.");

        int sampleCount = bytes.Length / 4;
        float[] outSamples = new float[sampleCount];

        int bi = 0;
        for (int i = 0; i < sampleCount; i++)
        {
            uint u =
                (uint)(bytes[bi] |
                      (bytes[bi + 1] << 8) |
                      (bytes[bi + 2] << 16) |
                      (bytes[bi + 3] << 24));
            bi += 4;
            float f = BitConverter.Int32BitsToSingle((int)u);
            outSamples[i] = Math.Clamp(f, -1f, 1f);
        }

        return outSamples;
    }
    ~AudioFile()
    {
        Dispose();
    }
    public void Dispose()
    {
        
    }
}
