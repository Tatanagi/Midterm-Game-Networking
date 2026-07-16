using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class PlayerSpawnManager : NetworkBehaviour
{
    private static int nextSpawnIndex = 0;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            RespawnPlayer();
        }
    }

    public void RespawnPlayer()
    {
        if (!IsServer) return;

        GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("SpawnPoint");
        if (spawnPoints.Length == 0)
        {
            Debug.LogError("❌ No SpawnPoint tagged objects found!");
            return;
        }

        int index = nextSpawnIndex % spawnPoints.Length;
        Transform spawnPoint = spawnPoints[index].transform;

        CharacterController cc = GetComponent<CharacterController>();
        bool ccWasEnabled = cc != null && cc.enabled;
        if (ccWasEnabled) cc.enabled = false;

        // Force teleport using temporary Server Authority
        NetworkTransform netTransform = GetComponent<NetworkTransform>();

        if (netTransform != null)
        {
            // Store original authority
            var originalAuthority = netTransform.AuthorityMode;

            // Temporarily switch to Server Authority to force position
            netTransform.AuthorityMode = NetworkTransform.AuthorityModes.Server;

            // Force the position + rotation
            netTransform.SetState(spawnPoint.position, spawnPoint.rotation, Vector3.one, false);

            // Switch back to Owner Authority after a short delay
            StartCoroutine(ResetToOwnerAuthority(netTransform, originalAuthority));
        }
        else
        {
            transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        }

        if (ccWasEnabled) cc.enabled = true;

        nextSpawnIndex = (nextSpawnIndex + 1) % spawnPoints.Length;

        Debug.Log($"✅ Player {OwnerClientId} respawned at SpawnPoint {index}");
    }

    private System.Collections.IEnumerator ResetToOwnerAuthority(NetworkTransform netTransform, NetworkTransform.AuthorityModes originalMode)
    {
        yield return new WaitForSeconds(0.15f); // Give time for position to settle
        if (netTransform != null)
        {
            netTransform.AuthorityMode = originalMode;
        }
    }
}