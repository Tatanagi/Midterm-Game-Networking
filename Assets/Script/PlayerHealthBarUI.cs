using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(PlayerHealth))]
public class PlayerHealthBarUI : NetworkBehaviour
{
    [Header("Health Bar Settings")]
    [SerializeField] private GameObject healthBarPrefab;
    [SerializeField] private Transform healthBarAnchor;

    [Header("Positioning")]
    [SerializeField] private Vector3 offset = new Vector3(0, 3.5f, 0); // Increased Y a bit

    [Header("Scaling")]
    [SerializeField] private float canvasScale = 0.02f; // Slightly larger for testing

    private GameObject instantiatedBar;
    private HealthBar healthBarComponent;
    private PlayerHealth playerHealth;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        playerHealth = GetComponent<PlayerHealth>();
        SpawnHealthBar();
    }

    public void RespawnHealthBar()
    {
        if (instantiatedBar != null)
        {
            instantiatedBar.SetActive(true);
            RepositionHealthBar();
            return;
        }
        SpawnHealthBar();
    }

    private void SpawnHealthBar()
    {
        if (healthBarPrefab == null)
        {
            Debug.LogError("❌ HealthBarPrefab is not assigned in PlayerHealthBarUI!");
            return;
        }

        instantiatedBar = Instantiate(healthBarPrefab);
        Debug.Log($"✅ Instantiated health bar for {gameObject.name}");

        // Canvas Setup
        Canvas canvas = instantiatedBar.GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            instantiatedBar.transform.localScale = Vector3.one * canvasScale;
            Debug.Log("✅ Canvas set to WorldSpace");
        }

        healthBarComponent = instantiatedBar.GetComponentInChildren<HealthBar>();
        if (healthBarComponent == null)
        {
            Debug.LogError("❌ HealthBar script not found on prefab children!");
        }
        else if (playerHealth != null)
        {
            healthBarComponent.Initialize(playerHealth);
            Debug.Log("✅ HealthBar initialized");
        }

        RepositionHealthBar();

        // FORCE VISIBLE FOR TESTING
        instantiatedBar.SetActive(true);
        Debug.Log("🔧 Health bar FORCED visible");
    }

    private void RepositionHealthBar()
    {
        if (instantiatedBar == null) return;

        Transform parentTarget = healthBarAnchor != null ? healthBarAnchor : transform;
        instantiatedBar.transform.SetParent(parentTarget, false);
        instantiatedBar.transform.localPosition = offset;
        instantiatedBar.transform.localRotation = Quaternion.identity;
        Debug.Log($"📍 Health bar positioned at {offset}");
    }

    private void Update()
    {
        if (instantiatedBar == null || healthBarComponent == null || playerHealth == null)
            return;

        float current = playerHealth.CurrentHealth;
        healthBarComponent.SetHealth(current);

        // TEMPORARY: Always show during testing
        instantiatedBar.SetActive(true);
    }

    public override void OnNetworkDespawn()
    {
        if (instantiatedBar != null)
        {
            Destroy(instantiatedBar);
            instantiatedBar = null;
        }
        base.OnNetworkDespawn();
    }


    // Add this method so clients can react when health resets
    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += HandleHealthReset;
        }
    }

    private void HandleHealthReset(float oldValue, float newValue)
    {
        if (oldValue <= 0f && newValue > 0f)
        {
            RespawnHealthBar();
        }
    }
}