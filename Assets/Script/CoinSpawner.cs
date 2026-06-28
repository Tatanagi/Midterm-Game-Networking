using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CoinSpawner : NetworkBehaviour
{
    [Header("Coin Settings")]
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private int coinsToSpawn = 20;
    [SerializeField] private float spawnHeight = 1f;
    [SerializeField] private float resetInterval = 10f; // ← Time in seconds between resets

    [Header("Spawn Points (Drag & Drop here)")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Spawn Area (Fallback if no spawn points assigned)")]
    [SerializeField] private Vector3 spawnAreaCenter = new Vector3(0, 0, 0);
    [SerializeField] private Vector3 spawnAreaSize = new Vector3(40, 1, 40);

    private List<NetworkObject> spawnedCoins = new List<NetworkObject>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            SpawnCoins();
            StartCoroutine(ResetCoinsRoutine());
        }
    }

    private IEnumerator ResetCoinsRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(resetInterval);
            if (IsServer)
            {
                ResetCoins();
            }
        }
    }

    public void SpawnCoins()
    {
        if (coinPrefab == null)
        {
            Debug.LogError("Coin Prefab is not assigned in CoinSpawner!");
            return;
        }

        // Safety: clear old coins before spawning new ones
        DespawnAllCoins();

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            for (int i = 0; i < coinsToSpawn; i++)
            {
                Transform randomSpawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

                Vector3 spawnPosition = randomSpawnPoint.position + Vector3.up * spawnHeight;

                // Add small random offset to prevent stacking
                spawnPosition += new Vector3(
                    Random.Range(-2.5f, 2.5f),
                    0,
                    Random.Range(-2.5f, 2.5f)
                );

                GameObject coin = Instantiate(coinPrefab, spawnPosition, Quaternion.identity);
                NetworkObject netObj = coin.GetComponent<NetworkObject>();

                if (netObj != null)
                {
                    netObj.Spawn();
                    spawnedCoins.Add(netObj);
                }
                else
                {
                    Debug.LogError("Coin prefab is missing NetworkObject component!");
                    Destroy(coin);
                }
            }
            Debug.Log($"✅ Spawned {coinsToSpawn} coins using {spawnPoints.Length} drag & drop spawn points.");
        }
        else
        {
            // Fallback to area spawn
            Debug.LogWarning("No spawn points assigned. Using fallback area spawn.");
            for (int i = 0; i < coinsToSpawn; i++)
            {
                Vector3 randomPosition = new Vector3(
                    Random.Range(spawnAreaCenter.x - spawnAreaSize.x / 2, spawnAreaCenter.x + spawnAreaSize.x / 2),
                    spawnAreaCenter.y + spawnHeight,
                    Random.Range(spawnAreaCenter.z - spawnAreaSize.z / 2, spawnAreaCenter.z + spawnAreaSize.z / 2)
                );

                GameObject coin = Instantiate(coinPrefab, randomPosition, Quaternion.identity);
                NetworkObject netObj = coin.GetComponent<NetworkObject>();

                if (netObj != null)
                {
                    netObj.Spawn();
                    spawnedCoins.Add(netObj);
                }
                else
                {
                    Debug.LogError("Coin prefab is missing NetworkObject component!");
                    Destroy(coin);
                }
            }
            Debug.Log($"✅ Spawned {coinsToSpawn} coins using area spawn.");
        }
    }

    private void ResetCoins()
    {
        DespawnAllCoins();
        SpawnCoins();
    }

    private void DespawnAllCoins()
    {
        foreach (var coin in spawnedCoins)
        {
            if (coin != null && coin.IsSpawned)
            {
                coin.Despawn();
            }
        }
        spawnedCoins.Clear();
    }

    private void OnDestroy()
    {
        if (IsServer)
        {
            DespawnAllCoins();
        }
    }
}