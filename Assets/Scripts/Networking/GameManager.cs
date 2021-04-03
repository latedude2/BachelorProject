﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Launcher.cs" company="Exit Games GmbH">
//   Part of: Photon Unity Networking Demos
// </copyright>
// <summary>
//  Used in "PUN Basic tutorial" to handle typical game management requirements
// </summary>
// <author>developer@exitgames.com</author>
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.SceneManagement;
using ExitGames.Client.Photon;
using Photon.Realtime;
using Photon.Pun;
using System.Collections;
using System.Linq;

namespace Photon.Pun.Demo.PunBasics
{
	#pragma warning disable 649

	/// <summary>
	/// Game manager.
	/// Connects and watch Photon Status, Instantiate Player
	/// Deals with quiting the room and the game
	/// Deals with level loading (outside the in room synchronization)
	/// </summary>
	public class GameManager : MonoBehaviourPunCallbacks
    {

		#region Public Fields

		static public GameManager Instance;

		#endregion

		#region Private Fields

		public const byte respawnEvent = 1;
		public const byte destroyKeyLocationEvent = 2;

		private GameObject instance;


		[Tooltip("Check to change the condition")]
		[SerializeField]
		private bool IsDDAEnabled = true;

		[Tooltip("Check if we should receive condition from the server")]
		[SerializeField]
		private bool isLiveTest = true;
		[Tooltip("Period at which the time based DDAAs are triggered")]
		[SerializeField]
		private float timeBasedDDAAPeriod;

		[Tooltip("The prefab to use for representing the player")]
        [SerializeField]
        private GameObject playerPrefab;

		bool isRespawning = false;
		float respawnCheckTime = 1f;

		#endregion

		#region MonoBehaviour CallBacks

		private void Awake()
		{
			GameObject conditionSetter = GameObject.Find("ConditionSetter");
			if (isLiveTest && conditionSetter != null)
            {
				IsDDAEnabled = conditionSetter.GetComponent<ConditionSetter>().IsDDACondition();
				Debug.LogError("condition string: " + conditionSetter.GetComponent<ConditionSetter>().condition);
			}
			if (PhotonNetwork.IsMasterClient)
				photonView.RPC(nameof(SetCondition), RpcTarget.All, IsDDAEnabled);
		}

		/// <summary>
		/// MonoBehaviour method called on GameObject by Unity during initialization phase.
		/// </summary>
		void Start()
		{
			//Ignore the collisions between layer 8 (Enemy) and layer 9 (Enemy projectile)
			Physics.IgnoreLayerCollision(8, 9);
			Physics.IgnoreLayerCollision(9, 9);
			Instance = this;

			// in case we started this demo with the wrong scene being active, simply load the menu scene
			if (!PhotonNetwork.IsConnected)
			{
				SceneManager.LoadScene("Launcher");

				return;
			}

			if (playerPrefab == null) { // #Tip Never assume public properties of Components are filled up properly, always check and inform the developer of it.

				Debug.LogError("<Color=Red><b>Missing</b></Color> playerPrefab Reference. Please set it up in GameObject 'Game Manager'", this);
			} else {


				if (PlayerManager.LocalPlayerInstance==null)
				{
				    //Debug.LogFormat("We are Instantiating LocalPlayer from {0}", SceneManagerHelper.ActiveSceneName);


					// we're in a room. spawn a character for the local player. it gets synced by using PhotonNetwork.Instantiate
					GameObject player = PhotonNetwork.Instantiate(this.playerPrefab.name, new Vector3(0f,5f,0f), Quaternion.identity, 0);
					player.transform.GetComponent<PlayerManager>().gameManager = this;	//We give the player a reference to the game manager
				}
				else{

					Debug.LogFormat("Ignoring scene load for {0}", SceneManagerHelper.ActiveSceneName);
				}
			}

			InvokeRepeating(nameof(TriggerTimeBasedDDAAs), timeBasedDDAAPeriod, timeBasedDDAAPeriod);
		}

