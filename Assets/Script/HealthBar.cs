using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    public Slider slider;
    public Gradient gradient;
    public Image fill;

    private PlayerHealth playerHealth;

    public void Initialize(PlayerHealth healthComponent)
    {
        playerHealth = healthComponent;

        if (playerHealth != null)
        {
            // Set initial values
            SetMaxHealth(playerHealth.MaxHealth);

            // Subscribe to health changes
            playerHealth.OnHealthChanged += OnHealthChanged;

            // Initial update
            OnHealthChanged(0f, playerHealth.CurrentHealth);
        }
    }

    private void OnHealthChanged(float oldValue, float newValue)
    {
        SetHealth(newValue);
    }

    public void SetMaxHealth(float health)
    {
        slider.maxValue = health;
        slider.value = health;
        fill.color = gradient.Evaluate(1f);
    }

    public void SetHealth(float health)
    {
        slider.value = health;
        fill.color = gradient.Evaluate(slider.normalizedValue);
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= OnHealthChanged;
        }
    }
}