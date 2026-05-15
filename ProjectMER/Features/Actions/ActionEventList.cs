namespace ProjectMER.Features.Actions;

public class ActionEventList
{
	public string Id { get; set; } = string.Empty;

	public string DisplayName { get; set; } = "Actions";

	public List<ActionGame> Actions { get; set; } = [];

	public void EnsureDefaults(int fallbackIndex)
	{
		if (string.IsNullOrWhiteSpace(Id))
			Id = $"Event{fallbackIndex}";

		if (string.IsNullOrWhiteSpace(DisplayName))
			DisplayName = $"Event {fallbackIndex}";

		Actions ??= [];
	}
}
