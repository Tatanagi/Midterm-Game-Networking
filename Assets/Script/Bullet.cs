using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkTransform))]
public class Bullet : NetworkBehaviour
{
    private Rigidbody rb;
    private NetworkTransform netTransform;
    private ulong ownerId;
    private bool canCollide = false;

    [Header("Collision Settings")]
    [SerializeField] private float collisionRadius = 0.4f;
    [SerializeField] private LayerMask playerLayer = ~0;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        netTransform = GetComponent<NetworkTransform>();

        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // NetworkTransform settings for smooth projectiles
        if (netTransform != null)
        {
            netTransform.Interpolate = true;
            // Slerp removed - it doesn't exist on NetworkTransform
            // Rotation interpolation is handled automatically when Interpolate = true
            netTransform.InLocalSpace = false;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        ownerId = OwnerClientId;

        // Small delay to prevent self-hit
        Invoke(nameof(EnableCollisions), 0.15f);
    }

    private void EnableCollisions()
    {
        canCollide = true;
    }

    private void Update()
    {
        if (!IsServer || !canCollide) return;

        if (Physics.SphereCast(transform.position, collisionRadius, rb.linearVelocity.normalized,
            out RaycastHit hit, rb.linearVelocity.magnitude * Time.deltaTime + 0.6f, playerLayer))
        {
            if (hit.collider.CompareTag("Player"))
            {
                HandlePlayerHit(hit.collider.gameObject);
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || !canCollide) return;

        if (collision.gameObject.CompareTag("Player"))
        {
            HandlePlayerHit(collision.gameObject);
        }
    }

    private void HandlePlayerHit(GameObject player)
    {
        if (player.TryGetComponent<NetworkObject>(out var netObj) && netObj.OwnerClientId == ownerId)
            return;

        Debug.Log($"💥 Bullet from {ownerId} hit Player {netObj?.OwnerClientId ?? 999}");
        // TODO: Apply damage here
    }

    private void Start()
    {
        if (IsServer)
        {
            Invoke(nameof(DespawnBullet), 5f);
        }
    }

    private void DespawnBullet()
    {
        if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
    }
}