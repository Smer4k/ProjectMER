using System;
using CommandSystem;
using SecretLabNAudio.Core.FileReading;

namespace AudioProjectMER;

[CommandHandler(typeof(RemoteAdminCommandHandler))]
public class ReloadShortClipsCommand : ICommand
{
    public string Command { get; } = "ReloadShortClips";
    public string[] Aliases { get; } = ["reloadClips"];
    public string Description { get; } = "Reloads all shortclips from the shortclip dictionary.";
    
    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        var countClips = ShortClipCache.AddAllFromDirectory(AudioProjectMER.ShortClipsPath);
        response = $"Loaded {countClips} clips from ShortClips Dictionary.";
        return true;
    }
}