		/// <summary>
		/// MonoBehaviour method called on GameObject by Unity on every frame.
		/// </summary>
		void Update()
		{
			// "back" button of phone equals "Escape". quit app if that's pressed
			if (Input.GetKeyDown(KeyCode.Escape))
			{
				QuitApplication();
			}
			if(PhotonNetwork.IsMasterClient && !isRespawning)
            {
				StartCoroutine(RespawnCheck());
			}
		}

        #endregion

        #region Photon Callbacks

        /// <summary>
        /// Called when a Photon Player got connected. We need to then load a bigger scene.
        /// </summary>
        /// <param name="other">Other.</param>
        public override void OnPlayerEnteredRoom( Player other  )
		{
			Debug.Log( "OnPlayerEnteredRoom() " + other.NickName); // not seen if you're the player connecting

			if ( PhotonNetwork.IsMasterClient )
			{
				Debug.LogFormat( "OnPlayerEnteredRoom IsMasterClient {0}", PhotonNetwork.IsMasterClient ); // called before OnPlayerLeftRoom

				LoadArena();
			}
		}

		/// <summary>
		/// Called when a Photon Player got disconnected. We need to load a smaller scene.
		/// </summary>
		/// <param name="other">Other.</param>
		public override void OnPlayerLeftRoom( Player other  )
		{
			Debug.Log( "OnPlayerLeftRoom() " + other.NickName ); // seen when other disconnects

			if ( PhotonNetwork.IsMasterClient )
			{
				Debug.LogFormat( "OnPlayerEnteredRoom IsMasterClient {0}", PhotonNetwork.IsMasterClient ); // called before OnPlayerLeftRoom

				LoadArena(); 
			}
		}

		[PunRPC]
		public void SetCondition(bool IsDDAEnabled)
        {
			this.IsDDAEnabled = IsDDAEnabled;
			DDAEngine.isDynamicAdjustmentEnabled = IsDDAEnabled;
			Debug.LogError("Condition set by master client - " + IsDDAEnabled);
		}

		/// <summary>
		/// Called when the local player left the room. We need to load the launcher scene.
		/// </summary>
		public override void OnLeftRoom()
		{
			SceneManager.LoadScene("Launcher");
		}

		#endregion

		#region Public Methods

		public void LeaveRoom()
		{
			PhotonNetwork.LeaveRoom();
		}

		public void QuitApplication()
		{
			Application.Quit();
		}

		#endregion

		#region Private Methods

		IEnumerator RespawnCheck()
        {
			isRespawning = true;
			GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
			//Check if any player is still alive
			foreach(GameObject player in players)
            {
				if (player.GetComponent<PlayerManager>().health > 0)
				{
					isRespawning = false;
					yield break;
				}
			}
			RaiseEventOptions raiseEventOptions = new RaiseEventOptions { Receivers = ReceiverGroup.All }; // You would have to set the Receivers to All in order to receive this event on the local client as well
			PhotonNetwork.RaiseEvent(respawnEvent,0, raiseEventOptions, SendOptions.SendReliable);
			yield return new WaitForSeconds(respawnCheckTime);
			isRespawning = false;
		}

		void LoadArena()
		{
			
			if ( ! PhotonNetwork.IsMasterClient )
			{
				Debug.LogError( "PhotonNetwork : Trying to Load a level but we are not the master Client" );
			}

			Debug.LogFormat( "PhotonNetwork : Loading Level : {0}", PhotonNetwork.CurrentRoom.PlayerCount );

			PhotonNetwork.LoadLevel("Master");
		}

        #endregion

        #region DDA System methods

		private void TriggerTimeBasedDDAAs()
		{
			Debug.Log("Time based DDAAs triggered. System time: " + Time.timeSinceLevelLoad);
			StunCondition.Instance.UpdateConditionalValue(GameObject.FindGameObjectsWithTag("Player").ToList());

			//TODO: Implement updating DDAAs here, which are time-based
		}

        #endregion

    }

}