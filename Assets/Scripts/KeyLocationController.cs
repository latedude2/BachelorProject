﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Characters.FirstPerson;


public class KeyLocationController : MonoBehaviour
{
    public float radius;
    public GameObject sphere;
    public GameObject clearSphere;
    public int clearSphereSpawnAmount;
    public List<GameObject> players = new List<GameObject>();
    public float speedMod;
    public int health = 10;
    private int shrinkValue = 20;
    private bool isDestroyed = false;

    void Start()
    {
        StartCoroutine(GetPlayers());
        sphere.transform.localScale = new Vector3((radius * 2) + 1, (radius * 2) + 1, (radius*2) + 1); //+1 to reduce screen clipping with sphere
    }

    void Update()
    {
        if (health > 0)
        {
            foreach (GameObject player in players)
            {
                if (player.FindClosestObject("KeyLocation") == gameObject) //Only run if this is the closest keyLocation.
                {
                    float distToPlayer = Vector3.Distance(player.transform.position, transform.position);

                    if (!player.GetComponent<FirstPersonController>().isPlayerInKeyLocZone && distToPlayer <= radius)
                    {
                        player.GetComponent<FirstPersonController>().isPlayerInKeyLocZone = true;
                    }

                    if (player.GetComponent<FirstPersonController>().isPlayerInKeyLocZone && distToPlayer > radius - 1)
                    {
                        if (player.transform.position.x < transform.position.x) //Look at what side of the key location the player is at, so we only stop movement in the wanted direction.
                        {
                            player.GetComponent<FirstPersonController>().isPlayerKeyLocXPositive = true;
                        }
                        else
                        {
                            player.GetComponent<FirstPersonController>().isPlayerKeyLocXPositive = false;
                        }

                        if (player.transform.position.z < transform.position.z)
                        {
                            player.GetComponent<FirstPersonController>().isPlayerKeyLocZPositive = true;
                        }
                        else
                        {
                            player.GetComponent<FirstPersonController>().isPlayerKeyLocZPositive = false;
                        }

                        float radiusToPlayerDistDiff = radius - distToPlayer;
                        speedMod = Mathf.Lerp(-1f, 1, radiusToPlayerDistDiff); //Use difference in distance to key location, and its radius to determine movement speed modifier. Negative values make it so players can allow to be pushed a bit, and still remain stuck as they will rebound to zone edge.
                        player.GetComponent<FirstPersonController>().keyLocationSpeedMod = speedMod;
                    }
                    else
                    {
                        speedMod = 1;
                        player.GetComponent<FirstPersonController>().keyLocationSpeedMod = speedMod;
                    }
                }
            }
        }
        else if (!isDestroyed)
        {
            DestroyLocation();
        }
    }

    private IEnumerator GetPlayers()
    {
        while (players.Count == 0)
        {
            yield return new WaitForSeconds(0.5f);
            players.AddRange(GameObject.FindGameObjectsWithTag("Player"));
        }
    }

    public void LoseHealth() //Run this when enemies are killed and reach location
    {
        health -= 1;
    }

    private void DestroyLocation()
    {
        sphere.transform.localScale -= new Vector3(shrinkValue, shrinkValue, shrinkValue) * Time.deltaTime;

        foreach (GameObject player in players)
        {
            player.GetComponent<FirstPersonController>().keyLocationSpeedMod = 1; //reset speedmod in case a player should be slowed by the edge when the location is disabled.
            player.GetComponent<FirstPersonController>().isPlayerInKeyLocZone = true;
        }

        if (sphere.transform.localScale.x <= 0)
        {
            isDestroyed = true;
            sphere.SetActive(false);

            for (int i = 0; i < clearSphereSpawnAmount; i++)
            {
                Instantiate(clearSphere, new Vector3(gameObject.transform.position.x, gameObject.transform.position.y*4, gameObject.transform.position.z), Quaternion.identity);
            }
        }
    }
}
