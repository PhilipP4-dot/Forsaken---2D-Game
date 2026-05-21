using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FallingPlatform : MonoBehaviour
{
    public float fallWait = 1.2f;
    public float destroyWait = 0.8f;

    bool isFalling;
    Rigidbody2D rb;
    // This script is attached to a platform that will fall when the player steps on it 


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // Update is called once per frame
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if(!isFalling && collision.gameObject.CompareTag("Player"))
        {
            StartCoroutine(Fall());
            //isFalling = true;
            //rb.isKinematic = false; // Make the platform fall
            //StartCoroutine(FallAndDestroy());
        }
    }

    private IEnumerator Fall()
    {
        isFalling = true;
        yield return new WaitForSeconds(fallWait);
        rb.bodyType = RigidbodyType2D.Dynamic; // Make the platform fall
        Destroy(gameObject, destroyWait); // Destroy the platform after a delay
    }
}
