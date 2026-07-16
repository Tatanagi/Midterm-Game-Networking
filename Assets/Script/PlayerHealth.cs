using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Death Settings")]
    [SerializeField] private float respawnDelay = 2f;

    private readonly NetworkVariable<float> netHealth = new NetworkVariable<float>(
        100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private PlayerSpawnManager spawnManager;
    private NetworkObject netObject;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => netHealth.Value;

    public event System.Action<float, float> OnHealthChanged;

    private void Awake()
    {
        netObject = GetComponent<NetworkObject>();
        spawnManager = GetComponent<PlayerSpawnManager>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        netHealth.OnValueChanged += OnNetHealthChanged;

        if (IsServer)
        {
            netHealth.Value = maxHealth;
        }

        OnNetHealthChanged(0f, netHealth.Value);
    }

    public override void OnNetworkDespawn()
    {
        netHealth.OnValueChanged -= OnNetHealthChanged;
        base.OnNetworkDespawn();
    }

    private void OnNetHealthChanged(float oldValue, float newValue)
    {
        OnHealthChanged?.Invoke(oldValue, newValue);

        if (newValue <= 0f && oldValue > 0f)
        {
            Die();
        }
        else if (oldValue <= 0f && newValue > 0f)
        {
            EnablePlayerComponents();   // Works for both Host and Client
        }
    }

    private void Die()
    {
        Debug.Log($"💀 Player {OwnerClientId} died!");
        DisablePlayerComponents();

        if (IsServer && spawnManager != null)
        {
            Invoke(nameof(Respawn), respawnDelay);
        }
    }

    private void Respawn()
    {
        if (!IsServer) return;

        netHealth.Value = maxHealth;

        if (spawnManager != null)
        {
            spawnManager.RespawnPlayer();
        }

        EnablePlayerComponents();
    }
    private void DisablePlayerComponents()
    {
        if (TryGetComponent<NetworkPlayerController>(out var controller))
            controller.enabled = false;

        if (TryGetComponent<NetworkGun>(out var gun))
            gun.enabled = false;
    }

    private void EnablePlayerComponents()
    {
        if (TryGetComponent<NetworkPlayerController>(out var controller))
            controller.enabled = true;

        if (TryGetComponent<NetworkGun>(out var gun))
            gun.enabled = true;
    }

    public void TakeDamage(float damage, ulong attackerClientId = 0)
    {
        if (!IsServer) return;
        float newHealth = Mathf.Max(0f, netHealth.Value - damage);
        netHealth.Value = newHealth;
    }

    public void Heal(float amount)
    {
        if (!IsServer) return;
        netHealth.Value = Mathf.Min(maxHealth, netHealth.Value + amount);
    }
}