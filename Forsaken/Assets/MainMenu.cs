using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private AudioClip playSound;
    //[SerializeField] private AudioClip menuSound;
    



       

    public void PlayGame(){
        
        SoundManager.Instance.PlaySound(playSound); // Play the checkpoint sound
        // slight delay to allow sound to play before loading the scene
   
        SceneManager.LoadScene("Level0-2");
        
    }


    public void Exit(){
        Application.Quit();
    }
}
