using PlayerRoles.PlayableScps.Scp079.Cameras;
using UnityEngine;
using Camera = LabApi.Features.Wrappers.Camera;

namespace ProjectMER.Features.Objects;

public sealed class CameraTransferObject : MonoBehaviour
{
    private static readonly Dictionary<ushort, ushort> CameraTransfers = new();
    public Camera CameraObject { get; private set; }
    public Camera TargetCamera { get; private set; }
    
    private ushort _initCameraId;
    
    public void Init(Scp079Camera initCamera, Scp079Camera targetCamera)
    {
        CameraObject = Camera.Get(initCamera);
        TargetCamera = Camera.Get(targetCamera);

        _initCameraId = initCamera.SyncId;
        CameraTransfers[initCamera.SyncId] = targetCamera.SyncId;
    }

    public void OnDestroy()
    {
        CameraTransfers.Remove(_initCameraId);
    }

    public static ushort GetCameraByTransfer(ushort syncIdTransfer)
    {
        return CameraTransfers.GetValueOrDefault(syncIdTransfer, syncIdTransfer);
    }
}