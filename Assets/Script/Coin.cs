using Unity.Netcode;
using UnityEngine;

public class Coin : NetworkBehaviour
{
    [SerializeField] private int value = 1;
    [SerializeField] private float rotationSpeed = 80f;

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
            NetworkObject.Despawn(false);
            Destroy(gameObject, 0.1f);
        }
    }
}