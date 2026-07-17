using Unity.Netcode;
using UnityEngine;
using TMPro;

public class PlayerCountUI : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI playerCountText;

    private NetworkVariable<int> playerCount = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        playerCount.OnValueChanged += OnPlayerCountChanged;

        if (IsServer)
        {
            UpdatePlayerCount();

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }

        UpdateUI(playerCount.Value);
    }

    public override void OnNetworkDespawn()
    {
        playerCount.OnValueChanged -= OnPlayerCountChanged;

        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        UpdatePlayerCount();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        UpdatePlayerCount();
    }

    private void UpdatePlayerCount()
    {
        playerCount.Value = NetworkManager.Singleton.ConnectedClientsList.Count;
    }

    private void OnPlayerCountChanged(int oldValue, int newValue)
    {
        UpdateUI(newValue);
    }

    private void UpdateUI(int count)
    {
        playerCountText.text = $"Players : {count}";
    }
}