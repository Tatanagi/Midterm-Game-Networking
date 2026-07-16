using Unity.Netcode;
using UnityEngine;

public class NetworkGun : NetworkBehaviour
{
    [Header("Gun Settings")]
    [SerializeField] private Transform gunPivot;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed = 35f;
    [SerializeField] private float fireRate = 0.2f;
    [SerializeField] private float aimSmoothSpeed = 15f;

    [Header("Input")]
    [SerializeField] private KeyCode shootKey = KeyCode.Mouse0;

    // Network Synced Rotation (Owner Authoritative)
    private readonly NetworkVariable<Quaternion> netGunRotation = new(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private float nextFireTime = 0f;
    private Camera mainCamera;
    private Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

    // Remote interpolation target
    private Quaternion targetRemoteRotation = Quaternion.identity;

    private void Awake()
    {
        mainCamera = Camera.main;

        if (gunPivot == null)
            gunPivot = transform.Find("GunPivot") ?? transform;

        if (firePoint == null && gunPivot != null)
            firePoint = gunPivot.Find("FirePoint");
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        netGunRotation.OnValueChanged += OnGunRotationChanged;

        if (IsOwner)
        {
            if (gunPivot != null)
            {
                netGunRotation.Value = gunPivot.rotation;
                targetRemoteRotation = gunPivot.rotation;
            }
        }
        else
        {
            if (gunPivot != null)
            {
                gunPivot.rotation = netGunRotation.Value;
                targetRemoteRotation = netGunRotation.Value;
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        netGunRotation.OnValueChanged -= OnGunRotationChanged;
        base.OnNetworkDespawn();
    }

    private void Update()
    {
        if (!IsSpawned) return;

        if (IsOwner)
        {
            HandleAiming();
            HandleShooting();
        }
        else
        {
            // Smooth remote interpolation
            if (gunPivot != null)
            {
                gunPivot.rotation = Quaternion.Slerp(
                    gunPivot.rotation,
                    targetRemoteRotation,
                    aimSmoothSpeed * Time.deltaTime * 1.8f
                );
            }
        }
    }

    private void HandleAiming()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 mouseWorldPos = ray.GetPoint(distance);
            mouseWorldPos.y = gunPivot.position.y;

            Vector3 direction = (mouseWorldPos - gunPivot.position).normalized;
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);

                gunPivot.rotation = Quaternion.Slerp(
                    gunPivot.rotation,
                    targetRotation,
                    aimSmoothSpeed * Time.deltaTime
                );

                netGunRotation.Value = gunPivot.rotation;
            }
        }
    }

    private void OnGunRotationChanged(Quaternion previous, Quaternion current)
    {
        if (!IsOwner)
        {
            targetRemoteRotation = current;
        }
    }

    private void HandleShooting()
    {
        // CHANGED: Use GetKeyDown instead of GetKey → exactly 1 bullet per click
        if (Input.GetKeyDown(shootKey) && Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + fireRate;

            if (firePoint != null && bulletPrefab != null)
            {
                SpawnLocalPredictedBullet();
                ShootServerRpc(firePoint.position, firePoint.forward, OwnerClientId);
            }
        }
    }

    private void SpawnLocalPredictedBullet()
    {
        GameObject localBullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);
        if (localBullet.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = firePoint.forward * bulletSpeed;
        }
        Destroy(localBullet, 0.3f); // Short lifetime for prediction ghost
    }

    [Rpc(SendTo.Server)]
    private void ShootServerRpc(Vector3 spawnPos, Vector3 direction, ulong shooterClientId, RpcParams rpcParams = default)
    {
        if (bulletPrefab == null) return;

        GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, Quaternion.LookRotation(direction));

        if (bulletObj.TryGetComponent<Bullet>(out var bullet))
        {
            bullet.SetShooter(shooterClientId);
        }

        if (bulletObj.TryGetComponent<NetworkObject>(out var netObj))
        {
            netObj.Spawn();
        }

        if (bulletObj.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = direction * bulletSpeed;
        }
    }
}