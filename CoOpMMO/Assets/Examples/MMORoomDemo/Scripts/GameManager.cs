using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Sfs2X;
using Sfs2X.Core;
using Sfs2X.Entities;
using Sfs2X.Entities.Data;
using Sfs2X.Entities.Variables;
using Sfs2X.Requests;
using Sfs2X.Requests.MMO;
using Sfs2X.Logging;


namespace Sfs2XExamples.MMORoom
{
	public class GameManager : MonoBehaviour {
		
		//----------------------------------------------------------
		// Setup variables
		//----------------------------------------------------------
		public GameObject[] playerModels;
		public Material[] playerMaterials;
		public LogLevel logLevel = LogLevel.DEBUG;

		// Internal / private variables
		private SmartFox smartFox;
		
		private GameObject localPlayer;
		private PlayerController localPlayerController;
		private Dictionary<SFSUser, GameObject> remotePlayers = new Dictionary<SFSUser, GameObject>();
		
		//----------------------------------------------------------
		// Unity callbacks
		//----------------------------------------------------------
		void Start() 
		{
			if (!SmartFoxConnection.IsInitialized) 
			{
				Application.LoadLevel("MMORoomConnection");
				return;
			}
			smartFox = SmartFoxConnection.Connection;
			
			smartFox.AddEventListener(SFSEvent.CONNECTION_LOST, OnConnectionLost);
			smartFox.AddEventListener(SFSEvent.USER_VARIABLES_UPDATE, OnUserVariableUpdate);
			smartFox.AddEventListener(SFSEvent.PROXIMITY_LIST_UPDATE, OnProximityListUpdate);
				
			smartFox.AddLogListener(logLevel, OnDebugMessage);
			
			// Start this clients avatar and get cracking!
			int numModel = UnityEngine.Random.Range(0, playerModels.Length);
			int numMaterial = UnityEngine.Random.Range(0, playerMaterials.Length);
			SpawnLocalPlayer(numModel, numMaterial);
		}
		
		void FixedUpdate() 
		{
			if (smartFox != null) 
			{
				smartFox.ProcessEvents();
				
				/*
				 * If we spawned a local player, send position if movement is dirty
				 * 
				 * NOTE: We have commented the UserVariable relative to the Y Axis because in this example
				 * the Y position is fixed (Y = 1.0) In case your game allows moving on all axis we should transmit all positions.
				 * 
				 * On the server side the UserVariable event is captured and the coordinates are also passed to the 
				 * MMOApi.SetUserPosition(...) method to update our position in the Room's map. This in turn will keep us in synch
				 * with all the other players within our Area Of Interest (AoI)
				 */
				if (localPlayer != null && localPlayerController != null && localPlayerController.MovementDirty) 
				{
					List<UserVariable> userVariables = new List<UserVariable>();
					userVariables.Add(new SFSUserVariable("x", (double)localPlayer.transform.position.x));
					//userVariables.Add(new SFSUserVariable("y", (double)localPlayer.transform.position.y));
					userVariables.Add(new SFSUserVariable("z", (double)localPlayer.transform.position.z));
					userVariables.Add(new SFSUserVariable("rot", (double)localPlayer.transform.rotation.eulerAngles.y));
					smartFox.Send(new SetUserVariablesRequest(userVariables));
					localPlayerController.MovementDirty = false;
				}
			}
		}
		
		void OnApplicationQuit() 
		{
			Debug.Log("Quitting...");
		}
			
		//----------------------------------------------------------
		// SmartFox callbacks
		//----------------------------------------------------------
		
		/**
		 * This is where we receive events about people within our proximity (AoI)
		 * We get two lists, one of new Users that have entered the AoI and one with Users that have left our proximity
		 */
		public void OnProximityListUpdate(BaseEvent evt)
		{
			var addedUsers = (List<User>) evt.Params["addedUsers"];
			var removedUsers = (List<User>) evt.Params["removedUsers"];
			
			// Handle all new Users
			foreach (User user in addedUsers)
			{
				SpawnRemotePlayer
				(
					(SFSUser) user, 
					user.GetVariable("model").GetIntValue(), 
					user.GetVariable("mat").GetIntValue(), 
					new Vector3(user.AOIEntryPoint.FloatX, user.AOIEntryPoint.FloatY, user.AOIEntryPoint.FloatZ),
					Quaternion.Euler(0, (float) user.GetVariable("rot").GetDoubleValue() , 0)
				);		
			}
			
			// Handle removed users
			foreach (User user in removedUsers)
			{
				RemoveRemotePlayer((SFSUser) user);
			}
		}
		
		public void OnConnectionLost(BaseEvent evt) 
		{
			// Reset all internal states so we kick back to login screen
			smartFox.RemoveAllEventListeners();
			Application.LoadLevel("MMORoomConnection");
		}
		
