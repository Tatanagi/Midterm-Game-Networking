using Unity.Netcode;
using UnityEngine;

public class PlayerSpawnManager : NetworkBehaviour
{
    private static int nextSpawnIndex = 0;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        GameObject[] spawnPointObjects = GameObject.FindGameObjectsWithTag("SpawnPoint");

        if (spawnPointObjects.Length == 0)
        {
            Debug.LogError("❌ No SpawnPoint tagged objects found in the scene! Please add at least one object with the 'SpawnPoint' tag.");
            return;
        }

        // Safety check for index
        if (nextSpawnIndex >= spawnPointObjects.Length)
        {
            nextSpawnIndex = 0;
        }

        Transform selectedSpawnPoint = spawnPointObjects[nextSpawnIndex].transform;

        CharacterController characterController = GetComponent<CharacterController>();

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        transform.position = selectedSpawnPoint.position;
        transform.rotation = selectedSpawnPoint.rotation;

        if (characterController != null)
        {
            characterController.enabled = true;
        }

        nextSpawnIndex++;

        Debug.Log($"✅ Player spawned at SpawnPoint index {nextSpawnIndex - 1}");
    }

    public void RespawnPlayer()
    {
        if (!IsServer) return;

        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");

        if (spawnPoints.Length == 0)
        {
            Debug.LogError("❌ No SpawnPoint tagged objects found for respawn!");
            return;
        }

        int index = nextSpawnIndex % spawnPoints.Length;
        Transform spawnPoint = spawnPoints[index].transform;

        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.position = spawnPoint.position;
        transform.rotation = spawnPoint.rotation;

        if (cc != null) cc.enabled = true;

        nextSpawnIndex++;
    }
}