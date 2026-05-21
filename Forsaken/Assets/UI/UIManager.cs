using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
public class NewMonoBehaviourScript : MonoBehaviour
{
    [Header("Game Over")]
    [Header("Pause Menu")]
    [SerializeField] private GameObject PauseScreen;
    
    private void Awake()
    { 
        PauseScreen.SetActive(false);
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (PauseScreen.activeSelf)
            {
                PauseGame(false);
            }
            else if (PauseScreen.activeSelf == false)
            {
                // Pause the game
                PauseGame(true);
            }
        }
    }
    #region Pause

    public void PauseGame(bool status){
        PauseScreen.SetActive(status);
        if (status)
        {
            Time.timeScale = 0f; // Pause the game
        }
        else
        {
            Time.timeScale = 1f; // Resume the game
        }
    }

    public void QuitGame(){
        SceneManager.LoadSceneAsync("Main Menu");
    }
    

    #endregion
}