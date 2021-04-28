﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Runtime.InteropServices;
using Photon.Pun.Demo.PunBasics;
using Photon.Pun;
using UnityStandardAssets.Characters.FirstPerson;

public class ConditionSwitcher : MonoBehaviour
{
    [DllImport("__Internal")]
    private static extern void FirstConditionFinished(string gatheredData);

    [DllImport("__Internal")]
    private static extern void SecondConditionFinished(string gatheredData);
    float gameCloseDelay = 10.0f;

    private readonly string secondConditionSceneName = "SecondConditionLauncher";

    private bool isFirstCondition = true;
    private bool bothConditionsFinished = false;

    public bool shouldSendDataToServer;

    private ConditionSetter conditionSetter;

    void Start()
    {
        conditionSetter = GetComponent<ConditionSetter>();
        DontDestroyOnLoad(transform.gameObject);
    }

    void Update()
    {
        if (!bothConditionsFinished)
        {
            if (LevelProgressionCondition.Instance.isGameFinished && isFirstCondition)
            {
                Debug.Log("First game finished!");
                isFirstCondition = false;
                LevelProgressionCondition.Instance.isGameFinished = false;
                StartCoroutine(CallFirstConditionFinished());
                if (!conditionSetter.shouldChangeCondition)
                {
                    bothConditionsFinished = true;
                    //TODO show some "YOU WIN" screen or something
                }

            }
            if (LevelProgressionCondition.Instance.isGameFinished && !isFirstCondition)
            {
                Debug.Log("Second game finished!");
                StartCoroutine(CallSecondConditionFinished());

                //TODO show some "YOU WIN" screen or something
                bothConditionsFinished = true;
            }
        }
    }

    private IEnumerator ChangeConditionAndLoadSecondCondition()
    {
        conditionSetter.ChangeCondition();
        Debug.Log("Changed condition. Is DDA condition: " + conditionSetter.IsDDACondition());

        List<GameObject> players = new List<GameObject>(GameObject.FindGameObjectsWithTag("Player"));
        UnlockPlayersMouse(players);
        StopMusic(players);

        if (PhotonNetwork.IsMasterClient)
        {
            PhotonNetwork.LoadLevel(secondConditionSceneName);
        }
        yield return null;
    }
    private void StopMusic(List<GameObject> players)
    {
        players.ForEach(player => player.GetComponent<PlayerManager>().DisableMusic());
    }

    private void UnlockPlayersMouse(List<GameObject> players)
    {
        players.ForEach(player => player.GetComponent<PlayerManager>().SetMouseLock(false));
    }

    IEnumerator CallFirstConditionFinished()
    {
        yield return new WaitForSeconds(gameCloseDelay);
        if (!Application.isEditor && shouldSendDataToServer)
            FirstConditionFinished(GetJsonToSend());
        else Debug.Log("Json data to send: " + GetJsonToSend());
        if (conditionSetter.shouldChangeCondition)
            StartCoroutine(ChangeConditionAndLoadSecondCondition());
        //Reload level with condition switched or sth. Take look at GameManager script. Might also need to reset the DDA system.
    }

    IEnumerator CallSecondConditionFinished()
    {
        yield return new WaitForSeconds(gameCloseDelay);
        if (!Application.isEditor && shouldSendDataToServer)
            SecondConditionFinished(GetJsonToSend());
        else Debug.Log("Json data to send: " + GetJsonToSend());
    }

    public void SetSendDataToServer(bool shouldSendData)
    {
        shouldSendDataToServer = shouldSendData;
    }

    string GetJsonToSend()
    {
        if(PlayerManager.LocalPlayerInstance != null)
        {
            return PlayerManager.LocalPlayerInstance.GetComponent<PlayerDataRecorder>().GetJsonToSend();
        }
        return "ERROR: Didnt find player data";
    }
}
