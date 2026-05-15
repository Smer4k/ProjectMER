using NLayer;
using NLayer.NAudioSupport;
using SecretLabNAudio.Core.FileReading;

namespace AudioProjectMER.Factories;

public static class NLayerSupport
{
    public static void RegisterFactory()
    {
        Ensure<MpegFile>();
        Ensure<Mp3FrameDecompressor>();
        AudioReaderFactoryManager.RegisterFactory("mp3", new MpegStreamFactory());
    }
    
    private static void Ensure<T>() => _ = typeof(T);
}