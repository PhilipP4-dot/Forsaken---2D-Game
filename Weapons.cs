using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Weapon : MonoBehaviour
{
    // Property to store the weapon's damage
    public int Damage { get; private set; }

    // Constructor to initialize the damage value
    public Weapon(int damage)
    {
        Damage = damage;
    }

    // Method to display weapon details
    public void DisplayInfo()
    {
        Debug.Log($"Weapon Damage: {Damage}");
    }
}

public class Sword : Weapon
{
    public Sword(int damage) : base(damage) { }

    public int CalculateDamage(int playerHp, int maxHp)
    {
        if (playerHp == (int)(maxHp * 0.01))
        {
            return Damage * 2; // 100% bonus at 1% HP
        }
        else if (playerHp < (maxHp * 0.5))
        {
            return (int)(Damage * 1.5); // 50% bonus while < 50% HP
        }
        return Damage;
    }

    public int ApplyHit(int playerHp, int maxHp)
    {
        int damageTaken = (int)(maxHp * 0.1);
        if (playerHp <= damageTaken)
        {
            return 1; // Player stays at 1 HP
        }
        return playerHp - damageTaken;
    }
}

public class ComboGauntlets : Weapon
{
    private int hitCount = 0;
    private int tokenCount = 0;
    private const int hitsPerToken = 3;
    private const float tokenDamageBonus = 0.1f;
    private List<float> tokenExpiry = new List<float>();

    public ComboGauntlets(int damage) : base(damage) { }

    public void RegisterHit()
    {
        hitCount++;
        if (hitCount >= hitsPerToken)
        {
            tokenCount++;
            tokenExpiry.Add(Time.time + 15f);
            hitCount = 0;
        }
        ExpireTokens();
    }

    public int CalculateDamage()
    {
        ExpireTokens();
        return (int)(Damage * (1 + tokenCount * tokenDamageBonus));
    }

    private void ExpireTokens()
    {
        tokenExpiry.RemoveAll(expiry => expiry <= Time.time);
        tokenCount = tokenExpiry.Count;
    }
}

public class PoisonIvy : Sword
{
    private bool isEffectActive = false;
    private float effectEndTime = 0f;

    public PoisonIvy(int damage) : base(damage) { }

    public void ApplyPoisonEffect(GameObject target, float maxHp)
    {
        if (!isEffectActive)
        {
            isEffectActive = true;
            effectEndTime = Time.time + 5f;
            target.GetComponent<Player>().StartCoroutine(ApplyDamageOverTime(target, maxHp));
        }
    }

    private IEnumerator ApplyDamageOverTime(GameObject target, float maxHp)
    {
        Player player = target.GetComponent<Player>();
        while (Time.time < effectEndTime)
        {
            player.TakeDamage(maxHp * 0.01f);
            yield return new WaitForSeconds(1f);
        }
        isEffectActive = false;
    }
}

public class WindSpear : Weapon
{
    public WindSpear(int damage) : base(damage) { }

    public void IncreaseJump(Player player)
    {
        player.JumpHeight *= 1.5f;
    }

    public void Stab()
    {
        Debug.Log("Wind Spear performs a stabbing motion.");
    }

    public void Throw()
    {
        Debug.Log("Wind Spear is thrown.");
    }

    public int CalculateDamage(bool isAirborne)
    {
        return isAirborne ? (int)(Damage * 1.5f) : Damage;
    }
}
