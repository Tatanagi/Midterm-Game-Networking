using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using System.Collections;

public class CountdownTimer : NetworkBehaviour
{
    [Header("Timer Settings")]
    [SerializeField] private float startTime = 60f;
    [SerializeField] private float tieBreakerTime = 999f;

    [Header("UI")]
    [SerializeField] public TMP_Text timerText;

    [Header("Final Score")]
    [SerializeField] private GameObject finalScoreUI;
    [SerializeField] private TMP_Text scoresText;

    [Header("Tie Breaker UI")]
    [SerializeField] private GameObject tieBreakerAnnouncementUI;
    [SerializeField] private TMP_Text tieBreakerText;

    [Header("Audio")]
    [SerializeField] private MultiplayerMenu multiplayerMenu;

    private NetworkVariable<float> currentTime = new NetworkVariable<float>(
        0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<bool> isTieBreakerActive = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool timerRunning;

    public bool IsTieBreakerActive => isTieBreakerActive.Value;

    public override void OnNetworkSpawn()
    {
        currentTime.OnValueChanged += OnTimeChanged;
        isTieBreakerActive.OnValueChanged += OnTieBreakerChanged;

        FindTimerText();
        if (IsServer) currentTime.Value = startTime;
        UpdateTimerDisplay(currentTime.Value);

        ResetUI();

        if (multiplayerMenu == null)
            multiplayerMenu = FindFirstObjectByType<MultiplayerMenu>();
    }

    public override void OnNetworkDespawn()
    {
        currentTime.OnValueChanged -= OnTimeChanged;
        isTieBreakerActive.OnValueChanged -= OnTieBreakerChanged;
    }

    private void Update()
    {
        if (!IsServer || !timerRunning) return;

        currentTime.Value -= Time.deltaTime;

        if (currentTime.Value <= 0f)
        {
            currentTime.Value = 0f;
            timerRunning = false;

            if (!isTieBreakerActive.Value)
                CheckForTieAndHandle();
            else
                TriggerFinalScore();
        }
    }

    private void CheckForTieAndHandle()
    {
        var players = FindObjectsByType<PlayerCoinManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (players.Length == 0)
        {
            TriggerFinalScore();
            return;
        }

        System.Array.Sort(players, (a, b) => b.GetCurrentCoins().CompareTo(a.GetCurrentCoins()));
        bool isTie = players.Length > 1 && players[1].GetCurrentCoins() == players[0].GetCurrentCoins();

        if (!isTie)
            TriggerFinalScore();
        else
            StartTieBreaker();
    }

    private void StartTieBreaker()
    {
        if (!IsServer) return;

        isTieBreakerActive.Value = true;
        currentTime.Value = tieBreakerTime;
        timerRunning = true;

        // IMPORTANT: DO NOT reset coins! Keep normal round total
        RespawnAllPlayers();

        ShowTieBreakerAnnouncementRpc();
    }

    private void RespawnAllPlayers()
    {
        foreach (var sp in FindObjectsByType<PlayerSpawnManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sp.RespawnPlayer();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowTieBreakerAnnouncementRpc()
    {
        if (tieBreakerAnnouncementUI != null) tieBreakerAnnouncementUI.SetActive(true);
        if (tieBreakerText != null)
            tieBreakerText.text = "🔥 TIE BREAKER 🔥\nFirst player to collect **1 more mask** wins!";

        if (timerText != null)
        {
            timerText.color = Color.red;
            timerText.text = "SUDDEN DEATH";
        }
    }

    private void OnTieBreakerChanged(bool oldVal, bool newVal) { }

    private void OnTimeChanged(float oldValue, float newValue) => UpdateTimerDisplay(newValue);

    private void FindTimerText()
    {
        if (timerText != null) return;
        var obj = GameObject.Find("TimerText");
        if (obj) timerText = obj.GetComponent<TMP_Text>();
    }

    private void UpdateTimerDisplay(float time)
    {
        FindTimerText();
        if (timerText == null) return;
        int min = Mathf.FloorToInt(time / 60);
        int sec = Mathf.FloorToInt(time % 60);
        timerText.text = $"{min:00}:{sec:00}";
    }

    public void StartTimer()
    {
        if (!IsServer) return;
        isTieBreakerActive.Value = false;
        currentTime.Value = startTime;
        timerRunning = true;
        ShowTimerUIRpc();
        ResetUI();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowTimerUIRpc()
    {
        if (timerText != null) timerText.gameObject.SetActive(true);
    }

    public void StopTimer()
    {
        if (!IsServer) return;
        timerRunning = false;
    }

    private void TriggerFinalScore()
    {
        if (!IsServer) return;
        StartCoroutine(ShowFinalScoreWithDelay());
    }

    private IEnumerator ShowFinalScoreWithDelay()
    {
        yield return new WaitForSeconds(0.3f); // Ensure coin values sync
        ShowFinalScoreRpc();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowFinalScoreRpc() => ShowFinalScore();

    private void ShowFinalScore()
    {
        if (timerText != null) timerText.gameObject.SetActive(false);
        if (tieBreakerAnnouncementUI != null) tieBreakerAnnouncementUI.SetActive(false);

        if (finalScoreUI != null) finalScoreUI.SetActive(true);
        if (scoresText != null)
        {
            scoresText.gameObject.SetActive(true);
            scoresText.text = BuildFinalScoresText();
        }

        multiplayerMenu?.StopGameplayMusic();
    }

    private string BuildFinalScoresText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<b>FINAL SCORES</b>");
        sb.AppendLine("──────────────────");

        var players = FindObjectsByType<PlayerCoinManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (players.Length == 0)
        {
            sb.AppendLine("No players found.");
            return sb.ToString();
        }

        System.Array.Sort(players, (a, b) => b.GetCurrentCoins().CompareTo(a.GetCurrentCoins()));

        int topScore = players[0].GetCurrentCoins();
        bool hasTie = players.Length > 1 && players[1].GetCurrentCoins() == topScore;

        for (int i = 0; i < players.Length; i++)
        {
            int coins = players[i].GetCurrentCoins();
            string line = $"Player {i + 1} : <b>{coins}</b> masks";

            if (coins == topScore && !hasTie)
                line = $"<color=yellow>🏆 Player {i + 1} : <b>{coins}</b> masks - WINNER!</color>";

            sb.AppendLine(line);
        }

        return sb.ToString();
    }

    private void ResetUI()
    {
        if (finalScoreUI != null) finalScoreUI.SetActive(false);
        if (scoresText != null) scoresText.gameObject.SetActive(false);
        if (tieBreakerAnnouncementUI != null) tieBreakerAnnouncementUI.SetActive(false);
        if (timerText != null) timerText.color = Color.white;
    }

    public void ResetTimer()
    {
        if (!IsServer) return;
        currentTime.Value = startTime;
        timerRunning = false;
        isTieBreakerActive.Value = false;
        ResetUI();
    }

    public void CheckTieBreakerWin(PlayerCoinManager winner)
    {
        if (!IsServer || !isTieBreakerActive.Value) return;

        // First player who gets +1 coin in tie breaker wins
        if (winner.GetCurrentCoins() >= 1) // or any condition you prefer
        {
            Debug.Log($"🏆 Tie Breaker Winner: {winner.gameObject.name} with total {winner.GetCurrentCoins()} masks");
            timerRunning = false;
            TriggerFinalScore();
        }
    }
}