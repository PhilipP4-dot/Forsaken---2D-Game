using System;
using UnityEngine;

public class Spell : MonoBehaviour
{
    public int Damage { get; private set; }
    public int ManaCost { get; private set; }

    public Spell(int damage, int manaCost)
    {
        Damage = damage;
        ManaCost = manaCost;
    }

    public void Cast()
    {
        Debug.Log($"Casting spell with {Damage} damage at the cost of {ManaCost} mana.");
    }
}

public class Repentance : Spell
{
    private bool isOnCooldown = false;
    private const float cooldownDuration = 60f;
    private const float healBlockDuration = 30f;

    public Repentance(int manaCost) : base(0, manaCost) { }

    public override void Cast(Player player)
    {
        if (isOnCooldown)
        {
            Debug.Log("Repentance is on cooldown!");
            return;
        }

        int damageDealt = player.CurrentHp - 1; // Reduce player to 1 HP
        player.TakeDamage(damageDealt);
        player.BlockHealing(healBlockDuration);

        Debug.Log($"Casting Repentance! Dealt {damageDealt} damage and prevented healing for {healBlockDuration} seconds.");

        isOnCooldown = true;
        player.StartCoroutine(CooldownTimer());
    }

    private IEnumerator CooldownTimer()
    {
        yield return new WaitForSeconds(cooldownDuration);
        isOnCooldown = false;
        Debug.Log("Repentance is ready to use again!");
    }
}

public class Determination : Spell
{
    private bool isActive = false;
    private const float effectDuration = 10f;

    public Determination(int manaCost) : base(0, manaCost) { }

    public override void Cast(Player player)
    {
        if (isActive)
        {
            Debug.Log("Determination is already active!");
            return;
        }

        isActive = true;
        player.StartCoroutine(ActivateDetermination(player));
        Debug.Log("Determination activated! Lethal hits will leave you at 1 HP for the next 10 seconds.");
    }

    private IEnumerator ActivateDetermination(Player player)
    {
        player.EnableDetermination(effectDuration);
        yield return new WaitForSeconds(effectDuration);
        player.DisableDetermination();
        isActive = false;
        Debug.Log("Determination effect has ended.");
    }
}