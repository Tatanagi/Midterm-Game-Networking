using Unity.Netcode;
using UnityEngine;

public class Coin : NetworkBehaviour
{
    [SerializeField] private int value = 1;
    [SerializeField] private float rotationSpeed = 80f;

    private CoinSpawner coinSpawner;

    private void Start()
    {
        if (IsServer)
        {
            coinSpawner = FindFirstObjectByType<CoinSpawner>();
        }
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        PlayerCoinManager playerCoins = other.GetComponent<PlayerCoinManager>();
        if (playerCoins != null)
        {
            playerCoins.AddCoins(value);

            // Despawn this coin
            var netObj = GetComponent<NetworkObject>();
            if (netObj != null)
                netObj.Despawn(false);

            Destroy(gameObject, 0.1f);

            // Spawn exactly one replacement
            if (coinSpawner != null)
            {
                coinSpawner.SpawnSingleCoin();
            }
        }
    }
}