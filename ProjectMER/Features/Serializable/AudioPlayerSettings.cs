namespace ProjectMER.Features.Serializable;

public sealed class AudioPlayerSettings
{
    public string FileName { get; set; }
    public bool IsShortClip { get; set; }
    public bool PlayOnSpawn { get; set; }
    public bool Loop { get; set; }
    public bool IsSpatial { get; set; } = true;
    public float Volume { get; set; } = 1f;
    public float MinDistance { get; set; } = 1f;
    public float MaxDistance { get; set; } = 15f;
    public bool Pause { get; set; }
    public float Speed { get; set; } = 1f;
}