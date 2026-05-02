using System.Globalization;
using AdminToys;
using CommandSystem.Commands.Shared;
using Interactables.Interobjects.DoorUtils;
using LabApi.Features.Wrappers;
using MapGeneration;
using MEC;
using Mirror;
using ProjectMER.Features.Actions;
using ProjectMER.Features.Enums;
using ProjectMER.Features.Extensions;
using RemoteAdmin;
using UnityEngine;
using LightSourceToy = AdminToys.LightSourceToy;
using PrimitiveObjectToy = AdminToys.PrimitiveObjectToy;
using TextToy = AdminToys.TextToy;

namespace ProjectMER.Features.Objects;

public class ActionEventHostObject
{
    public ActionEventHostObject(SchematicObject schematic, int hostObjectId)
    {
        _schematic = schematic;
        _hostObjectId = hostObjectId;
    }

    public IReadOnlyList<ActionEventList> ActionEvents => _actionEvents;

    public Dictionary<string, List<ActionGame>> ActionsByEventId { get; } = new(StringComparer.Ordinal);

    public CoroutineHandle RunActions(string eventId, Player? target = null) =>
        Timing.RunCoroutine(ExecuteActionsCoroutine(eventId, target));

    public bool TryGetActions(string eventId, out List<ActionGame> actions) =>
        ActionsByEventId.TryGetValue(eventId, out actions);

    public IEnumerator<float> ExecuteActionsCoroutine(string eventId, Player? target = null)
    {
        if (string.IsNullOrWhiteSpace(eventId))
            yield break;

        if (!TryGetActions(eventId, out List<ActionGame> actions) || actions.Count == 0)
            yield break;

        foreach (ActionGame action in actions)
        {
            if (action == null)
            {
                yield return Timing.WaitForOneFrame;
                continue;
            }

            if (action.ActionDelay > 0f)
            {
                yield return Timing.WaitForSeconds(action.ActionDelay);
            }

            try
            {
                ExecuteAction(action, target);
            }
            catch (Exception e)
            {
                Logger.Error($"Action execution failed for EventId '{eventId}' and action type '{action.Type}': {e}");
            }

            yield return Timing.WaitForOneFrame;
        }
    }

    internal void SetActionEvents(List<ActionEventList> actionEvents)
    {
        _actionEvents = actionEvents ?? [];
        ActionsByEventId.Clear();
        _animatorByObjectIdCache.Clear();
        _animParamHashCache.Clear();

        ActionsByEventId.AddRange(ActionEventSerialization.BuildEventDictionary(_actionEvents));
    }

    private void ExecuteAction(ActionGame action, Player? target)
    {
        switch (action.Type)
        {
            case ActionType.Command:
                ExecuteCommandAction(action, target);
                break;
            case ActionType.Animation:
                ExecuteAnimationAction(action);
                break;
            case ActionType.Audio:
                ExecuteAudioAction(action);
                break;
            case ActionType.SetComponentProperty:
                ExecuteSetComponentPropertyAction(action);
                break;
            case ActionType.Destroy:
                ExecuteDestroy(action);
                break;
            default:
                Logger.Warn($"Unknown action type: {action.Type}");
                break;
        }
    }

