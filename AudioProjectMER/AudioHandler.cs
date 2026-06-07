using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProjectMER.Features.Serializable;
using SecretLabNAudio.Core;
using SecretLabNAudio.Core.Extensions;
using SecretLabNAudio.Core.Extensions.Processors;
using SecretLabNAudio.Core.FileReading;
using SecretLabNAudio.Core.Pools;
using SecretLabNAudio.Core.Processors;
using SecretLabNAudio.Core.Providers;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace AudioProjectMER;

public class AudioHandler : MonoBehaviour
{
    public AudioPlayerSettings Settings;
    public AudioPlayer AudioPlayer;
    public SpeedChangingSampleProvider SpeedChangingSampleProvider;
    private static readonly HashSet<string> SupportedAudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".aiff", ".mp3", ".wav", ".ogg"
    };

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
        if (AudioPlayer == null)
        {
            CreateAudioPlayer();
        }

        SetPause(false);

        if (Settings.IsShortClip)
        {
            var clipName = new ClipName(Settings.FileName);
            if (!ShortClipCache.TryGet(clipName, out _))
            {
                var fullPath = ResolveAudioPath(AudioProjectMER.ShortClipsPath);
                if (fullPath == null)
                    return;
                ShortClipCache.AddFromFile(fullPath);
            }

            AudioPlayer.UseShortClip(clipName, Settings.Loop);
        }
        else
        {
            var fullPath = ResolveAudioPath(AudioProjectMER.ClipsPath);
            if (fullPath == null)
                return;
            AudioPlayer.UseFileSafe(fullPath, Settings.Loop);
        }

        if (AudioPlayer.SampleProvider == null)
            return;
        AudioPlayer.OwnsProvider = false;
        ProcessorChain chain = AudioPlayer.SampleProvider.ToCompatibleChain().Speed(Settings.Speed);
        SpeedChangingSampleProvider = (SpeedChangingSampleProvider)chain.Master;
        AudioPlayer.SampleProvider = chain;
        AudioPlayer.OwnsProvider = true;
    }

    private string ResolveAudioPath(string basePath)
    {
        if (Path.HasExtension(Settings.FileName))
            return Path.Combine(basePath, Settings.FileName);

        Logger.Warn(
            $"The file named \"{Settings.FileName}\" does not contain an extension. Trying to find the file... (Add the file extension to the schematic for optimization!)");

        if (!Directory.Exists(basePath))
        {
            Logger.Warn($"Directory {basePath} does not exist");
            return null;
        }

        var matches = Directory.GetFiles(basePath, $"{Settings.FileName}.*", SearchOption.TopDirectoryOnly);
        if (matches.Length == 0)
        {
            Logger.Warn($"Audio File \"{Settings.FileName}\" not found");
            return null;
        }

        var fullPath = matches.FirstOrDefault(f => SupportedAudioExtensions.Contains(Path.GetExtension(f))) ??
                       matches[0];

        Settings.FileName = Path.GetFileName(fullPath);
        Logger.Info($"File found: {Settings.FileName}");
        return fullPath;
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
        if (AudioPlayer != null)
            AudioPlayer.Loop(loop);
    }

    public void SetIsSpatial(bool isSpatial)
    {
        Settings.IsSpatial = isSpatial;
        if (AudioPlayer != null)
            AudioPlayer.IsSpatial = isSpatial;
    }

    public void SetVolume(float volume)
    {
        Settings.Volume = volume;
        if (AudioPlayer != null)
            AudioPlayer.Volume = volume;
    }

    public void SetMinDistance(float distance)
    {
        Settings.MinDistance = distance;
        if (AudioPlayer != null)
            AudioPlayer.MinDistance = distance;
    }

    public void SetMaxDistance(float distance)
    {
        Settings.MaxDistance = distance;
        if (AudioPlayer != null)
            AudioPlayer.MaxDistance = distance;
    }

    public void SetPause(bool pause)
    {
        Settings.Pause = pause;
        if (AudioPlayer != null)
            AudioPlayer.IsPaused = pause;
    }

    public void SetSpeed(float speed)
    {
        Settings.Speed = speed;
        if (AudioPlayer != null)
            SpeedChangingSampleProvider.Speed = speed;
    }

    public void OnDestroy()
    {
        AudioPlayerPool.Return(AudioPlayer);
        AudioPlayer = null;
        SpeedChangingSampleProvider = null;
        ActionHandler.AudioHandlers.Remove(transform);
    }
}