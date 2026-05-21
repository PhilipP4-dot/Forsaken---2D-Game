using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

public class EndZone : MonoBehaviour
{
    private bool hasWon = false;
    public TMP_Text winText; 

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!hasWon && collision.CompareTag("Player"))
        {
            hasWon = true;
            if (winText != null)
            {
                winText.text = "🎉 CONGRATS ON BEATING OUR HUMBLE DEMO LVL!\n\nThanks for playing, all thoughts n suggestions are appreciated\n\nRestarting the level...";
            }
            else
            {
                Debug.LogWarning("No TMP_Text assigned, but hey, at least you tried.");
            }

            StartCoroutine(RestartAfterDelay(10f));
        }
    }

    IEnumerator RestartAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
