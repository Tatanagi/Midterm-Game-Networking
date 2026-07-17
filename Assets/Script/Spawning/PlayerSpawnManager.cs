using Unity.Netcode;
using UnityEngine;

public class PlayerSpawnManager : NetworkBehaviour
{
    private static int nextSpawnIndex = 0;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        TeleportToNextSpawnPoint();
    }

    public void RespawnPlayer()
    {
        if (!IsServer) return;
        TeleportToNextSpawnPoint();
    }

    private void TeleportToNextSpawnPoint()
    {
        GameObject[] spawnPointObjects = GameObject.FindGameObjectsWithTag("SpawnPoint");

        if (spawnPointObjects.Length == 0)
        {
            Debug.LogError("❌ No SpawnPoint tagged objects found in the scene!");
            return;
        }

        if (nextSpawnIndex >= spawnPointObjects.Length)
            nextSpawnIndex = 0;

        Transform selected = spawnPointObjects[nextSpawnIndex].transform;
        int usedIndex = nextSpawnIndex;
        nextSpawnIndex++;

        // Owner-authoritative NetworkTransform: the move MUST come from the owner.
        TeleportOwnerRpc(selected.position, selected.rotation);

        Debug.Log($"✅ Player spawn requested at SpawnPoint index {usedIndex}");
    }

    [Rpc(SendTo.Owner)]
    private void TeleportOwnerRpc(Vector3 position, Quaternion rotation)
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.position = position;
        transform.rotation = rotation;

        if (cc != null) cc.enabled = true;
    }
}