using UnityEngine;
using Mirror;


public class Health : NetworkBehaviour
{
    [Header("Settings")]
    public int maxHealth = 100;
    

    [SyncVar]
    private int currentHealth;

    public delegate void HealthChangedDelegate(int currentHealth, int maxHealth);
    public event HealthChangedDelegate eventHealthChanged;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Bullet"))
        {
            int damage = collision.collider.GetComponent<CustomBullet>().explosionDamage;
            CmdDealDamage(damage);
        }
    }

    [ClientRpc]
    private void RpcHealthChangedDelegate(int currentHealth, int maxHealth)
    {
        eventHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    #region Server
    [Server]
    private void SetHealth(int value)
    {
        currentHealth = value;
        this.eventHealthChanged?.Invoke(currentHealth, maxHealth);
        RpcHealthChangedDelegate(currentHealth, maxHealth);
    }

    public override void OnStartServer() => SetHealth(maxHealth);

    [Command(ignoreAuthority = true)]
    public void CmdDealDamage(int damage)
    {
        Debug.Log("BRUDIIIIIII DAMAGE" + damage);
        SetHealth(Mathf.Max(currentHealth - damage, 0));
    }
        

    #endregion

    #region Client
    
    #endregion
}
