using ProjectMER.Features.Objects;

namespace ProjectMER.Events.Arguments.Interfaces;

public interface ISchematicEvent
{
	public SchematicObject Schematic { get; }
}
