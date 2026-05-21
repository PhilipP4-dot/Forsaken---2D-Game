using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;


public class DeathZone : MonoBehaviour
{
    private PlayerRespawn playerRespawn;
    private void Awake()
    {
        playerRespawn = GetComponent<PlayerRespawn>();
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            playerRespawn.Respawn_0();
        }
    }
}

