using ProjectMER.Events.Arguments.Interfaces;
using ProjectMER.Features.Objects;

namespace ProjectMER.Events.Arguments;

public class SchematicSpawnedEventArgs : EventArgs, ISchematicEvent
{
	public SchematicSpawnedEventArgs(SchematicObject schematic, string name)
	{
		Schematic = schematic;
		Name = name;
	}

	public SchematicObject Schematic { get; }

	public string Name { get; }
}
