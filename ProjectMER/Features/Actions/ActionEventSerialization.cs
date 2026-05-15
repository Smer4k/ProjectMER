using System.Globalization;
using ProjectMER.Features.Enums;
using UnityEngine;
using Utf8Json;

namespace ProjectMER.Features.Actions;

public static class ActionEventSerialization
{
	public const string ActionEventsPropertyName = "ActionEvents";

	public static List<ActionEventList> ReadEventListsFromProperties(Dictionary<string, object>? properties, string key = ActionEventsPropertyName)
	{
		if (properties == null)
			return [];

		if (!TryGetValueIgnoreCase(properties, key, out object eventListsObject))
			return [];

		return ReadEventListsFromObject(eventListsObject);
	}

	public static List<ActionEventList> ReadEventListsFromObject(object? eventListsObject)
	{
		if (eventListsObject == null)
			return [];

		if (eventListsObject is List<ActionEventList> typedList)
		{
			EnsureEventLists(typedList);
			return typedList;
		}

		if (eventListsObject is string json)
		{
			return DeserializeEventListsJson(json);
		}

		if (eventListsObject is Dictionary<string, object> dictionary)
		{
			ActionEventList? single = ParseEventList(dictionary, 1);
			if (single == null)
				return [];

			List<ActionEventList> list = [single];
			EnsureEventLists(list);
			return list;
		}

		if (eventListsObject is IEnumerable<object> enumerable)
		{
			List<ActionEventList> list = [];
			int fallbackIndex = 1;
			foreach (object item in enumerable)
			{
				ActionEventList? actionEventList = ParseEventList(item, fallbackIndex);
				if (actionEventList != null)
					list.Add(actionEventList);

				fallbackIndex++;
			}

			EnsureEventLists(list);
			return list;
		}

		return [];
	}

	public static void EnsureEventLists(List<ActionEventList>? eventLists)
	{
		if (eventLists == null)
			return;

		for (int i = eventLists.Count - 1; i >= 0; i--)
		{
			ActionEventList? eventList = eventLists[i];
			if (eventList == null)
			{
				eventLists.RemoveAt(i);
				continue;
			}

			eventList.EnsureDefaults(i + 1);
			NormalizeActions(eventList.Actions);
		}
	}

	public static Dictionary<string, List<ActionGame>> BuildEventDictionary(List<ActionEventList>? eventLists)
	{
		Dictionary<string, List<ActionGame>> dictionary = new(StringComparer.Ordinal);
		if (eventLists == null)
			return dictionary;

		foreach (ActionEventList eventList in eventLists)
		{
			if (eventList == null)
				continue;

			if (!dictionary.TryGetValue(eventList.Id, out List<ActionGame> actions))
			{
				actions = [];
				dictionary.Add(eventList.Id, actions);
			}

			if (eventList.Actions == null)
				continue;

			foreach (ActionGame action in eventList.Actions)
			{
				if (action == null)
					continue;

				actions.Add(action);
			}
		}

		return dictionary;
	}

	public static IEnumerable<ActionGame> EnumerateActions(List<ActionEventList>? eventLists)
	{
		if (eventLists == null)
			yield break;

		foreach (ActionEventList eventList in eventLists)
		{
			if (eventList?.Actions == null)
				continue;

			foreach (ActionGame action in eventList.Actions)
				yield return action;
		}
	}

	private static ActionEventList? ParseEventList(object? rawEventList, int fallbackIndex)
	{
		if (rawEventList == null)
			return null;

		if (rawEventList is ActionEventList typedEventList)
		{
			typedEventList.EnsureDefaults(fallbackIndex);
			NormalizeActions(typedEventList.Actions);
			return typedEventList;
		}

		if (rawEventList is string rawJson)
		{
			try
			{
				ActionEventList? deserialized = JsonSerializer.Deserialize<ActionEventList>(rawJson);
				deserialized?.EnsureDefaults(fallbackIndex);
				NormalizeActions(deserialized?.Actions);
				return deserialized;
			}
			catch
			{
				return null;
			}
		}

		if (rawEventList is not Dictionary<string, object> dictionary)
			return null;

		ActionEventList eventList = new()
		{
			Id = ReadString(dictionary, nameof(ActionEventList.Id)),
			DisplayName = ReadString(dictionary, nameof(ActionEventList.DisplayName)),
			Actions = ParseActions(ReadObject(dictionary, nameof(ActionEventList.Actions))),
		};

		if (string.IsNullOrWhiteSpace(eventList.Id))
			eventList.Id = ReadString(dictionary, "EventId");

		eventList.EnsureDefaults(fallbackIndex);
		NormalizeActions(eventList.Actions);
		return eventList;
	}

	private static List<ActionGame> ParseActions(object? rawActions)
	{
		if (rawActions == null)
			return [];

		if (rawActions is List<ActionGame> typedList)
		{
			NormalizeActions(typedList);
			return typedList;
		}

		if (rawActions is string json)
		{
			try
			{
				List<ActionGame>? list = JsonSerializer.Deserialize<List<ActionGame>>(json);
				NormalizeActions(list);
				return list ?? [];
			}
			catch
			{
				return [];
			}
		}

		if (rawActions is not IEnumerable<object> enumerable)
			return [];

		List<ActionGame> actions = [];
		foreach (object actionObject in enumerable)
		{
			ActionGame? action = ParseAction(actionObject);
			if (action != null)
				actions.Add(action);
		}

		NormalizeActions(actions);
		return actions;
	}

