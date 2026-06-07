using System;
using System.IO;
using AudioProjectMER.Factories;
using LabApi.Features;
using LabApi.Features.Console;
using LabApi.Loader.Features.Plugins;
using ProjectMER.Events.Handlers;
using ProjectMER.Features.Objects;
using SecretLabNAudio.Core.FileReading;

namespace AudioProjectMER;

public sealed class AudioProjectMER : Plugin<Config>
{
    public override string Name { get; } = "AudioProjectMER";
    public override string Description { get; } = "Audio module for ProjectMER";
    public override string Author { get; } = "Smer4k";
    public override Version Version { get; } = new Version("1.0.0");
    public override Version RequiredApiVersion { get; } = LabApiProperties.CurrentVersion;
    
    public static AudioProjectMER Singleton { get; private set; }
    public static string ShortClipsPath { get; private set; }
    public static string ClipsPath { get; private set; }
    
    public override void Enable()
    {
        Singleton = this;
        ShortClipsPath = Path.Combine(Config.AudioPath, "ShortClips");
        ClipsPath = Path.Combine(Config.AudioPath, "Clips");
        
        if (!Directory.Exists(Config.AudioPath))
        {
            Logger.Warn("Audio directory does not exist. Creating...");
            Directory.CreateDirectory(Config.AudioPath);
        }

        if (!Directory.Exists(ShortClipsPath))
        {
            Logger.Warn("ShortClips directory does not exist. Creating...");
            Directory.CreateDirectory(ShortClipsPath);
        }

        if (!Directory.Exists(ClipsPath))
        {
            Logger.Warn("Clips directory does not exist. Creating...");
            Directory.CreateDirectory(ClipsPath);
        }
        
        NLayerSupport.RegisterFactory();
        NVorbisSupport.RegisterFactory();
        ActionEventHostObject.OnAudioAction += ActionHandler.OnAudioAction;
        Schematic.SchematicSpawned += ActionHandler.OnSchematicSpawned;
    }

    public override void Disable()
    {
        ActionEventHostObject.OnAudioAction -= ActionHandler.OnAudioAction;
        Schematic.SchematicSpawned -= ActionHandler.OnSchematicSpawned;
        Singleton = null;
    }
}