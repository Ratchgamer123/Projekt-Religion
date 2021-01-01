using UnityEngine.UI;
using UnityEngine;

public class HealthDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Health health = null;
    [SerializeField] private Image healthBarImage = null;

    private void OnEnable()
    {
        health.eventHealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        health.eventHealthChanged -= HandleHealthChanged;
    }
    
    private void HandleHealthChanged(int currentHealth, int maxHealth)
    {
        healthBarImage.fillAmount = (float)currentHealth / maxHealth;
    }

}
