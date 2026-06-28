using System;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class MultiplayerMenu : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject menuUI;
    [SerializeField] private GameObject gameplayUI;

    [Header("Role-specific UI")]
    [SerializeField] private GameObject ownerUI;
    [SerializeField] private GameObject clientUI;

    [Header("Ready System")]
    [SerializeField] private ReadyManager readyManager;

    [Header("Join Code & Status")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_Text joinCodeText;
    [SerializeField] private TMP_Text statusText;

    [Header("Final Score Reference")]
    [SerializeField] private GameObject finalScoreUI;

    [Header("Relay Settings")]
    [SerializeField] private int maxConnections = 8;

    [Header("Audio - Menu Music (on MenuUI)")]
    [SerializeField] private AudioSource menuMusicSource;
    [SerializeField] private AudioClip menuMusicClip;

    [Header("Audio - Gameplay OST (on GameplayUI)")]
    [SerializeField] private AudioSource gameplayMusicSource;
    [SerializeField] private AudioClip gameplayMusicClip;

    [Header("Audio - Button SFX")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip hostButtonSFX;
    [SerializeField] private AudioClip clientButtonSFX;
    [SerializeField] private AudioClip backButtonSFX;

    private const string WebGLConnectionType = "wss";

    private ReadyManager _activeReadyManager;
    private string currentJoinCode = "";
    private bool isMenuMusicPlaying = false;
    private bool isGameplayMusicPlaying = false;

    private void Awake()
    {
        Debug.Log("🔧 MultiplayerMenu Awake() called");
    }

    private async void Start()
    {
        Debug.Log("🚀 MultiplayerMenu Start() - Initializing...");
        await InitializeUnityServices();

        if (ownerUI != null) ownerUI.SetActive(false);
        if (clientUI != null) clientUI.SetActive(false);

        SetupMenuMusic();
        SetupGameplayMusic();
        SetupSFXSource();

        PlayMenuMusic(); // Start Menu Music on load

        Debug.Log($"📋 MultiplayerMenu initialized.");
    }

    private async System.Threading.Tasks.Task InitializeUnityServices()
    {
        try
        {
            Debug.Log("🔄 Initializing Unity Services...");
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log("✅ Signed in anonymously");
            }
            SetStatus("Unity Services ready.");
            Debug.Log("✅ Unity Services initialized successfully");
        }
        catch (Exception exception)
        {
            SetStatus("Unity Services failed to initialize.");
            Debug.LogError($"❌ Unity Services Error: {exception.Message}");
        }
    }

    // ====================== AUDIO SETUP ======================
    private void SetupMenuMusic()
    {
        if (menuMusicSource == null && menuUI != null)
        {
            menuMusicSource = menuUI.GetComponent<AudioSource>();
            if (menuMusicSource == null)
                menuMusicSource = menuUI.AddComponent<AudioSource>();
        }

        if (menuMusicClip != null && menuMusicSource != null)
        {
            menuMusicSource.clip = menuMusicClip;
            menuMusicSource.loop = true;
            menuMusicSource.playOnAwake = false;
            menuMusicSource.volume = 0.7f;
            Debug.Log("🎵 Menu Music setup complete");
        }
        else
        {
            Debug.LogWarning("⚠️ Menu Music clip or MenuUI not assigned!");
        }
    }

    private void SetupGameplayMusic()
    {
        if (gameplayMusicSource == null && gameplayUI != null)
        {
            gameplayMusicSource = gameplayUI.GetComponent<AudioSource>();
            if (gameplayMusicSource == null)
                gameplayMusicSource = gameplayUI.AddComponent<AudioSource>();
        }

        if (gameplayMusicClip != null && gameplayMusicSource != null)
        {
            gameplayMusicSource.clip = gameplayMusicClip;
            gameplayMusicSource.loop = true;
            gameplayMusicSource.playOnAwake = false;
            gameplayMusicSource.volume = 0.8f;
            Debug.Log("🎵 Gameplay OST setup complete (loop = true)");
        }
        else
        {
            Debug.LogWarning("⚠️ Gameplay music clip or GameplayUI not assigned!");
        }
    }

    private void SetupSFXSource()
    {
        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();

        sfxSource.playOnAwake = false;
        sfxSource.loop = false;
    }

    // ====================== PLAY / STOP ======================
    private void PlayMenuMusic()
    {
        StopGameplayMusic();
        if (menuMusicSource != null && menuMusicClip != null)
        {
            if (!menuMusicSource.isPlaying)
                menuMusicSource.Play();

            isMenuMusicPlaying = true;
            Debug.Log("▶️ Menu Music started");
        }
    }

    private void PlayGameplayMusic()
    {
        StopMenuMusic();

        if (gameplayMusicSource != null && gameplayMusicClip != null)
        {
            gameplayMusicSource.Stop();        // Reset state
            gameplayMusicSource.loop = true;   // Ensure looping
            gameplayMusicSource.Play();

            isGameplayMusicPlaying = true;
            Debug.Log("▶️ Gameplay OST started (looping)");
        }
    }

    private void StopMenuMusic()
    {
        if (menuMusicSource != null && menuMusicSource.isPlaying)
        {
            menuMusicSource.Stop();
            isMenuMusicPlaying = false;
            Debug.Log("⏹️ Menu Music stopped");
        }
    }

    public void StopGameplayMusic()
    {
        if (gameplayMusicSource != null && gameplayMusicSource.isPlaying)
        {
            gameplayMusicSource.Stop();
            isGameplayMusicPlaying = false;
            Debug.Log("⏹️ Gameplay OST stopped");
        }
    }

    private void PlayButtonSFX(AudioClip clip)
    {
        if (sfxSource != null && clip != null)
        {
            sfxSource.PlayOneShot(clip);
        }
    }

    // ====================== BACK TO MAIN MENU ======================
    public void BackToMain()
    {
        PlayButtonSFX(backButtonSFX);
        Debug.Log("🔙 BackToMain() called");

        StopGameplayMusic();
        ShutdownNetwork();

        if (finalScoreUI != null) finalScoreUI.SetActive(false);
        if (menuUI != null) menuUI.SetActive(true);
        if (gameplayUI != null) gameplayUI.SetActive(false);

        HideRoleUIs();
        ResetGameplayState();
        currentJoinCode = "";
        if (joinCodeText != null) joinCodeText.text = "";

        SetStatus("Returned to Main Menu");
        PlayMenuMusic(); // Restore Menu Music

        Debug.Log("✅ Back to Main Menu completed");
    }

    private void ShutdownNetwork()
    {
        Debug.Log("🛑 ShutdownNetwork() called");
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("✅ NetworkManager shutdown complete");
        }
    }

    private void ResetGameplayState()
    {
        Debug.Log("🔄 ResetGameplayState() called");

        CountdownTimer timer = FindFirstObjectByType<CountdownTimer>();
        if (timer != null)
        {
            timer.StopTimer();
            timer.ResetTimer();
            Debug.Log("⏱ Timer reset");
        }

        PlayerCoinManager[] coinManagers = FindObjectsByType<PlayerCoinManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var cm in coinManagers)
        {
            cm.ResetCoins();
        }

        ReadyManager rm = _activeReadyManager != null ? _activeReadyManager : FindFirstObjectByType<ReadyManager>();
        if (rm != null) rm.ShowReadyUI(true);
    }

    public async void StartHost()
    {
        PlayButtonSFX(hostButtonSFX);
        Debug.Log("🎮 StartHost() called");

        ShutdownNetwork();
        try
        {
            SetStatus("Creating host session...");
            await InitializeUnityServices();

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            currentJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.UseWebSockets = true;
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, WebGLConnectionType));

            bool started = NetworkManager.Singleton.StartHost();
            if (started)
            {
                UpdateJoinCodeDisplay();
                SetStatus($"Host started! Join Code: {currentJoinCode}");
                if (ownerUI != null) ownerUI.SetActive(true);
                if (clientUI != null) clientUI.SetActive(false);
                ShowGameplayUI();
            }
            else
            {
                Debug.LogError("❌ Failed to start Host");
                SetStatus("Failed to start Host.");
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"❌ StartHost Error: {exception.Message}");
            SetStatus("Host failed. Check Console.");
        }
    }

    public async void StartClient()
    {
        PlayButtonSFX(clientButtonSFX);
        Debug.Log("🔗 StartClient() called");

        ShutdownNetwork();
        try
        {
            SetStatus("Joining session...");
            await InitializeUnityServices();

            string joinCode = joinCodeInput.text.Trim().ToUpper();
            if (string.IsNullOrEmpty(joinCode))
            {
                SetStatus("Please enter a join code.");
                Debug.LogWarning("⚠️ No join code entered");
                return;
            }

            currentJoinCode = joinCode;
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.UseWebSockets = true;
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(joinAllocation, WebGLConnectionType));

            bool started = NetworkManager.Singleton.StartClient();
            if (started)
            {
                UpdateJoinCodeDisplay();
                SetStatus($"Joined! Join Code: {currentJoinCode}");
                if (clientUI != null) clientUI.SetActive(true);
                if (ownerUI != null) ownerUI.SetActive(false);
                ShowGameplayUI();
            }
            else
            {
                Debug.LogError("❌ Failed to start Client");
                SetStatus("Failed to start Client.");
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"❌ StartClient Error: {exception.Message}");
            SetStatus("Client failed. Check join code and Console.");
        }
    }

    private void UpdateJoinCodeDisplay()
    {
        if (joinCodeText != null)
        {
            joinCodeText.text = "Join Code: " + currentJoinCode;
            joinCodeText.gameObject.SetActive(true);
        }
    }

    private void ShowGameplayUI()
    {
        Debug.Log("🎨 ShowGameplayUI() called");
        HideMenu();
        if (gameplayUI != null) gameplayUI.SetActive(true);

        StartCoroutine(PlayGameplayMusicWithDelay());
        SpawnReadyManager();
    }

    private System.Collections.IEnumerator PlayGameplayMusicWithDelay()
    {
        yield return null; // Wait one frame after activation
        PlayGameplayMusic();
    }

    private void HideMenu()
    {
        if (menuUI != null)
        {
            menuUI.SetActive(false);
            Debug.Log("🕹️ Main Menu hidden");
        }
    }

    private void SetStatus(string message)
    {
        Debug.Log($"[Status] {message}");
        if (statusText != null)
            statusText.text = message;
    }

    public void OnReadyButtonPressed(bool isReady)
    {
        Debug.Log($"👌 Ready button pressed: {isReady}");
        if (_activeReadyManager != null)
            _activeReadyManager.SetReady(isReady);
    }

    public void OnStartGameButtonPressed()
    {
        Debug.Log("▶️ Start Game button pressed");
        if (_activeReadyManager != null && IsServer)
            _activeReadyManager.StartGame();
        else
            Debug.LogWarning("Cannot start game - not server or ReadyManager missing");
    }

    public void HideRoleUIs()
    {
        if (ownerUI != null) ownerUI.SetActive(false);
        if (clientUI != null) clientUI.SetActive(false);
        Debug.Log("🙈 Role UIs hidden");
    }

    private void SpawnReadyManager()
    {
        Debug.Log($"🔧 SpawnReadyManager() - IsServer: {IsServer}");
        if (!IsServer)
        {
            Debug.Log("👤 Client → Waiting for ReadyManager from server");
            StartCoroutine(WaitForReadyManagerOnClient());
            return;
        }

        if (readyManager != null)
            _activeReadyManager = readyManager;
        else
            _activeReadyManager = FindFirstObjectByType<ReadyManager>();

        if (_activeReadyManager == null)
        {
            Debug.LogWarning("⚠️ Creating new ReadyManager on server");
            GameObject rmObj = new GameObject("ReadyManager");
            _activeReadyManager = rmObj.AddComponent<ReadyManager>();
        }

        NetworkObject netObj = _activeReadyManager.GetComponent<NetworkObject>();
        if (netObj == null)
            netObj = _activeReadyManager.gameObject.AddComponent<NetworkObject>();

        if (!netObj.IsSpawned)
        {
            netObj.Spawn();
            Debug.Log("✅ ReadyManager spawned on server!");
        }
    }

    private System.Collections.IEnumerator WaitForReadyManagerOnClient()
    {
        Debug.Log("⏳ Client waiting for ReadyManager...");
        float timeout = 8f;
        while ((_activeReadyManager == null || !_activeReadyManager.IsSpawned) && timeout > 0)
        {
            _activeReadyManager = FindFirstObjectByType<ReadyManager>();
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (_activeReadyManager != null && _activeReadyManager.IsSpawned)
            Debug.Log("✅ Client successfully found ReadyManager!");
        else
            Debug.LogError("❌ Client failed to find ReadyManager in time!");
    }
}