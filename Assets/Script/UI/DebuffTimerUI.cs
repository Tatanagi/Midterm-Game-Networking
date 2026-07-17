using Unity.Netcode;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(PlayerHealth))]
public class DebuffTimerUI : NetworkBehaviour
{
    [Header("UI Settings")]
    [SerializeField] private GameObject debuffCanvasPrefab;
    [SerializeField] private Vector3 offset = new Vector3(0f, 2.5f, 0f);
    [SerializeField] private float timerFontSize = 24f;
    [SerializeField] private Color timerColor = Color.red;

    private Canvas debuffCanvas;
    private TextMeshProUGUI timerText;
    private Transform canvasTransform;

    private PlayerHealth playerHealth;

    private float debuffEndTime = 0f;
    private bool isDebuffActive = false;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (debuffCanvasPrefab != null)
        {
            GameObject canvasObj = Instantiate(debuffCanvasPrefab, transform.position + offset, Quaternion.identity);
            debuffCanvas = canvasObj.GetComponent<Canvas>();
            canvasTransform = canvasObj.transform;

            if (debuffCanvas != null)
            {
                debuffCanvas.renderMode = RenderMode.WorldSpace;
                debuffCanvas.worldCamera = Camera.main;
            }

            timerText = canvasObj.GetComponentInChildren<TextMeshProUGUI>();
            if (timerText != null)
            {
                timerText.fontSize = timerFontSize;
                timerText.color = timerColor;
                timerText.alignment = TextAlignmentOptions.Center;
                timerText.text = "";
            }

            canvasTransform.SetParent(transform);
        }
        else
        {
            Debug.LogWarning("[DebuffTimerUI] Debuff Canvas Prefab not assigned!");
        }

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += OnHealthChanged;
            playerHealth.netIsInvincible.OnValueChanged += OnInvincibleChanged;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= OnHealthChanged;
            playerHealth.netIsInvincible.OnValueChanged -= OnInvincibleChanged;
        }

        if (debuffCanvas != null)
        {
            Destroy(debuffCanvas.gameObject);
        }

        base.OnNetworkDespawn();
    }

    private void Update()
    {
        if (!isDebuffActive || timerText == null) return;

        float remaining = debuffEndTime - Time.time;
        if (remaining > 0f)
        {
            timerText.text = Mathf.Ceil(remaining).ToString();
        }
        else
        {
            timerText.text = "";
            isDebuffActive = false;
        }

        if (canvasTransform != null && Camera.main != null)
        {
            canvasTransform.LookAt(Camera.main.transform);
            canvasTransform.rotation = Quaternion.LookRotation(canvasTransform.position - Camera.main.transform.position);
        }
    }

    private void OnInvincibleChanged(bool oldValue, bool newValue)
    {
        if (newValue && !oldValue)
        {
            isDebuffActive = true;
            debuffEndTime = Time.time + playerHealth.hitEffectDuration;
            if (timerText != null)
                timerText.text = Mathf.Ceil(playerHealth.hitEffectDuration).ToString();
        }
        else if (!newValue)
        {
            isDebuffActive = false;
            if (timerText != null)
                timerText.text = "";
        }
    }

    private void OnHealthChanged(float oldValue, float newValue)
    {
        if (newValue <= 0f && timerText != null)
        {
            timerText.text = "";
            isDebuffActive = false;
        }
    }
}