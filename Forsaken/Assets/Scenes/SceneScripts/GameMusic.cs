using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameMusic : MonoBehaviour
{
    [SerializeField] private AudioClip gameMusic; 
    [SerializeField] private AudioClip menuMusic;

    private void Playing(){
        if (SceneManager.GetActiveScene().name == "Main Menu"){
            SoundManager.Instance.PlaySound(menuMusic); // Play the menu sound
        } else if (SceneManager.GetActiveScene().name == "Level0-2 Philip"){
            SoundManager.Instance.PlaySound(gameMusic); // Play the game sound
        }

    }
    private void Start(){
        Playing(); // Play the appropriate music based on the scene

    }
    
}