		public void OnUserVariableUpdate(BaseEvent evt) 
		{
			// When user variable is updated on any client, then this callback is being received
			// This is where most of the magic happens
			
		    ArrayList changedVars = (ArrayList)evt.Params["changedVars"];
		    SFSUser user = (SFSUser)evt.Params["user"];
			
			if (user == smartFox.MySelf) return;
			
			
		    // Check if the remote user changed his position or rotation
	    	if (changedVars.Contains("x") || changedVars.Contains("y") || changedVars.Contains("z") || changedVars.Contains("rot")) 
			{
	        	// Move the character to a new position...
				remotePlayers[user].GetComponent<SimpleRemoteInterpolation>().SetTransform(
					new Vector3((float)user.GetVariable("x").GetDoubleValue(), 1, (float)user.GetVariable("z").GetDoubleValue()),
					Quaternion.Euler(0, (float)user.GetVariable("rot").GetDoubleValue(), 0),
					true);
	    	}
			
			// Remote client got new name?
			if (changedVars.Contains("name")) 
			{
				remotePlayers[user].GetComponentInChildren<TextMesh>().text = user.Name;
			}
			
			// Remote client selected new model?
			if (changedVars.Contains("model")) 
			{
				SpawnRemotePlayer(user, user.GetVariable("model").GetIntValue(), user.GetVariable("mat").GetIntValue(), remotePlayers[user].transform.position, remotePlayers[user].transform.rotation);
			}
			
			// Remote client selected new material?
			if (changedVars.Contains("mat")) 
			{
				remotePlayers[user].GetComponentInChildren<Renderer>().material = playerMaterials[ user.GetVariable("mat").GetIntValue() ];
			}
		}
		
		public void OnDebugMessage(BaseEvent evt) 
		{
			string message = (string)evt.Params["message"];
			Debug.Log("[SFS DEBUG] " + message);
		}


		//----------------------------------------------------------
		// Public interface methods for GUI
		//----------------------------------------------------------
		
		public void ChangePlayerMaterial(int numMaterial) 
		{
			localPlayer.GetComponentInChildren<Renderer>().material = playerMaterials[numMaterial];

			List<UserVariable> userVariables = new List<UserVariable>();
			userVariables.Add(new SFSUserVariable("mat", numMaterial));
			smartFox.Send(new SetUserVariablesRequest(userVariables));
		}
		
		public void ChangePlayerModel(int numModel) 
		{
			SpawnLocalPlayer(numModel, smartFox.MySelf.GetVariable("mat").GetIntValue() );
		}
		
		//----------------------------------------------------------
		// Private player helper methods
		//----------------------------------------------------------
		
		private void SpawnLocalPlayer(int numModel, int numMaterial) 
		{
			Vector3 pos;
			Quaternion rot;
			
			// See if there already exists a model - if so, take its pos+rot before destroying it
			if (localPlayer != null) 
			{
				pos = localPlayer.transform.position;
				rot = localPlayer.transform.rotation;
				Camera.main.transform.parent = null;
				Destroy(localPlayer);
			} 
			
			else 
			{
				pos = new Vector3(0, 1, 0);
				rot = Quaternion.identity;
			}
			
			// Lets spawn our local player model
			localPlayer = GameObject.Instantiate(playerModels[numModel]) as GameObject;
			localPlayer.transform.position = pos;
			localPlayer.transform.rotation = rot;
			
			// Assign starting material
			localPlayer.GetComponentInChildren<Renderer>().material = playerMaterials[numMaterial];

			// Since this is the local player, lets add a controller and fix the camera
			localPlayer.AddComponent<PlayerController>();
			localPlayerController = localPlayer.GetComponent<PlayerController>();
			localPlayer.GetComponentInChildren<TextMesh>().text = smartFox.MySelf.Name;
			Camera.main.transform.parent = localPlayer.transform;
			
			/*
			 * Lets set the model and material choice and tell the others about it
			 * 
		     * NOTE: We have commented the UserVariable relative to the Y Axis because in this example
			 * the Y position is fixed (Y = 1.0) In case your game allows moving on all axis we should transmit all positions
			 */
			List<UserVariable> userVariables = new List<UserVariable>();
			
			userVariables.Add(new SFSUserVariable("x", (double)localPlayer.transform.position.x));
			//userVariables.Add(new SFSUserVariable("y", (double)localPlayer.transform.position.y));
			userVariables.Add(new SFSUserVariable("z", (double)localPlayer.transform.position.z));
			userVariables.Add(new SFSUserVariable("rot", (double)localPlayer.transform.rotation.eulerAngles.y));
			userVariables.Add(new SFSUserVariable("model", numModel));
			userVariables.Add(new SFSUserVariable("mat", numMaterial));
			
			// Send request
			smartFox.Send(new SetUserVariablesRequest(userVariables));
			
			Debug.LogWarning("Position Updated");
		}
		
		private void SpawnRemotePlayer(SFSUser user, int numModel, int numMaterial, Vector3 pos, Quaternion rot) 
		{
			// See if there already exists a model so we can destroy it first
			if (remotePlayers.ContainsKey(user) && remotePlayers[user] != null) 
			{
				Destroy(remotePlayers[user]);
				remotePlayers.Remove(user);
			}
			
			// Lets spawn our remote player model
			GameObject remotePlayer = GameObject.Instantiate(playerModels[numModel]) as GameObject;
			remotePlayer.AddComponent<SimpleRemoteInterpolation>();
			remotePlayer.GetComponent<SimpleRemoteInterpolation>().SetTransform(pos, rot, false);
			
			// Color and name
			remotePlayer.GetComponentInChildren<TextMesh>().text = user.Name;
			remotePlayer.GetComponentInChildren<Renderer>().material = playerMaterials[numMaterial];

			// Lets track the dude
			remotePlayers.Add(user, remotePlayer);
		}
		
		private void RemoveRemotePlayer(SFSUser user) 
		{
			if (user == smartFox.MySelf) return;
			
			if (remotePlayers.ContainsKey(user)) 
			{
				Destroy(remotePlayers[user]);
				remotePlayers.Remove(user);
			}
		}		
	}
}