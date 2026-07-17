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
    [SerializeField] public float hitEffectDuration = 5f;     // Made public for DebuffTimerUI
    [SerializeField] private float slowMultiplier = 0.5f;

    // Synced Health (Server Authoritative)
    private readonly NetworkVariable<float> netHealth = new NetworkVariable<float>(
        100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Hit Effect Network Variables - Made public for DebuffTimerUI
    public readonly NetworkVariable<bool> netIsInvincible = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> netSpeedMultiplier = new NetworkVariable<float>(
        1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private PlayerSpawnManager spawnManager;
    private NetworkObject netObject;
    private NetworkPlayerController playerController;
    private PlayerHealthBarUI healthBarUI;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => netHealth.Value;
    public bool IsInvincible => netIsInvincible.Value;

    public event System.Action<float, float> OnHealthChanged;

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

    private void OnInvincibleChanged(bool oldValue, bool newValue) { }
    private void OnSpeedMultiplierChanged(float oldValue, float newValue)
    {
        if (playerController != null)
            playerController.SetSpeedMultiplier(newValue);
    }

    public void TakeDamage(float damage, ulong attackerClientId = 0)
    {
        if (!IsServer) return;
        if (netIsInvincible.Value) return;

        float newHealth = Mathf.Max(0f, netHealth.Value - damage);
        netHealth.Value = newHealth;

        if (newHealth > 0f)
            ApplyHitEffect();
    }

    private void ApplyHitEffect()
    {
        if (!IsServer) return;
        netIsInvincible.Value = true;
        netSpeedMultiplier.Value = slowMultiplier;

        CancelInvoke(nameof(ClearHitEffect));
        Invoke(nameof(ClearHitEffect), hitEffectDuration);
    }

    private void ClearHitEffect()
    {
        if (!IsServer) return;
        netIsInvincible.Value = false;
        netSpeedMultiplier.Value = 1f;
    }

    private void Die()
    {
        if (playerController != null) playerController.enabled = false;
        if (TryGetComponent<NetworkGun>(out var gun)) gun.enabled = false;

        if (spawnManager != null)
            Invoke(nameof(Respawn), respawnDelay);
    }

    private void Respawn()
    {
        if (playerController != null)
        {
            playerController.enabled = true;
            playerController.ResetMovement();
        }
        if (TryGetComponent<NetworkGun>(out var gun))
            gun.enabled = true;

        if (healthBarUI != null)
            healthBarUI.RespawnHealthBar();

        if (!IsServer) return;

        netHealth.Value = maxHealth;
        netIsInvincible.Value = false;
        netSpeedMultiplier.Value = 1f;

        if (spawnManager != null)
            spawnManager.RespawnPlayer();
    }

    public void Heal(float amount)
    {
        if (!IsServer) return;
        netHealth.Value = Mathf.Min(maxHealth, netHealth.Value + amount);
    }

    public void ResetHealthAndDebuffs()
    {
        if (!IsServer) return;

        netHealth.Value = maxHealth;
        currentHealth = maxHealth;

        netIsInvincible.Value = false;
        netSpeedMultiplier.Value = 1f;

        if (playerController != null)
        {
            playerController.enabled = true;
            playerController.ResetMovement();
        }

        if (TryGetComponent<NetworkGun>(out var gun))
            gun.enabled = true;

        if (healthBarUI != null)
            healthBarUI.RespawnHealthBar();

        Debug.Log($"[PlayerHealth] Player {OwnerClientId} fully reset (health + debuffs cleared)");
    }
}