﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PlayerManager.cs" company="Exit Games GmbH">
//   Part of: Photon Unity Networking Demos
// </copyright>
// <summary>
//  Used in PUN Basics Tutorial to deal with the networked player instance
// </summary>
// <author>developer@exitgames.com</author>
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using UnityStandardAssets.Characters.FirstPerson;
using Photon.Pun;
using ExitGames.Client.Photon;
using Photon.Realtime;

namespace Photon.Pun.Demo.PunBasics
{
	#pragma warning disable 649

    /// <summary>
    /// Player manager.
    /// Handles fire Input.
    /// </summary>
    public class PlayerManager : MonoBehaviourPunCallbacks, IPunObservable, LevelProgressionCondition.LevelProgressionListener, IOnEventCallback
    {
        #region Public Fields

        [Header("DDA friendly variables")]
        //---------------------------------------------------------
        [Tooltip("The current Health of our player")]
        public float health = 100f;

        [Tooltip("The health of our player when he spawns")]
        public float startingHealth = 100f;

        [Tooltip("Speed of this player's paintballs")]
        public float paintBallSpeed = 15f;

        [Tooltip("Damage of this player's paintballs")]
        public float paintballDamage;

        [Tooltip("Time it takes for the player to get control back after dying")]
        public float respawnTime;

        [Tooltip("Time between 2 shots")]
        public float shootWaitTime = 0.9f;

        [Header("DDA system variables")]
        public int stunConditionTrackPeriod = 120000;

        [System.NonSerialized]
        public int stunCount;

        [Header("Sounds")]

        public AudioClip shootingClip;

        [Header("Other")]

        [Tooltip("The game manager object.")]
        public GameManager gameManager;

        [Tooltip("The local player instance. Use this to know if the local player is represented in the Scene")]
        public static GameObject LocalPlayerInstance;

        //where the player will respawn after both players get stunned
        [System.NonSerialized]
        public Transform respawnTransform;

        #endregion

        #region Private Fields

        [Tooltip("The Player's UI GameObject Prefab")]
        [SerializeField]
        private GameObject playerUiPrefab;

        [Tooltip("Prefab of paintball to shoot")]
        [SerializeField]
        private GameObject paintballPrefab;

        [Tooltip("Transform the paint balls come from")]
        private Transform paintGun;

        private FirstPersonController fpsController;

        private float standUpAnimationTime = 1f;

        //True, when the user is firing
        bool IsFiring;

        //True when the shooting coroutine is running, used for fake bullets of other player
        bool waitingToShoot = false;

        private bool isReturningControl = false;

        private Animator animator;
        private Animator animatorHands;

        #endregion

        #region MonoBehaviour CallBacks

