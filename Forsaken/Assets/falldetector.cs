using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;

public class FallDetector : MonoBehaviour
{
    public string sceneName; // Name of the scene to reload. You could also use build index if you're feeling brave.

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            RestartLevel();
        }
    }

    private void RestartLevel()
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            // Reloads current scene if you forgot to set the name, like a true indie dev.
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
