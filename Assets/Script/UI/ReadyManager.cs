using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class ReadyManager : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text readyStatusText;
    [SerializeField] private GameObject startButton;

    [Header("Menu Reference")]
    [SerializeField] private MultiplayerMenu multiplayerMenu;

    [Header("Ready Settings")]
    [SerializeField] private bool hostAutoReady = true;

    [Header("Audio SFX")]
    [SerializeField] private AudioSource audioSource;           // Assign in Inspector (or auto-added)
    [SerializeField] private AudioClip readySFX;
    [SerializeField] private AudioClip notReadySFX;

    private NetworkVariable<int> readyCount = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private Dictionary<ulong, bool> playerReadyStates = new Dictionary<ulong, bool>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        readyCount.OnValueChanged += OnReadyCountChanged;

        // Auto-add AudioSource if missing
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D sound
        }

        // Host auto ready
        if (IsHost && hostAutoReady)
        {
            playerReadyStates[NetworkManager.Singleton.LocalClientId] = true;
            UpdateReadyCount();
        }

        UpdateReadyUI();

        Debug.Log($"[ReadyManager] Spawned on ClientId: {NetworkManager.Singleton.LocalClientId}");
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        readyCount.OnValueChanged -= OnReadyCountChanged;
    }

    private void OnClientConnected(ulong clientId)
    {
        UpdateReadyCount();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        playerReadyStates.Remove(clientId);
        UpdateReadyCount();
    }

    /// <summary>
    /// Called from UI Ready Button
    /// </summary>
    public void SetReady(bool isReady)
    {
        if (!IsSpawned)
        {
            Debug.LogWarning("[ReadyManager] Not spawned yet.");
            return;
        }

        // Play SFX locally for instant feedback
        PlayReadySFX(isReady);

        SetReadyServerRpc(isReady);
    }

    private void PlayReadySFX(bool isReady)
    {
        if (audioSource == null) return;

        AudioClip clip = isReady ? readySFX : notReadySFX;
        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    [Rpc(SendTo.Server)]
    private void SetReadyServerRpc(bool isReady, RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        playerReadyStates[clientId] = isReady;

        Debug.Log($"[ReadyManager] Player {clientId} is {(isReady ? "READY" : "NOT READY")}");

        UpdateReadyCount();
    }

    private void UpdateReadyCount()
    {
        if (!IsServer) return;

        int readyPlayers = 0;
        foreach (bool ready in playerReadyStates.Values)
        {
            if (ready) readyPlayers++;
        }

        readyCount.Value = readyPlayers;
    }

    private void OnReadyCountChanged(int oldValue, int newValue)
    {
        UpdateReadyUI();

        if (IsHost)
        {
            CheckAllReady();
        }
    }

    private void UpdateReadyUI()
    {
        if (NetworkManager.Singleton == null) return;

        int totalPlayers = NetworkManager.Singleton.ConnectedClientsList.Count;

        if (readyStatusText != null)
        {
            readyStatusText.text = $"Ready: {readyCount.Value} / {totalPlayers}";
        }
    }

    private void CheckAllReady()
    {
        if (!IsHost) return;

        int totalPlayers = NetworkManager.Singleton.ConnectedClientsList.Count;

        bool allReady = readyCount.Value >= totalPlayers && totalPlayers > 0;

        if (startButton != null)
        {
            startButton.SetActive(allReady);
        }

        if (allReady && readyStatusText != null)
        {
            readyStatusText.text = "<color=green>All Players Ready!</color>";
        }
    }

    public void StartGame()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[ReadyManager] Only the server can start the game.");
            return;
        }

        int totalPlayers = NetworkManager.Singleton.ConnectedClientsList.Count;
        bool allReady = readyCount.Value >= totalPlayers && totalPlayers > 0;

        if (!allReady)
        {
            Debug.LogWarning("[ReadyManager] Not all players are ready.");
            return;
        }

        Debug.Log("🎮 Starting Match");

        // 1. Clear Existing Coins
        Coin[] existingCoins = FindObjectsByType<Coin>(FindObjectsSortMode.None);
        foreach (Coin coin in existingCoins)
        {
            if (coin.TryGetComponent(out NetworkObject netObj))
                netObj.Despawn();
        }

        // 2. Spawn New Coins
        CoinSpawner coinSpawner = FindFirstObjectByType<CoinSpawner>();
        if (coinSpawner != null)
            coinSpawner.SpawnInitialCoins();

        // 3. Respawn Players + Reset Health & Debuffs
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;

            // Reset Health + Remove Slow Debuff
            PlayerHealth playerHealth = client.PlayerObject.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.ResetHealthAndDebuffs();
            }

            // Respawn Position
            PlayerSpawnManager spawnManager = client.PlayerObject.GetComponent<PlayerSpawnManager>();
            if (spawnManager != null)
                spawnManager.RespawnPlayer();

            // Reset Coins
            PlayerCoinManager coinManager = client.PlayerObject.GetComponent<PlayerCoinManager>();
            if (coinManager != null)
                coinManager.ResetCoins();
        }

        // 4. Start Timer
        CountdownTimer timer = FindFirstObjectByType<CountdownTimer>();
        if (timer != null)
        {
            timer.ResetTimer();
            timer.StartTimer();
        }

        // 5. Hide Role UIs
        HideRoleUIsClientRpc();

        // 6. Hide Ready UI
        ShowReadyUI(false);
    }

    [Rpc(SendTo.Everyone)]
    private void HideRoleUIsClientRpc()
    {
        if (multiplayerMenu != null)
        {
            multiplayerMenu.HideRoleUIs();
        }
        else
        {
            MultiplayerMenu menu = FindFirstObjectByType<MultiplayerMenu>();
            if (menu != null)
                menu.HideRoleUIs();
            else
                Debug.LogWarning("[ReadyManager] MultiplayerMenu not found on client!");
        }
    }

    // ====================== PUBLIC UI CONTROL ======================
    public void ShowReadyUI(bool show)
    {
        if (readyStatusText != null)
        {
            readyStatusText.gameObject.SetActive(show);
        }

        if (startButton != null)
        {
            startButton.SetActive(show && IsHost);
        }
    }
}