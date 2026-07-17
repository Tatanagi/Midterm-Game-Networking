using UnityEngine;
using Unity.Netcode;
public class LocalPlayerCameraTarget : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            return;
        }
        TopDownCameraFollow cameraFollow = Camera.main.GetComponent<TopDownCameraFollow>();
        if (cameraFollow != null)
        {
            cameraFollow.SetTarget(transform);
        }
    }
}