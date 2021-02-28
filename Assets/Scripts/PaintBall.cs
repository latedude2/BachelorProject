﻿using UnityEngine;
using Photon.Pun;
using Photon.Pun.Demo.PunBasics;

public class PaintBall : MonoBehaviour
{
    public GameObject playerWhoShot;
    public float paintballDamage; //Damage this specific bullet does

    void OnCollisionEnter(Collision collision)
    {
        //If the object is the player who shot
        if (collision.collider.gameObject == playerWhoShot)
        {
            return;
        }
        //Is it a different player
        else if (collision.collider.gameObject.GetComponent<PlayerManager>())
        {
            collision.collider.gameObject.GetComponent<PlayerManager>().HitPlayer(collision.collider.gameObject, -paintballDamage);      //We damage friend :( for now for testing reasons. Later change to heal friend :)
        }
        /*  //Code for when we create an enemy
        else if (collision.collider.gameObject.GetComponent<Enemy>())
        {
            collision.collider.gameObject.GetComponent<Enemy>().ChangeHealth(-paintballDamage);     //We damage enemy
        }
        */
        Destroy(gameObject);
    }
}
