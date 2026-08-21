using PlayerRoles.PlayableScps.Scp079;
using PlayerRoles.PlayableScps.Scp079.Cameras;
using UnityEngine;

namespace ProjectMER.Features.Objects;

public sealed class CameraTransferObject : MonoBehaviour
{
    private Scp079Camera _camera;
    private ushort _cameraId;

    public void Init(Scp079Camera camera)
    {
        _camera = camera;
        _cameraId = camera.SyncId;
    }

    private void OnDestroy()
    {
        if (_cameraId >= Scp079InteractableBase.OrderedInstances.Count)
            return;
        if (_camera != Scp079InteractableBase.OrderedInstances[_cameraId - 1])
            return;
        Scp079InteractableBase.OrderedInstances[_cameraId - 1] = null;
    }
}