using NAudio.Vorbis;
using NVorbis;
using SecretLabNAudio.Core.FileReading;

namespace AudioProjectMER.Factories;

public static class NVorbisSupport
{
    public static void RegisterFactory()
    {
        Ensure<VorbisReader>();
        Ensure<VorbisWaveReader>();
        AudioReaderFactoryManager.RegisterFactory("ogg", new VorbisStreamFactory());
    }

    private static void Ensure<T>() => _ = typeof(T);
}