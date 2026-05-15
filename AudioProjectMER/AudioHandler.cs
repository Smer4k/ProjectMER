using System.IO;
using ProjectMER.Features.Serializable;
using SecretLabNAudio.Core;
using SecretLabNAudio.Core.Extensions;
using SecretLabNAudio.Core.Extensions.Processors;
using SecretLabNAudio.Core.Pools;
using SecretLabNAudio.Core.Processors;
using SecretLabNAudio.Core.Providers;
using UnityEngine;

namespace AudioProjectMER;

public class AudioHandler : MonoBehaviour
{
    public AudioPlayerSettings Settings;
    public AudioPlayer AudioPlayer;
    public SpeedChangingSampleProvider SpeedChangingSampleProvider;

    private void CreateAudioPlayer()
    {
        var speakerSettings = new SpeakerSettings()
        {
            IsSpatial = Settings.IsSpatial,
            Volume = Settings.Volume,
            MinDistance = Settings.MinDistance,
            MaxDistance = Settings.MaxDistance,
        };
        AudioPlayer = AudioPlayerPool.Rent(speakerSettings, transform, Vector3.zero);
    }

    public void Play()
    {
        if (AudioPlayer == null || AudioPlayer.HasEnded)
        {
            Settings.Pause = false;
            CreateAudioPlayer();
        }

        if (Settings.IsShortClip)
            AudioPlayer.UseShortClip(Settings.FileName, Settings.Loop);
        else
            AudioPlayer.UseFileSafe(Path.Combine(AudioProjectMER.ClipsPath, Settings.FileName), Settings.Loop);
        
        ProcessorChain chain = AudioPlayer.SampleProvider!.ToCompatibleChain().Speed(Settings.Speed);
        SpeedChangingSampleProvider = (SpeedChangingSampleProvider)chain.Master;
        AudioPlayer.SampleProvider = chain;
        AudioPlayer.PoolOnEnd();
    }

    public void SetFileName(string fileName)
    {
        Settings.FileName = fileName;
        Play();
    }

    public void SetIsShortClip(bool isShortClip)
    {
        Settings.IsShortClip = isShortClip;
    }

    public void SetPlayOnSpawn(bool isPlayOnSpawn)
    {
        Settings.PlayOnSpawn = isPlayOnSpawn;
    }

    public void SetLoop(bool loop)
    {
        Settings.Loop = loop;
        if (AudioPlayer != null && !AudioPlayer.HasEnded)
            AudioPlayer.Loop(loop);
    }

    public void SetIsSpatial(bool isSpatial)
    {
        Settings.IsSpatial = isSpatial;
        if (AudioPlayer != null && !AudioPlayer.HasEnded)
            AudioPlayer.IsSpatial = isSpatial;
    }

    public void SetVolume(float volume)
    {
        Settings.Volume = volume;
        if (AudioPlayer != null && !AudioPlayer.HasEnded)
            AudioPlayer.Volume = volume;
    }

    public void SetMinDistance(float distance)
    {
        Settings.MinDistance = distance;
        if (AudioPlayer != null && !AudioPlayer.HasEnded)
            AudioPlayer.MinDistance = distance;
    }

    public void SetMaxDistance(float distance)
    {
        Settings.MaxDistance = distance;
        if (AudioPlayer != null && !AudioPlayer.HasEnded)
            AudioPlayer.MaxDistance = distance;
    }

    public void SetPause(bool pause)
    {
        Settings.Pause = pause;
        if (AudioPlayer != null && !AudioPlayer.HasEnded)
            AudioPlayer.IsPaused = pause;
    }

    public void SetSpeed(float speed)
    {
        Settings.Speed = speed;
        if (AudioPlayer != null && !AudioPlayer.HasEnded)
            SpeedChangingSampleProvider.Speed = speed;
    }

    public void OnDestroy()
    {
        ActionHandler.AudioHandlers.Remove(transform);
    }
}