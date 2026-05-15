using System.IO;
using LabApi.Loader.Features.Paths;

namespace AudioProjectMER;

public class Config
{
    public string AudioPath { get; set; } = Path.Combine(PathManager.Configs.FullName, "Audio");
}