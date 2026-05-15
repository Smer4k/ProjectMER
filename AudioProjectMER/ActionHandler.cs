using System.Collections.Generic;
using ProjectMER.Events.Arguments;
using ProjectMER.Features.Actions;
using ProjectMER.Features.Extensions;
using ProjectMER.Features.Objects;
using ProjectMER.Features.Serializable;
using UnityEngine;

namespace AudioProjectMER;

public static class ActionHandler
{
    public static Dictionary<Transform, AudioHandler> AudioHandlers = new();
    
    public static void OnAudioAction(SchematicObject schematicObject, ActionGame actionGame)
    {
        if (!schematicObject.ObjectFromId.TryGetValue(actionGame.TargetId, out var objTransform))
            return;
        if (!AudioHandlers.TryGetValue(objTransform, out var audioHandler))
            return;
        switch (actionGame.Param)
        {
            case nameof(AudioPlayerSettings.FileName):
                audioHandler.SetFileName(actionGame.Value);
                break;
            case nameof(AudioPlayerSettings.IsShortClip):
                audioHandler.SetIsShortClip(actionGame.Value.ParseBool());
                break;
            case nameof(AudioPlayerSettings.PlayOnSpawn):
                audioHandler.SetPlayOnSpawn(actionGame.Value.ParseBool());
                break;
            case nameof(AudioPlayerSettings.Loop):
                audioHandler.SetLoop(actionGame.Value.ParseBool());
                break;
            case nameof(AudioPlayerSettings.IsSpatial):
                audioHandler.SetIsSpatial(actionGame.Value.ParseBool());
                break;
            case nameof(AudioPlayerSettings.Volume):
                audioHandler.SetVolume(actionGame.Value.ParseFloat());
                break;
            case nameof(AudioPlayerSettings.MinDistance):
                audioHandler.SetMinDistance(actionGame.Value.ParseFloat());
                break;
            case nameof(AudioPlayerSettings.MaxDistance):
                audioHandler.SetMaxDistance(actionGame.Value.ParseFloat());
                break;
            case nameof(AudioPlayerSettings.Pause):
                audioHandler.SetPause(actionGame.Value.ParseBool());
                break;
            case nameof(AudioPlayerSettings.Speed):
                audioHandler.SetSpeed(actionGame.Value.ParseFloat());
                break;
            case "Play":
                audioHandler.Play();
                break;
        }
    }

    public static void OnSchematicSpawned(SchematicSpawnedEventArgs ev)
    {
        foreach (var kvp in ev.Schematic.AudioPlayerSettingsByObjectId)
        {
            if (!ev.Schematic.ObjectFromId.TryGetValue(kvp.Key, out var objTransform))
                continue;
            var audioHandler = objTransform.gameObject.AddComponent<AudioHandler>();
            audioHandler.Settings = kvp.Value;
            AudioHandlers.Add(objTransform, audioHandler);
            
            if (audioHandler.Settings.PlayOnSpawn)
                audioHandler.Play();
        }
    }
}