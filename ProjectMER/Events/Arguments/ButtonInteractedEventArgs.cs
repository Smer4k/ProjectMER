using LabApi.Events.Arguments.Interfaces;
using LabApi.Features.Wrappers;
using ProjectMER.Events.Arguments.Interfaces;
using ProjectMER.Features.Objects;

namespace ProjectMER.Events.Arguments;

public class ButtonInteractedEventArgs : EventArgs, IPickupEvent, IPlayerEvent, ISchematicEvent
{
	public ButtonInteractedEventArgs(Pickup button, Player player, SchematicObject schematic)
	{
		Button = button;
		Player = player;
		Schematic = schematic;
	}

	public Pickup Button { get; }

	public Pickup Pickup => Button;

	public Player Player { get; }

	public SchematicObject Schematic { get; }
}
