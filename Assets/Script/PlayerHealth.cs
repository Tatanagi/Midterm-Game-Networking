using Unity.Netcode;
using UnityEngine;

public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;

    [Header("Death Settings")]
    [SerializeField] private float respawnDelay = 2f;

    // Synced Health (Server Authoritative)
    private readonly NetworkVariable<float> netHealth = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private PlayerSpawnManager spawnManager;
    private NetworkObject netObject;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => netHealth.Value;

    public event System.Action<float, float> OnHealthChanged; // old, new

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
            currentHealth = maxHealth;
        }

        // Initial UI/feedback update
        OnNetHealthChanged(0f, netHealth.Value);
    }

    public override void OnNetworkDespawn()
    {
        netHealth.OnValueChanged -= OnNetHealthChanged;
        base.OnNetworkDespawn();
    }

    private void OnNetHealthChanged(float oldValue, float newValue)
    {
        currentHealth = newValue;
        OnHealthChanged?.Invoke(oldValue, newValue);

        if (newValue <= 0f && oldValue > 0f)
        {
            Die();
        }
    }

    /// <summary>
    /// Call this on the SERVER only
    /// </summary>
    public void TakeDamage(float damage, ulong attackerClientId = 0)
    {
        if (!IsServer) return;

        float newHealth = Mathf.Max(0f, netHealth.Value - damage);
        netHealth.Value = newHealth;

        Debug.Log($"[PlayerHealth] Player {OwnerClientId} took {damage} damage from {attackerClientId}. Health: {newHealth}/{maxHealth}");
    }

    private void Die()
    {
        Debug.Log($"💀 Player {OwnerClientId} died!");

        // Optional: Disable movement / shooting while dead
        if (TryGetComponent<NetworkPlayerController>(out var controller))
            controller.enabled = false;

        if (TryGetComponent<NetworkGun>(out var gun))
            gun.enabled = false;

        // Respawn after delay
        if (spawnManager != null)
        {
            Invoke(nameof(Respawn), respawnDelay);
        }
        else
        {
            Debug.LogWarning("PlayerSpawnManager not found on player!");
        }
    }

    private void Respawn()
    {
        if (!IsServer) return;

        // Re-enable components
        if (TryGetComponent<NetworkPlayerController>(out var controller))
            controller.enabled = true;

        if (TryGetComponent<NetworkGun>(out var gun))
            gun.enabled = true;

        // Reset health
        netHealth.Value = maxHealth;

        // === NEW: Re-show health bar ===
        if (TryGetComponent<PlayerHealthBarUI>(out var healthBarUI))
        {
            healthBarUI.RespawnHealthBar();
        }

        // Respawn position
        if (spawnManager != null)
        {
            spawnManager.RespawnPlayer();
        }
    }

    // Optional: Public method for testing / other systems
    public void Heal(float amount)
    {
        if (!IsServer) return;
        netHealth.Value = Mathf.Min(maxHealth, netHealth.Value + amount);
    }
}