	private static ActionGame? ParseAction(object? rawAction)
	{
		if (rawAction == null)
			return null;

		if (rawAction is ActionGame typedAction)
		{
			typedAction.EnsureDefaults();
			return typedAction;
		}

		if (rawAction is string json)
		{
			try
			{
				ActionGame? action = JsonSerializer.Deserialize<ActionGame>(json);
				action?.EnsureDefaults();
				return action;
			}
			catch
			{
				return null;
			}
		}

		if (rawAction is not Dictionary<string, object> dictionary)
			return null;

		ActionGame actionGame = new()
		{
			Type = ReadActionType(dictionary, nameof(ActionGame.Type)),
			ActionDelay = ReadFloat(dictionary, nameof(ActionGame.ActionDelay)),
			Value = ReadString(dictionary, nameof(ActionGame.Value)),
			TargetId = ReadInt(dictionary, nameof(ActionGame.TargetId)),
			Param = ReadString(dictionary, nameof(ActionGame.Param)),
			BlockType = (BlockType)ReadInt(dictionary, nameof(ActionGame.BlockType)),
			ParamType = ReadAnimatorParamType(dictionary, nameof(ActionGame.ParamType)),
		};

		actionGame.EnsureDefaults();
		return actionGame;
	}

	private static List<ActionEventList> DeserializeEventListsJson(string json)
	{
		if (string.IsNullOrWhiteSpace(json))
			return [];

		try
		{
			List<ActionEventList>? list = JsonSerializer.Deserialize<List<ActionEventList>>(json);
			EnsureEventLists(list);
			return list ?? [];
		}
		catch
		{
			try
			{
				ActionEventList? single = JsonSerializer.Deserialize<ActionEventList>(json);
				if (single == null)
					return [];

				single.EnsureDefaults(1);
				NormalizeActions(single.Actions);
				return [single];
			}
			catch
			{
				return [];
			}
		}
	}

	private static void NormalizeActions(List<ActionGame>? actions)
	{
		if (actions == null)
			return;

		for (int i = actions.Count - 1; i >= 0; i--)
		{
			ActionGame? action = actions[i];
			if (action == null)
			{
				actions.RemoveAt(i);
				continue;
			}

			action.EnsureDefaults();
		}
	}

	private static string ReadString(Dictionary<string, object> dictionary, string key)
	{
		if (!TryGetValueIgnoreCase(dictionary, key, out object value))
			return string.Empty;

		return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
	}

	private static int ReadInt(Dictionary<string, object> dictionary, string key)
	{
		if (!TryGetValueIgnoreCase(dictionary, key, out object value))
			return 0;

		if (value is string stringValue)
		{
			if (int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedFromString))
				return parsedFromString;

			return 0;
		}

		try
		{
			return Convert.ToInt32(value, CultureInfo.InvariantCulture);
		}
		catch
		{
			return 0;
		}
	}

	private static float ReadFloat(Dictionary<string, object> dictionary, string key)
	{
		if (!TryGetValueIgnoreCase(dictionary, key, out object value))
			return 0f;

		if (value is string stringValue)
		{
			if (float.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsedFromString))
				return parsedFromString;
			return 0f;
		}

		try
		{
			return Convert.ToSingle(value, CultureInfo.InvariantCulture);
		}
		catch
		{
			return 0f;
		}
	}

	private static ActionType ReadActionType(Dictionary<string, object> dictionary, string key)
	{
		if (!TryGetValueIgnoreCase(dictionary, key, out object value))
			return default;

		if (value is string stringValue)
		{
			if (Enum.TryParse(stringValue, true, out ActionType parsedByName))
				return parsedByName;

			if (byte.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte parsedByNumber))
				return (ActionType)parsedByNumber;
		}

		try
		{
			return (ActionType)Convert.ToByte(value, CultureInfo.InvariantCulture);
		}
		catch
		{
			return default;
		}
	}

	private static AnimatorControllerParameterType ReadAnimatorParamType(Dictionary<string, object> dictionary, string key)
	{
		if (!TryGetValueIgnoreCase(dictionary, key, out object value))
			return default;

		if (value is string stringValue)
		{
			if (Enum.TryParse(stringValue, true, out AnimatorControllerParameterType parsedByName))
				return parsedByName;

			if (int.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedByNumber))
				return (AnimatorControllerParameterType)parsedByNumber;
		}

		try
		{
			return (AnimatorControllerParameterType)Convert.ToInt32(value, CultureInfo.InvariantCulture);
		}
		catch
		{
			return default;
		}
	}

	private static object? ReadObject(Dictionary<string, object> dictionary, string key) =>
		TryGetValueIgnoreCase(dictionary, key, out object value) ? value : null;

	private static bool TryGetValueIgnoreCase(Dictionary<string, object> dictionary, string key, out object value)
	{
		if (dictionary.TryGetValue(key, out value))
			return true;

		foreach (KeyValuePair<string, object> pair in dictionary)
		{
			if (!string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
				continue;

			value = pair.Value;
			return true;
		}

		value = null!;
		return false;
	}
}
