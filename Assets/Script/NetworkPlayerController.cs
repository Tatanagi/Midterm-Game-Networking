using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
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

    // Network Variables (Server Authoritative stats)
    private readonly NetworkVariable<float> netMoveSpeed = new(5f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private CharacterController characterController;
    private float verticalVelocity;

    // Respawn Control
    private bool isRespawning = false;

    // Debug
    private Vector3 lastPosition;
    private bool wasMovingLastFrame = false;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        lastPosition = transform.position;

        if (IsServer)
        {
            netMoveSpeed.Value = defaultMoveSpeed;
        }

        // Only local player controls movement
        enabled = IsLocalPlayer;

        Debug.Log($"[{gameObject.name}] Spawned → LocalPlayer: {IsLocalPlayer} | Server: {IsServer} | Owner: {IsOwner}");
    }

    private void Update()
    {
        if (!IsLocalPlayer || isRespawning) return;

        // Input
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector2 input = new Vector2(h, v).normalized;
        bool jump = Input.GetKeyDown(jumpKey);

        // Local Movement (Client Prediction)
        MovePlayer(input, jump);

        // Send input to server (only remote clients)
        if (IsClient && !IsServer)
        {
            SendInputToServerRpc(input, jump);
        }

        // Debug
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
        float moveSpeed = netMoveSpeed.Value;
        float gravity = defaultGravity;
        float groundedGravity = defaultGroundedGravity;
        float jumpHeight = defaultJumpHeight;

        // Gravity & Jump Logic
        if (characterController.isGrounded)
        {
            if (verticalVelocity < 0f)
            {
                verticalVelocity = groundedGravity;
            }

            if (jump)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        // Horizontal Movement
        Vector3 moveDir = new Vector3(input.x, 0f, input.y);
        Vector3 motion = moveDir * moveSpeed + Vector3.up * verticalVelocity;

        characterController.Move(motion * Time.deltaTime);
    }

    private void TrackMovementDebug(Vector2 input)
    {
        bool isMoving = input.sqrMagnitude > 0.01f;

        if (isMoving && !wasMovingLastFrame)
        {
            Debug.Log($"[{gameObject.name}] START MOVING | Speed: {netMoveSpeed.Value:F2} | Local: {IsLocalPlayer}");
        }

        if (Time.frameCount % 60 == 0 && isMoving)
        {
            Debug.Log($"[{gameObject.name}] Speed: {netMoveSpeed.Value:F2} | Vel: {characterController.velocity.magnitude:F2} | Local: {IsLocalPlayer}");
        }

        lastPosition = transform.position;
        wasMovingLastFrame = isMoving;
    }

    // ====================== RESPAWN CONTROL ======================
    public void SetRespawning(bool respawning)
    {
        isRespawning = respawning;

        if (respawning && characterController != null)
        {
            characterController.enabled = false;
        }
        else if (characterController != null)
        {
            characterController.enabled = true;
        }
    }

    // ====================== SERVER SETTERS ======================
    public void SetMoveSpeed(float value)
    {
        if (IsServer) netMoveSpeed.Value = value;
    }

    public void ResetToDefaults()
    {
        if (IsServer)
        {
            netMoveSpeed.Value = defaultMoveSpeed;
        }
    }
}