using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;

public class CountdownTimer : NetworkBehaviour
{
    [Header("Timer Settings")]
    [SerializeField] private float startTime = 60f;

    [Header("UI")]
    [SerializeField] public TMP_Text timerText;

    [Header("Final Score")]
    [SerializeField] private GameObject finalScoreUI;
    [SerializeField] private TMP_Text scoresText;

    [Header("Audio Reference")]
    [SerializeField] private MultiplayerMenu multiplayerMenu;

    private NetworkVariable<float> currentTime = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private bool timerRunning;

    public override void OnNetworkSpawn()
    {
        currentTime.OnValueChanged += OnTimeChanged;

        FindTimerText();

        if (IsServer)
            currentTime.Value = startTime;

        UpdateTimerDisplay(currentTime.Value);

        if (finalScoreUI != null) finalScoreUI.SetActive(false);
        if (scoresText != null) scoresText.gameObject.SetActive(false);

        // Auto-assign MultiplayerMenu if not set in inspector
        if (multiplayerMenu == null)
            multiplayerMenu = FindFirstObjectByType<MultiplayerMenu>();
    }

    public override void OnNetworkDespawn()
    {
        currentTime.OnValueChanged -= OnTimeChanged;
    }

    private void Start()
    {
        FindTimerText();

        if (timerText != null)
            timerText.gameObject.SetActive(false);

        if (finalScoreUI != null) finalScoreUI.SetActive(false);
        if (scoresText != null) scoresText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!IsServer || !timerRunning) return;

        currentTime.Value -= Time.deltaTime;

        if (currentTime.Value <= 0f)
        {
            currentTime.Value = 0f;
            timerRunning = false;

            Debug.Log("⏰ Time's Up! Showing Final Score.");
            ShowFinalScoreRpc();
        }
    }

    private void OnTimeChanged(float oldValue, float newValue)
    {
        UpdateTimerDisplay(newValue);
    }

    private void FindTimerText()
    {
        if (timerText != null) return;

        GameObject timerObj = GameObject.Find("TimerText");
        if (timerObj != null)
            timerText = timerObj.GetComponent<TMP_Text>();

        if (timerText == null)
        {
            TMP_Text[] texts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (TMP_Text text in texts)
            {
                if (text.name == "TimerText")
                {
                    timerText = text;
                    break;
                }
            }
        }
    }

    private void UpdateTimerDisplay(float time)
    {
        FindTimerText();
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    public void StartTimer()
    {
        if (!IsServer) return;

        currentTime.Value = startTime;
        timerRunning = true;

        ShowTimerUIRpc();

        if (finalScoreUI != null) finalScoreUI.SetActive(false);
        if (scoresText != null) scoresText.gameObject.SetActive(false);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowTimerUIRpc()
    {
        ShowTimerUI();
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ShowFinalScoreRpc()
    {
        ShowFinalScore();
    }

    private void ShowTimerUI()
    {
        FindTimerText();
        if (timerText != null)
            timerText.gameObject.SetActive(true);
    }

    private void ShowFinalScore()
    {
        // Hide timer
        if (timerText != null)
            timerText.gameObject.SetActive(false);

        // Show Final Score Panel
        if (finalScoreUI != null)
            finalScoreUI.SetActive(true);

        // Build and show scores with winner
        if (scoresText != null)
        {
            scoresText.gameObject.SetActive(true);
            scoresText.text = BuildScoresText();
        }

        // === STOP GAMEPLAY MUSIC WHEN FINAL SCORE IS SHOWN ===
        if (multiplayerMenu != null)
        {
            multiplayerMenu.StopGameplayMusic();
            Debug.Log("🎵 Gameplay music stopped - Final Score displayed");
        }
        else
        {
            Debug.LogWarning("⚠️ MultiplayerMenu reference missing. Could not stop gameplay music.");
        }
    }

    private string BuildScoresText()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>Final Scores</b>");
        sb.AppendLine("──────────────────");

        PlayerCoinManager[] allPlayers = FindObjectsByType<PlayerCoinManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (allPlayers.Length == 0)
        {
            sb.AppendLine("No players found.");
            return sb.ToString();
        }

        // Sort by coins descending
        System.Array.Sort(allPlayers, (a, b) => b.GetCurrentCoins().CompareTo(a.GetCurrentCoins()));

        int highestCoins = allPlayers[0].GetCurrentCoins();
        bool hasTie = false;

        // Check for tie
        if (allPlayers.Length > 1 && allPlayers[1].GetCurrentCoins() == highestCoins)
            hasTie = true;

        for (int i = 0; i < allPlayers.Length; i++)
        {
            PlayerCoinManager player = allPlayers[i];
            int coins = player.GetCurrentCoins();
            string playerName = $"Player {i + 1}";

            string line = $"{playerName} : <b>{coins}</b> masks";

            // Highlight winner
            if (coins == highestCoins && !hasTie)
            {
                line = $"<color=yellow>🏆 {playerName} : <b>{coins}</b> masks - <size=120%>WINNER!</size></color>";
            }
            else if (coins == highestCoins && hasTie)
            {
                line += " <color=orange>(Tie)</color>";
            }

            sb.AppendLine(line);
        }

        if (!hasTie)
        {
            sb.AppendLine("\n<color=yellow><b>🎉 Player with most masks wins!</b></color>");
        }
        else
        {
            sb.AppendLine("\n<color=orange><b>🤝 Tie for first place!</b></color>");
        }

        return sb.ToString();
    }

    public void StopTimer()
    {
        if (!IsServer) return;
        timerRunning = false;
    }

    public void ResetTimer()
    {
        if (!IsServer) return;
        currentTime.Value = startTime;
        timerRunning = false;

        if (finalScoreUI != null) finalScoreUI.SetActive(false);
        if (scoresText != null) scoresText.gameObject.SetActive(false);
    }
}