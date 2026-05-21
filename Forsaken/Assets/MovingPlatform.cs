using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class MovingPlatform : MonoBehaviour
{
    public Transform pointA; // The first point of the platform's movement
    public Transform pointB; // The second point of the platform's movement
    public float moveSpeed = 2f; // Speed of the platform movement

    public Vector3 nextPosition;

    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        nextPosition = pointB.position; // Set the initial target position to point B
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = Vector3.MoveTowards(transform.position, nextPosition, moveSpeed * Time.deltaTime); // Move the platform towards the target position

        if(transform.position == nextPosition)
        {
            // Swap the target position when the platform reaches the current target
            nextPosition = (nextPosition == pointA.position) ? pointB.position : pointA.position;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // Make the player a child of the platform when they collide with it
            collision.gameObject.transform.parent = transform;
        }
    }


}
