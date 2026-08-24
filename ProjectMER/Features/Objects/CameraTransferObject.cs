using PlayerRoles.PlayableScps.Scp079.Cameras;
using UnityEngine;
using Camera = LabApi.Features.Wrappers.Camera;

namespace ProjectMER.Features.Objects;

public sealed class CameraTransferObject : MonoBehaviour
{
    public Camera TargetCamera { get; private set; }

    public void Init(Scp079Camera targetCamera)
    {
        TargetCamera = Camera.Get(targetCamera);
    }
}