    private static void ExecuteCommandAction(ActionGame action, Player? target)
    {
        if (string.IsNullOrWhiteSpace(action.Value))
            return;

        var args = action.Value.Trim().Split(' ', 512, StringSplitOptions.RemoveEmptyEntries);
        if (args.Length == 0)
            return;
        if (args[0].StartsWith('!'))
        {
            Logger.Warn("Command starts with '!'. Not allowed!");
            return;
        }

        if (!CommandProcessor.RemoteAdminCommandHandler.TryGetCommand(args[0], out var command))
        {
            Logger.Warn($"Command execution failed for command {args[0]}. Command not found!");
            return;
        }

        if (ProjectMER.Singleton.Config.BannedCommandsForActions.Contains(command.Command.ToLower()))
        {
            Logger.Warn($"Command '{command.Command}' banned for Actions");
            return;
        }

        if (target != null)
        {
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i].ToLower() is "[p_id]" or "{p_id}")
                {
                    args[i] = target.PlayerId.ToString();
                }
            }
        }

        // Logger.Info($"Trying to execute {string.Join(' ', args)}");
        command.Execute(args.Segment(1), ServerConsole.Scs, out _);
    }

    private void ExecuteAnimationAction(ActionGame action)
    {
        if (string.IsNullOrWhiteSpace(action.Param))
            return;

        int targetObjectId = action.TargetId != 0 ? action.TargetId : _hostObjectId;
        Animator? animator = ResolveAnimator(targetObjectId);
        if (animator == null)
        {
            Logger.Warn($"Animation action skipped: no Animator for object id {targetObjectId}.");
            return;
        }

        int paramHash = GetAnimatorParamHash(action.Param);

        switch (action.ParamType)
        {
            case AnimatorControllerParameterType.Trigger:
                animator.SetTrigger(paramHash);
                break;

            case AnimatorControllerParameterType.Bool:
                animator.SetBool(paramHash, ParseBool(action.Value));
                break;

            case AnimatorControllerParameterType.Int:
                animator.SetInteger(paramHash, ParseInt(action.Value));
                break;

            case AnimatorControllerParameterType.Float:
                animator.SetFloat(paramHash, ParseFloat(action.Value));
                break;

            default:
                Logger.Warn($"Animation action skipped: unsupported ParamType {action.ParamType}.");
                break;
        }
    }

    private void ExecuteAudioAction(ActionGame action)
    {
        Logger.Warn($"Audio action is not implemented for runtime yet. Value: '{action.Value}'.");
    }

    private void ExecuteSetComponentPropertyAction(ActionGame action)
    {
        if (string.IsNullOrWhiteSpace(action.Param))
            return;

        int targetObjectId = action.TargetId != 0 ? action.TargetId : _hostObjectId;
        if (!_schematic.ObjectFromId.TryGetValue(targetObjectId, out Transform targetTransform) ||
            targetTransform == null)
        {
            Logger.Warn($"SetComponentProperty action skipped: object {targetObjectId} not found.");
            return;
        }

        GameObject targetObj = targetTransform.gameObject;

        if (action.Param == "MovementSmoothing")
        {
            if (targetObj.TryGetComponent<AdminToyBase>(out var value))
            {
                value.NetworkMovementSmoothing = ParseByte(action.Value);
            }

            return;
        }

        switch (action.BlockType)
        {
            case BlockType.Primitive:
                var primitive = targetObj.GetComponent<PrimitiveObjectToy>();
                if (primitive == null)
                    break;
                switch (action.Param)
                {
                    case nameof(PrimitiveFlags.Visible):
                    {
                        if (ParseBool(action.Value))
                            primitive.NetworkPrimitiveFlags |= PrimitiveFlags.Visible;
                        else
                            primitive.NetworkPrimitiveFlags &= ~PrimitiveFlags.Visible;
                        break;
                    }
                    case nameof(PrimitiveFlags.Collidable):
                    {
                        if (ParseBool(action.Value))
                            primitive.NetworkPrimitiveFlags |= PrimitiveFlags.Collidable;
                        else
                            primitive.NetworkPrimitiveFlags &= ~PrimitiveFlags.Collidable;
                        break;
                    }
                    case nameof(Color):
                        primitive.NetworkMaterialColor = action.Value.GetColorFromString();
                        break;
                }

                break;
            case BlockType.Light:
                var lightSourceToy = targetObj.GetComponent<LightSourceToy>();
                if (lightSourceToy == null)
                    break;
                FlickerController flicker;
                switch (action.Param)
                {
                    case nameof(LightType):
                        lightSourceToy.NetworkLightType = Enum.Parse<LightType>(action.Value, true);
                        break;
                    case "Intensity":
                        lightSourceToy.NetworkLightIntensity = ParseFloat(action.Value);
                        break;
                    case "Range":
                        lightSourceToy.NetworkLightRange = ParseFloat(action.Value);
                        break;
                    case "ShadowStrength":
                        lightSourceToy.NetworkShadowStrength = ParseFloat(action.Value);
                        break;
                    case nameof(LightShadows):
                        lightSourceToy.NetworkShadowType = Enum.Parse<LightShadows>(action.Value, true);
                        break;
                    case "SpotAngle":
                        lightSourceToy.NetworkSpotAngle = ParseFloat(action.Value);
                        break;
                    case "InnerSpotAngle":
                        lightSourceToy.NetworkInnerSpotAngle = ParseFloat(action.Value);
                        break;
                    case nameof(Color):
                        lightSourceToy.NetworkLightColor = action.Value.GetColorFromString();
                        break;
                    case "Flicker":
                        flicker = lightSourceToy.GetComponent<FlickerController>();
                        var enableFlicker = ParseBool(action.Value);
                        if (enableFlicker && flicker == null)
                        {
                            flicker = lightSourceToy.gameObject.AddComponent<FlickerController>();
                            flicker.AddSchematic(_schematic);
                        }
                        else if (!enableFlicker && flicker != null)
                        {
                            GameObject.Destroy(flicker);
                        }

                        break;
                    case "FlickerZone":
                        flicker = lightSourceToy.GetComponent<FlickerController>();
                        if (flicker == null)
                            break;
                        flicker.Zone = Enum.Parse<FacilityZone>(action.Value, true);
                        break;
                }

                break;
            case BlockType.Interactable:
                var interactable = targetObj.GetComponent<InvisibleInteractableToy>();
                if (interactable == null)
                    break;
                switch (action.Param)
                {
                    case "Shape":
                        interactable.NetworkShape =
                            Enum.Parse<InvisibleInteractableToy.ColliderShape>(action.Value, true);
                        break;
                    case "InteractionDuration":
                        interactable.NetworkInteractionDuration = ParseFloat(action.Value);
                        break;
                    case "IsLocked":
                        interactable.IsLocked = ParseBool(action.Value);
                        break;
                    case nameof(ActionInteractableToy.Permissions):
                        if (!ActionInteractableToy.Instances.TryGetValue(InteractableToy.Get(interactable), out var actionInteractableToy))
                            break;
                        actionInteractableToy.Permissions = Enum.Parse<DoorPermissionFlags>(action.Value, true);
                        break;
                    case nameof(ActionInteractableToy.RequireAll):
                        if (!ActionInteractableToy.Instances.TryGetValue(InteractableToy.Get(interactable), out actionInteractableToy))
                            break;
                        actionInteractableToy.RequireAll = ParseBool(action.Value);
                        break;
                }

                break;
            case BlockType.Door:
                var doorVariant = targetObj.GetComponent<DoorVariant>();
                if (doorVariant == null)
                    break;
                switch (action.Param)
                {
                    case "RequiredPermissions":
                        doorVariant.RequiredPermissions.RequiredPermissions =
                            Enum.Parse<DoorPermissionFlags>(action.Value, true);
                        break;
                    case "RequireAll":
                        doorVariant.RequiredPermissions.RequireAll = ParseBool(action.Value);
                        break;
                    case "IsLocked":
                        doorVariant.ServerChangeLock(DoorLockReason.SpecialDoorFeature, ParseBool(action.Value));
                        break;
                    case "IsOpen":
                        doorVariant.NetworkTargetState = ParseBool(action.Value);
                        break;
                }

                break;
            case BlockType.Text:
                var textToy = targetObj.GetComponent<TextToy>();
                if (textToy == null)
                    break;
                switch (action.Param)
                {
                    case "Text":
                        textToy.Network_textFormat = action.Value;
                        break;
                    case "DisplaySize":
                        textToy.Network_displaySize = ParseVector3(action.Value);
                        break;
                }

                break;
            default:
                Logger.Warn($"Not Implemented for blockType: {action.BlockType}");
                break;
        }
    }

    private void ExecuteDestroy(ActionGame action)
    {
        int targetObjectId = action.TargetId != 0 ? action.TargetId : _hostObjectId;
        if (!_schematic.ObjectFromId.TryGetValue(targetObjectId, out Transform targetTransform) ||
            targetTransform == null)
        {
            Logger.Warn($"ExecuteDestroy action skipped: object {targetObjectId} not found.");
            return;
        }

        NetworkServer.Destroy(targetTransform.gameObject);
    }

    private Animator? ResolveAnimator(int targetObjectId)
    {
        if (_animatorByObjectIdCache.TryGetValue(targetObjectId, out Animator cachedAnimator))
        {
            if (cachedAnimator != null)
                return cachedAnimator;

            _animatorByObjectIdCache.Remove(targetObjectId);
        }

        if (!_schematic.ObjectFromId.TryGetValue(targetObjectId, out Transform targetTransform) ||
            targetTransform == null)
            return null;

        GameObject target = targetTransform.gameObject;
        Animator? targetAnimator = target.GetComponent<Animator>();
        if (targetAnimator != null)
            _animatorByObjectIdCache[targetObjectId] = targetAnimator;

        return targetAnimator;
    }

    private int GetAnimatorParamHash(string paramName)
    {
        if (_animParamHashCache.TryGetValue(paramName, out int cachedHash))
            return cachedHash;

        int hash = Animator.StringToHash(paramName);
        _animParamHashCache[paramName] = hash;
        return hash;
    }

    private static byte ParseByte(string value)
    {
        if (byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out byte result))
            return result;
        return 0;
    }

    private static bool ParseBool(string value)
    {
        if (bool.TryParse(value, out bool boolValue))
            return boolValue;

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
            return intValue != 0;

        return false;
    }

    private static int ParseInt(string value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int intValue))
            return intValue;

        return 0;
    }

    private static float ParseFloat(string value)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
            return floatValue;

        return 0f;
    }

    private static Vector3 ParseVector3(string value)
    {
        if (value.Contains(':'))
        {
            var parts = value.Split(':');
            float x = parts.Length > 0 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture,
                out float px)
                ? px
                : 0f;
            float y = parts.Length > 1 && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture,
                out float py)
                ? py
                : 0f;
            float z = parts.Length > 2 && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture,
                out float pz)
                ? pz
                : 0f;
            return new Vector3(x, y, z);
        }

        return Vector3.zero;
    }

    private List<ActionEventList> _actionEvents = [];
    private readonly SchematicObject _schematic;
    private readonly int _hostObjectId;
    private readonly Dictionary<int, Animator> _animatorByObjectIdCache = [];
    private readonly Dictionary<string, int> _animParamHashCache = new(StringComparer.Ordinal);
}