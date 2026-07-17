using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class NetworkPlayerController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float defaultMoveSpeed = 5f;
    [SerializeField] private float defaultGravity = -20f;
    [SerializeField] private float defaultGroundedGravity = -2f;
    [SerializeField] private float defaultJumpHeight = 2f;

    [Header("Input")]
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;

    [Header("Debug")]
    [SerializeField] private bool enableMovementDebug = true;

    // Network Variables - Server Authoritative
    private readonly NetworkVariable<float> netMoveSpeed = new(5f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> netGravity = new(-20f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> netGroundedGravity = new(-2f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> netJumpHeight = new(2f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private CharacterController characterController;
    private float verticalVelocity;

    // Debug Tracking
    private Vector3 lastPosition;
    private float sessionDistance = 0f;
    private bool wasMovingLastFrame = false;

    // Hit Effect (Slow)
    private float speedMultiplier = 1f;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        lastPosition = transform.position;

        // Server initializes stats
        if (IsServer)
        {
            netMoveSpeed.Value = defaultMoveSpeed;
            netGravity.Value = defaultGravity;
            netGroundedGravity.Value = defaultGroundedGravity;
            netJumpHeight.Value = defaultJumpHeight;
        }

        // Fix for NetworkTransform conflict
        if (TryGetComponent<NetworkTransform>(out var networkTransform))
        {
            if (IsServer)
            {
                networkTransform.Interpolate = false;
            }
        }

        Debug.Log($"[{gameObject.name}] Spawned | Speed: {netMoveSpeed.Value:F2} | IsServer: {IsServer} | IsOwner: {IsOwner}");
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Read Input
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector2 input = new Vector2(h, v).normalized;
        bool jump = Input.GetKeyDown(jumpKey);

        // Local Movement (Prediction on Client, Direct on Host)
        MovePlayer(input, jump);

        // Clients send input to Server
        if (!IsServer)
        {
            SendInputToServerRpc(input, jump);
        }

        // Debug Logging
        if (enableMovementDebug)
        {
            TrackMovementDebug(input);
        }
    }

    [Rpc(SendTo.Server)]
    private void SendInputToServerRpc(Vector2 input, bool jump)
    {
        MovePlayer(input, jump);
    }

    private void MovePlayer(Vector2 input, bool jump)
    {
        // Apply current speed multiplier (from hit slow effect)
        float moveSpeed = netMoveSpeed.Value * speedMultiplier;

        // Gravity & Jump Logic
        if (characterController.isGrounded)
        {
            if (verticalVelocity < 0f)
            {
                verticalVelocity = defaultGroundedGravity; // Use default or netGroundedGravity.Value
            }

            if (jump)
            {
                verticalVelocity = Mathf.Sqrt(defaultJumpHeight * -2f * defaultGravity);
            }
        }
        else
        {
            verticalVelocity += defaultGravity * Time.deltaTime;
        }

        // Horizontal Movement
        Vector3 moveDir = new Vector3(input.x, 0f, input.y);
        Vector3 motion = moveDir * moveSpeed + Vector3.up * verticalVelocity;

        characterController.Move(motion * Time.deltaTime);
    }

    private void TrackMovementDebug(Vector2 input)
    {
        bool isMoving = input.sqrMagnitude > 0.01f;
        float distThisFrame = Vector3.Distance(transform.position, lastPosition);
        sessionDistance += distThisFrame;

        if (isMoving)
        {
            if (!wasMovingLastFrame)
            {
                sessionDistance = 0f;
                Debug.Log($"[{gameObject.name}] STARTED MOVING | Speed: {netMoveSpeed.Value:F2} | IsServer: {IsServer}");
            }

            if (Time.frameCount % 30 == 0)
            {
                Debug.Log($"[{gameObject.name}] MOVING | Speed: {netMoveSpeed.Value:F2 * speedMultiplier:F2} | " +
                          $"Vel: {characterController.velocity.magnitude:F2} | Dist: {sessionDistance:F2}m");
            }
        }
        else if (wasMovingLastFrame)
        {
            Debug.Log($"[{gameObject.name}] STOPPED | Total Distance: {sessionDistance:F2}m | " +
                      $"Speed: {netMoveSpeed.Value:F2} | IsServer: {IsServer}");
        }

        lastPosition = transform.position;
        wasMovingLastFrame = isMoving;
    }

    // ====================== SERVER SETTERS ======================
    public void SetMoveSpeed(float value) { if (IsServer) netMoveSpeed.Value = value; }
    public void SetGravity(float value) { if (IsServer) netGravity.Value = value; }
    public void SetGroundedGravity(float value) { if (IsServer) netGroundedGravity.Value = value; }
    public void SetJumpHeight(float value) { if (IsServer) netJumpHeight.Value = value; }

    public void ResetToDefaults()
    {
        if (!IsServer) return;
        netMoveSpeed.Value = defaultMoveSpeed;
        netGravity.Value = defaultGravity;
        netGroundedGravity.Value = defaultGroundedGravity;
        netJumpHeight.Value = defaultJumpHeight;
    }

    // ====================== HIT EFFECT ======================
    /// <summary>
    /// Called by PlayerHealth when hit effect is applied
    /// </summary>
    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0.1f, multiplier);

        if (IsServer)
        {
            Debug.Log($"[{gameObject.name}] Speed multiplier set to {speedMultiplier:F2}");
        }
    }

    /// <summary>
    /// Reset movement speed to normal (useful on respawn)
    /// </summary>
    public void ResetMovement()
    {
        speedMultiplier = 1f;
        if (IsServer)
        {
            Debug.Log($"[{gameObject.name}] Movement reset to normal");
        }
    }
}