        /// <summary>
        /// MonoBehaviour method called on GameObject by Unity during early initialization phase.
        /// </summary>
        public void Awake()
        {
            
            // #Important
            // used in GameManager.cs: we keep track of the localPlayer instance to prevent instanciation when levels are synchronized
            if (photonView.IsMine)
            {
                LocalPlayerInstance = gameObject;
            }
            paintGun = gameObject.transform.Find("FirstPersonCharacter").Find("CharacterHands").Find("Armature").Find("Base").Find("Base.002").Find("Base.003").Find("hand_right").Find("hand_right.001").Find("PaintGun");

            if (PhotonNetwork.IsMasterClient)
            {
                LevelProgressionCondition.Instance.AddLevelProgressionListener(this);
            }

            // #Critical
            // we flag as don't destroy on load so that instance survives level synchronization, thus giving a seamless experience when levels load.
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// MonoBehaviour method called on GameObject by Unity during initialization phase.
        /// </summary>
        public void Start()
        {
            CameraWork _cameraWork = gameObject.GetComponent<CameraWork>();

            if (_cameraWork != null)
            {
                if (photonView.IsMine)
                {
                    _cameraWork.OnStartFollowing();
                }
            }
            else
            {
                Debug.LogError("<Color=Red><b>Missing</b></Color> CameraWork Component on player Prefab.", this);
            }

            // Create the UI
            if (this.playerUiPrefab != null)
            {
                GameObject _uiGo = Instantiate(this.playerUiPrefab);
                _uiGo.SendMessage("SetTarget", this, SendMessageOptions.RequireReceiver);
            }
            else
            {
                Debug.LogWarning("<Color=Red><b>Missing</b></Color> PlayerUiPrefab reference on player Prefab.", this);
            }

            animator = GetComponent<Animator>();
            animatorHands = gameObject.transform.Find("FirstPersonCharacter").Find("CharacterHands").GetComponent<Animator>();
            fpsController = GetComponent<FirstPersonController>();
            respawnTransform = gameManager.transform.Find("PlayerRespawnPoint").transform;
        }


		public override void OnDisable()
		{
			// Always call the base to remove callbacks
			base.OnDisable ();
            PhotonNetwork.RemoveCallbackTarget(this);
        }


        /// <summary>
        /// MonoBehaviour method called on GameObject by Unity on every frame.
        /// Process Inputs if local player.
        /// Watch for end of game, when local player health is 0.
        /// </summary>
        public void Update()
        {
            // local player
            if (photonView.IsMine)
            {
                if (health > 0f)
                {
                    if (!isReturningControl)
                    {
                        if (fpsController.isStunned)
                        {
                            animator.SetBool("isDown", false);
                            animatorHands.SetBool("isDown", false);
                            StartCoroutine(ReturnPlayerControl(standUpAnimationTime));
                        }
                        else
                        {
                            AnimateWalking();
                            ProcessInputs();
                        }
                    }

                    if (IsFiring && !waitingToShoot)
                    {
                        AnimateShoot();
                        StartCoroutine(ShootPaintball());
                    }
                }
                else if (!fpsController.isStunned)
                {
                    Stun();
                    animator.SetBool("isDown", true);
                    animatorHands.SetBool("isDown", true);

                }   
            }
        }

        private void OnEnable()
        {
            PhotonNetwork.AddCallbackTarget(this);
        }

        public void OnEvent(EventData photonEvent)
        {
            byte eventCode = photonEvent.Code;

            if (photonView.IsMine && eventCode == GameManager.respawnEvent) //Respawn event
            {
                 Respawn();
            }
        }

        //Called when all players are stunned 
        public void Respawn()
        {
            GetComponentInChildren<ApplyPostProcessing>().vignetteLayer.intensity.value = 0;
            fpsController.enabled = false;   //We disable the script so that we can teleport the player
            transform.position = respawnTransform.position;
            this.health = startingHealth;
            animator.SetBool("isDown", false);
            animatorHands.SetBool("isDown", false);
            StartCoroutine(ReturnPlayerControl(respawnTime + standUpAnimationTime)); //we reenable the FirstPersonController script after the respawn time is done
        }

        //Call this function from non networked projectiles to change a player's health. This allows to avoid having a PhotonView on every paintball which is very inefficient.
        //We have to call the RPC from this function because RPCs must be called from gameobjects that have a PhotonView component.
        public void HitPlayer(GameObject player, float healthChange)
        {
            photonView.RPC("ChangeHealth", RpcTarget.All, healthChange, player.GetComponent<PhotonView>().ViewID);
        }
        
        /// <summary>
        /// Change the player's health.
        /// </summary>
        [PunRPC]
        public void ChangeHealth(float value, int targetViewID)
        {
            PhotonView.Find(targetViewID).gameObject.GetComponent<PlayerManager>().health += value;
        }
        
        //Function to call when an enemy is hit. 
        // enemy - the enemy we hit
        // healthChange - the effect on the enemies health (negative values for hurting)
        public void HitEnemy(GameObject enemy, float healthChange)
        {
            photonView.RPC("ChangeEnemyHealth", RpcTarget.All, healthChange, enemy.GetComponent<PhotonView>().ViewID);
        }

        [PunRPC]
        public void ChangeEnemyHealth(float value, int targetViewID)
        {
            PhotonView.Find(targetViewID).gameObject.GetComponent<EnemyController>().currentHealth += value;
            PhotonView.Find(targetViewID).gameObject.GetComponent<EnemyController>().OnDamageTaken();
        }

        [PunRPC]
        public void AnimateShoot()
        {
            animator.Play("Shoot");
            animatorHands.Play("Shoot");
          
        }

        private void PlayShootingSound()
        {
            GetComponent<AudioSource>().PlayOneShot(shootingClip);
        }

        public void OnLevelFinished()
        {
            HealingRateDDAA.Instance.AdjustInGameValue();
        }

        #endregion

        #region Private Methods

        //Disables movement
        void Stun()
        {
            fpsController.isStunned = true;
            GetComponentInChildren<ApplyPostProcessing>().vignetteLayer.intensity.value = 1;
        }

        /// <summary>
        /// Processes the inputs. This MUST ONLY BE USED when the player has authority over this Networked GameObject (photonView.isMine == true)
        /// </summary>
        void ProcessInputs()
        {
            if (Input.GetButtonDown("Fire1"))
            {
                // we don't want to fire when we interact with UI buttons for example. IsPointerOverGameObject really means IsPointerOver*UI*GameObject
                // notice we don't use on on GetbuttonUp() few lines down, because one can mouse down, move over a UI element and release, which would lead to not lower the isFiring Flag.
                if (EventSystem.current.IsPointerOverGameObject())
                {
                    //	return;
                }
                this.IsFiring = true;
            }

            if (Input.GetButtonUp("Fire1"))
            {
                if (this.IsFiring)
                {
                    this.IsFiring = false;
                }
            }
        }

        void AnimateWalking()
        {
            if (Input.GetAxis("Vertical") > 0)
            {
                animator.SetBool("isMovingForward", true);
                animator.SetBool("isMovingBackward", false);
                animatorHands.SetBool("isMovingForward", true);
                animatorHands.SetBool("isMovingBackward", false);
            }
            else if (Input.GetAxis("Vertical") < 0)
            {
                animator.SetBool("isMovingForward", false);
                animator.SetBool("isMovingBackward", true);
                animatorHands.SetBool("isMovingForward", false);
                animatorHands.SetBool("isMovingBackward", true);
            }
            else
            {
                animator.SetBool("isMovingForward", false);
                animator.SetBool("isMovingBackward", false);
                animatorHands.SetBool("isMovingForward", false);
                animatorHands.SetBool("isMovingBackward", false);
            }
            if (Input.GetAxis("Horizontal") > 0)
            {
                animator.SetBool("isMovingRight", true);
                animator.SetBool("isMovingLeft", false);
                animatorHands.SetBool("isMovingRight", true);
                animatorHands.SetBool("isMovingLeft", false);
            }
            else if (Input.GetAxis("Horizontal") < 0)
            {
                animator.SetBool("isMovingRight", false);
                animator.SetBool("isMovingLeft", true);
                animatorHands.SetBool("isMovingRight", false);
                animatorHands.SetBool("isMovingLeft", true);
            }
            else
            {
                animator.SetBool("isMovingRight", false);
                animator.SetBool("isMovingLeft", false);
                animatorHands.SetBool("isMovingRight", false);
                animatorHands.SetBool("isMovingLeft", false);
            }
        }

        private IEnumerator ReturnPlayerControl(float waitTime)
        {
            isReturningControl = true;
            fpsController.enabled = true;
            yield return new WaitForSeconds(waitTime);
            fpsController.isStunned = false;
            isReturningControl = false;
        }
        

        private IEnumerator ShootPaintball()
        {
            waitingToShoot = true;
            
            GameObject paintball;
            paintball = Instantiate(paintballPrefab, paintGun.transform.position, Quaternion.identity);
            paintball.GetComponent<PaintBall>().playerWhoShot = this.gameObject;
            paintball.GetComponent<PaintBall>().paintballDamage = this.paintballDamage;
            paintball.GetComponent<PaintBall>().isLocal = photonView.IsMine;
            paintball.GetComponent<Rigidbody>().velocity = paintGun.TransformDirection(Vector3.forward * paintBallSpeed);
            PlayShootingSound();
            yield return new WaitForSeconds(shootWaitTime);
            waitingToShoot = false;
        }

    #endregion

    #region IPunObservable implementation

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
        {
            if (stream.IsWriting)
            {
                // We own this player: send the others our data
                stream.SendNext(this.health);
                stream.SendNext(this.IsFiring);
            }
            else
            {
                // Network player, receive data
                this.health = (float)stream.ReceiveNext();
                this.IsFiring = (bool)stream.ReceiveNext();
            }
        }

        #endregion
    }
}