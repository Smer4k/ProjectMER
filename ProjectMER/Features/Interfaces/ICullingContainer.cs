using LabApi.Features.Wrappers;

namespace ProjectMER.Features.Interfaces;

public interface ICullingContainer
{
    void AddPlayer(Player player);
    void RemovePlayer(Player player);
}