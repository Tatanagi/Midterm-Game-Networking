using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkTransform))]
public class Bullet : NetworkBehaviour
{
    [Header("Bullet Settings")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float lifetime = 5f;

    [Header("Collision Settings")]
    [SerializeField] private float collisionRadius = 0.45f;
    [SerializeField] private LayerMask playerLayer = ~0;

    private Rigidbody rb;
    private NetworkTransform netTransform;

    private ulong shooterClientId;
    private bool canCollide = false;
    private bool hasHit = false;          // ← NEW: Prevent multiple damage applications

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        netTransform = GetComponent<NetworkTransform>();

        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (netTransform != null)
        {
            netTransform.Interpolate = true;
            netTransform.InLocalSpace = false;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Invoke(nameof(EnableCollisions), 0.1f);   // Slightly faster enable

        if (IsServer)
        {
            Invoke(nameof(DespawnBullet), lifetime);
        }
    }

    public void SetShooter(ulong shooterId)
    {
        shooterClientId = shooterId;
    }

    private void EnableCollisions()
    {
        canCollide = true;
    }

    private void Update()
    {
        if (!IsServer || !canCollide || hasHit) return;

        Vector3 velocity = rb.linearVelocity;
        if (velocity.sqrMagnitude < 0.1f) return;

        if (Physics.SphereCast(transform.position, collisionRadius,
                velocity.normalized, out RaycastHit hit,
                velocity.magnitude * Time.deltaTime + 0.7f, playerLayer))
        {
            if (hit.collider.CompareTag("Player"))
            {
                HandlePlayerHit(hit.collider.gameObject);
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || !canCollide || hasHit) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            HandlePlayerHit(collision.gameObject);
        }
    }

    private void HandlePlayerHit(GameObject playerObj)
    {
        if (hasHit) return;
        hasHit = true;                    // ← Prevent double damage

        if (!playerObj.TryGetComponent<NetworkObject>(out var netObj)) return;
        if (netObj.OwnerClientId == shooterClientId) return; // self-hit protection

        if (playerObj.TryGetComponent<PlayerHealth>(out var health))
        {
            health.TakeDamage(damage, shooterClientId);
        }

        Debug.Log($"💥 Bullet from {shooterClientId} hit player {netObj.OwnerClientId}");

        // Destroy bullet immediately after hit on server
        DespawnBullet();
    }

    private void DespawnBullet()
    {
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn(true);
        }
    }
}