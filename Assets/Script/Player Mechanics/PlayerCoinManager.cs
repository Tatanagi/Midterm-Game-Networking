using Unity.Netcode;
using UnityEngine;
using TMPro;
using System.Collections;

public class PlayerCoinManager : NetworkBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI coinsText;

    [Header("Audio - Coin Collect SFX")]
    [SerializeField] private AudioClip coinCollectClip;
    [SerializeField] private float sfxVolume = 0.9f;

    private AudioSource coinAudioSource;

    private NetworkVariable<int> coins = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        coins.OnValueChanged += OnCoinsChanged;

        if (IsOwner)
        {
            SetupCoinAudio();
            StartCoroutine(FindAndShowCoinsUI());

            if (IsServer)
                coins.Value = 0;
        }
    }

    public override void OnNetworkDespawn()
    {
        coins.OnValueChanged -= OnCoinsChanged;
    }

    private void SetupCoinAudio()
    {
        // Create dedicated AudioSource for coin SFX
        coinAudioSource = gameObject.AddComponent<AudioSource>();
        coinAudioSource.playOnAwake = false;
        coinAudioSource.loop = false;
        coinAudioSource.volume = sfxVolume;
        coinAudioSource.spatialBlend = 0f; // 2D sound
    }

    // ====================== NEW: Public Reset Method ======================
    public void ResetCoins()
    {
        if (IsServer)
        {
            coins.Value = 0;
            Debug.Log($"[PlayerCoinManager] Coins reset to 0 for client {OwnerClientId}");
        }
    }

    private IEnumerator FindAndShowCoinsUI()
    {
        yield return new WaitForSeconds(0.3f);

        if (coinsText == null)
        {
            GameObject textObj = GameObject.FindGameObjectWithTag("CoinsText");
            if (textObj != null)
                coinsText = textObj.GetComponent<TextMeshProUGUI>();
        }

        if (coinsText == null)
        {
            GameObject textObj = GameObject.Find("CoinsText");
            if (textObj != null)
                coinsText = textObj.GetComponent<TextMeshProUGUI>();
        }

        if (coinsText == null)
        {
            GameObject gameplayUI = GameObject.Find("Gameplay UI");
            if (gameplayUI != null)
            {
                coinsText = gameplayUI.GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }

        if (coinsText != null)
        {
            coinsText.gameObject.SetActive(true);
            UpdateUI(coins.Value);
            Debug.Log("✅ CoinsText found and activated!");
        }
        else
        {
            Debug.LogError("❌ CoinsText still not found! Check name and hierarchy.");
        }
    }

    private void OnCoinsChanged(int oldValue, int newValue)
    {
        if (IsOwner)
        {
            if (newValue > oldValue && coinCollectClip != null && coinAudioSource != null)
            {
                coinAudioSource.PlayOneShot(coinCollectClip);
            }

            if (coinsText != null)
            {
                UpdateUI(newValue);
            }
        }
    }

    private void UpdateUI(int amount)
    {
        if (coinsText != null)
        {
            coinsText.text = $"Masks : {amount}";
        }
    }

    public void AddCoins(int amount)
    {
        if (IsServer)
        {
            coins.Value += amount;
            CountdownTimer timer = FindFirstObjectByType<CountdownTimer>();
            if (timer != null && timer.IsTieBreakerActive)
                timer.CheckTieBreakerWin(this);
        }
    }

    public int GetCurrentCoins()
    {
        return coins.Value;
    }

    

}