using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(PlayerHealthBarUI))]
public class PlayerHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;

    [Header("Death Settings")]
    [SerializeField] private float respawnDelay = 2f;

    [Header("Hit Effect Settings")]
    [SerializeField] private float hitEffectDuration = 5f;
    [SerializeField] private float slowMultiplier = 0.5f; // 50% speed when slowed

    // Synced Health (Server Authoritative)
    private readonly NetworkVariable<float> netHealth = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Hit Effect Network Variables
    private readonly NetworkVariable<bool> netIsInvincible = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private readonly NetworkVariable<float> netSpeedMultiplier = new NetworkVariable<float>(
        1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private PlayerSpawnManager spawnManager;
    private NetworkObject netObject;
    private NetworkPlayerController playerController;
    private PlayerHealthBarUI healthBarUI;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => netHealth.Value;
    public bool IsInvincible => netIsInvincible.Value;

    public event System.Action<float, float> OnHealthChanged; // old, new

    private void Awake()
    {
        netObject = GetComponent<NetworkObject>();
        spawnManager = GetComponent<PlayerSpawnManager>();
        playerController = GetComponent<NetworkPlayerController>();
        healthBarUI = GetComponent<PlayerHealthBarUI>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        netHealth.OnValueChanged += OnNetHealthChanged;
        netIsInvincible.OnValueChanged += OnInvincibleChanged;
        netSpeedMultiplier.OnValueChanged += OnSpeedMultiplierChanged;

        if (IsServer)
        {
            netHealth.Value = maxHealth;
            currentHealth = maxHealth;
            netSpeedMultiplier.Value = 1f;
        }

        // Initial UI/feedback update
        OnNetHealthChanged(0f, netHealth.Value);
    }

    public override void OnNetworkDespawn()
    {
        netHealth.OnValueChanged -= OnNetHealthChanged;
        netIsInvincible.OnValueChanged -= OnInvincibleChanged;
        netSpeedMultiplier.OnValueChanged -= OnSpeedMultiplierChanged;
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

    private void OnInvincibleChanged(bool oldValue, bool newValue)
    {
        Debug.Log($"Player {OwnerClientId} Invincible: {newValue}");
        // You can trigger VFX here (e.g., shield effect)
    }

    private void OnSpeedMultiplierChanged(float oldValue, float newValue)
    {
        if (playerController != null)
        {
            playerController.SetSpeedMultiplier(newValue);
        }
    }

    /// <summary>
    /// Call this on the SERVER only
    /// </summary>
    public void TakeDamage(float damage, ulong attackerClientId = 0)
    {
        if (!IsServer) return;
        if (netIsInvincible.Value)
        {
            Debug.Log($"[PlayerHealth] Player {OwnerClientId} is invincible - damage ignored");
            return;
        }

        float newHealth = Mathf.Max(0f, netHealth.Value - damage);
        netHealth.Value = newHealth;

        Debug.Log($"[PlayerHealth] Player {OwnerClientId} took {damage} damage from {attackerClientId}. Health: {newHealth}/{maxHealth}");

        // Apply hit effect (invincibility + slow)
        if (newHealth > 0f) // Don't apply effect if dead
        {
            ApplyHitEffect();
        }
    }

    private void ApplyHitEffect()
    {
        if (!IsServer) return;

        netIsInvincible.Value = true;
        netSpeedMultiplier.Value = slowMultiplier;

        // Auto-remove effect after duration
        CancelInvoke(nameof(ClearHitEffect)); // prevent duplicate timers
        Invoke(nameof(ClearHitEffect), hitEffectDuration);
    }

    private void ClearHitEffect()
    {
        if (!IsServer) return;

        netIsInvincible.Value = false;
        netSpeedMultiplier.Value = 1f;

        Debug.Log($"[PlayerHealth] Player {OwnerClientId} hit effect ended");
    }

    private void Die()
    {
        Debug.Log($"💀 Player {OwnerClientId} died!");

        // Optional: Disable movement / shooting while dead
        if (playerController != null)
            playerController.enabled = false;

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
        // Re-enable components
        if (playerController != null)
        {
            playerController.enabled = true;
            playerController.ResetMovement(); // Reset slow effect
        }

        if (TryGetComponent<NetworkGun>(out var gun))
            gun.enabled = true;

        if (healthBarUI != null)
            healthBarUI.RespawnHealthBar();

        // Everything below is server-authoritative.
        if (!IsServer) return;

        netHealth.Value = maxHealth;
        netIsInvincible.Value = false;
        netSpeedMultiplier.Value = 1f;

        if (spawnManager != null)
            spawnManager.RespawnPlayer();
        else
            Debug.LogWarning("PlayerSpawnManager not found on player!");
    }

    // Optional: Public method for testing / other systems
    public void Heal(float amount)
    {
        if (!IsServer) return;
        netHealth.Value = Mathf.Min(maxHealth, netHealth.Value + amount);
    }
}