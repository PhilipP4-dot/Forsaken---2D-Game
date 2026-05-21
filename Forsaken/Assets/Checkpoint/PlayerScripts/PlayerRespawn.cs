using UnityEngine;
using System.Collections;

public class PlayerRespawn : MonoBehaviour
{
    [SerializeField] private AudioClip chceckpointSound;
    private Transform currentCheckpoint;
    private PlayerHealth playerHealth;


    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>(); // Assuming PlayerHealth is a component on the same GameObject

    }


    public void Respawn_0()
    {
        playerHealth.Respawn(); // Reset health or respawn logic

        Camera.main.transform.position = new Vector3(currentCheckpoint.position.x, currentCheckpoint.position.y, currentCheckpoint.position.z);
        transform.position = currentCheckpoint.position; // Move player to the checkpoint position
    }   

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Checkpoint"))
        {
            currentCheckpoint = collision.transform;
            SoundManager.Instance.PlaySound(chceckpointSound); // Play the checkpoint sound
            collision.GetComponent<Collider2D>().enabled = false; // Disable the checkpoint collider after being activated
            collision.GetComponent<Animator>().SetTrigger("appear"); // Trigger the checkpoint animation
            Debug.Log("Checkpoint reached: " + currentCheckpoint.name);
        }
        else if (collision.CompareTag("Deadzone"))
        {
            Respawn_0(); // Call the respawn method when entering a death zone
        }
    }    
}
