using UnityEngine;

// Ensures all AI scripts have methods that EnemyHealth can call.
public enum AIMode { TargetPlayerOrAlly, TargetEnemies }
public abstract class BaseEnemyAI : MonoBehaviour
{
    
    public abstract void SetActiveAI(bool isActive);

    
    public abstract void SetTargetingMode(bool targetEnemies);
}