using LabApi.Features.Wrappers;
using UnityEngine;

namespace ProjectMER.Features.Interfaces;

public interface IIndicatorDefinition
{
	public GameObject SpawnOrUpdateIndicator(Room room, GameObject? instance = null);
}
