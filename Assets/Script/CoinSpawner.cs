using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class CoinSpawner : NetworkBehaviour
{
    [Header("Coin Settings")]
    [SerializeField] private GameObject coinPrefab;
    [SerializeField] private int targetCoinCount = 20;
    [SerializeField] private float spawnHeight = 1f;

    [Header("Spawn Points (Drag & Drop here)")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Spawn Area (Fallback)")]
    [SerializeField] private Vector3 spawnAreaCenter = new Vector3(0, 0, 0);
    [SerializeField] private Vector3 spawnAreaSize = new Vector3(40, 1, 40);

    private List<NetworkObject> spawnedCoins = new List<NetworkObject>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            SpawnInitialCoins();
        }
    }

    /// <summary>
    /// Public method - used by ReadyManager when starting a new round
    /// </summary>
    public void SpawnInitialCoins()
    {
        if (coinPrefab == null)
        {
            Debug.LogError("Coin Prefab is not assigned in CoinSpawner!");
            return;
        }

        DespawnAllCoins();

        for (int i = 0; i < targetCoinCount; i++)
        {
            SpawnSingleCoinInternal();
        }

        Debug.Log($"✅ Spawned {targetCoinCount} coins initially.");
    }

    /// <summary>
    /// Public method - called every time a coin is collected
    /// </summary>
    public void SpawnSingleCoin()
    {
        if (!IsServer || coinPrefab == null) return;

        // Clean up destroyed coins
        spawnedCoins.RemoveAll(coin => coin == null || !coin.IsSpawned);

        // Maintain exact count
        if (spawnedCoins.Count < targetCoinCount)
        {
            SpawnSingleCoinInternal();
            Debug.Log($"[CoinSpawner] Spawned 1 replacement coin. Current: {spawnedCoins.Count}/{targetCoinCount}");
        }
    }

    private void SpawnSingleCoinInternal()
    {
        Vector3 spawnPosition;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform randomSpawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            spawnPosition = randomSpawnPoint.position + Vector3.up * spawnHeight
                         + new Vector3(Random.Range(-2.5f, 2.5f), 0, Random.Range(-2.5f, 2.5f));
        }
        else
        {
            spawnPosition = new Vector3(
                Random.Range(spawnAreaCenter.x - spawnAreaSize.x / 2f, spawnAreaCenter.x + spawnAreaSize.x / 2f),
                spawnAreaCenter.y + spawnHeight,
                Random.Range(spawnAreaCenter.z - spawnAreaSize.z / 2f, spawnAreaCenter.z + spawnAreaSize.z / 2f)
            );
        }

        GameObject coinObj = Instantiate(coinPrefab, spawnPosition, Quaternion.identity);
        NetworkObject netObj = coinObj.GetComponent<NetworkObject>();

        if (netObj != null)
        {
            netObj.Spawn();
            spawnedCoins.Add(netObj);
        }
        else
        {
            Debug.LogError("Coin prefab is missing NetworkObject component!");
            Destroy(coinObj);
        }
    }

    private void DespawnAllCoins()
    {
        foreach (var coin in spawnedCoins)
        {
            if (coin != null && coin.IsSpawned)
                coin.Despawn();
        }
        spawnedCoins.Clear();
    }

    private void OnDestroy()
    {
        if (IsServer)
            DespawnAllCoins();
    }
}