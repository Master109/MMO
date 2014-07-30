using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Sfs2X;
using Sfs2X.Core;
using Sfs2X.Entities;
using Sfs2X.Entities.Data;
using Sfs2X.Entities.Variables;
using Sfs2X.Requests;
using Sfs2X.Logging;


namespace Sfs2XExamples.ObjectMovement
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
		void Start() {

			if (!Sfs2XExamples.ObjectMovement.SmartFoxConnection.IsInitialized) {
				Application.LoadLevel("ObjectMovementConnection");
				return;
			}
			smartFox = Sfs2XExamples.ObjectMovement.SmartFoxConnection.Connection;
						
			// Register callback delegates
			smartFox.AddEventListener(SFSEvent.OBJECT_MESSAGE, OnObjectMessage);
			smartFox.AddEventListener(SFSEvent.CONNECTION_LOST, OnConnectionLost);
			smartFox.AddEventListener(SFSEvent.USER_VARIABLES_UPDATE, OnUserVariableUpdate);
			smartFox.AddEventListener(SFSEvent.USER_EXIT_ROOM, OnUserExitRoom);
			smartFox.AddEventListener(SFSEvent.USER_ENTER_ROOM, OnUserEnterRoom);

			smartFox.AddLogListener(logLevel, OnDebugMessage);
			
			// Start this clients avatar and get cracking!
			int numModel = UnityEngine.Random.Range(0, playerModels.Length);
			int numMaterial = UnityEngine.Random.Range(0, playerMaterials.Length);
			SpawnLocalPlayer(numModel, numMaterial);
		}
		
		void FixedUpdate() {
			if (smartFox != null) {
				smartFox.ProcessEvents();
				
				// If we spawned a local player, send position if movement is dirty
				if (localPlayer != null && localPlayerController != null && localPlayerController.MovementDirty) {
					List<UserVariable> userVariables = new List<UserVariable>();
					userVariables.Add(new SFSUserVariable("x", (double)localPlayer.transform.position.x));
					userVariables.Add(new SFSUserVariable("y", (double)localPlayer.transform.position.y));
					userVariables.Add(new SFSUserVariable("z", (double)localPlayer.transform.position.z));
					userVariables.Add(new SFSUserVariable("rot", (double)localPlayer.transform.rotation.eulerAngles.y));
					smartFox.Send(new SetUserVariablesRequest(userVariables));
					localPlayerController.MovementDirty = false;
				}
			}
		}
		
		void OnApplicationQuit() {
			// Before leaving, lets notify the others about this client dropping out
			RemoveLocalPlayer();
		}
			
		//----------------------------------------------------------
		// SmartFox callbacks
		//----------------------------------------------------------
		
		public void OnUserExitRoom(BaseEvent evt) {
			// Someone left - lets make certain they are removed if they didnt nicely send a remove command
			SFSUser user = (SFSUser)evt.Params["user"];		
			RemoveRemotePlayer(user);
		}
		
		public void OnUserEnterRoom(BaseEvent evt) {
			// User joined - and we might be standing still (not sending position info). So lets send him our position info
			if (localPlayer != null) {
				List<UserVariable> userVariables = new List<UserVariable>();
				userVariables.Add(new SFSUserVariable("x", (double)localPlayer.transform.position.x));
				userVariables.Add(new SFSUserVariable("y", (double)localPlayer.transform.position.y));
				userVariables.Add(new SFSUserVariable("z", (double)localPlayer.transform.position.z));
				userVariables.Add(new SFSUserVariable("rot", (double)localPlayer.transform.rotation.eulerAngles.y));
				userVariables.Add(new SFSUserVariable("model", smartFox.MySelf.GetVariable("model").GetIntValue()));
				userVariables.Add(new SFSUserVariable("mat", smartFox.MySelf.GetVariable("mat").GetIntValue()));
				smartFox.Send(new SetUserVariablesRequest(userVariables));
			}
		}
		
		public void OnConnectionLost(BaseEvent evt) {
			// Reset all internal states so we kick back to login screen
			smartFox.RemoveAllEventListeners();
			Application.LoadLevel("ObjectMovementConnection");
		}
		
		public void OnObjectMessage(BaseEvent evt) {
			// The only messages being send around are
			// - a remove message from someone that is dropping out
		    ISFSObject dataObj = (SFSObject)evt.Params["message"];
		    SFSUser sender = (SFSUser)evt.Params["sender"];
			
			if (dataObj.ContainsKey("cmd")) {
				switch (dataObj.GetUtfString("cmd")) {
				case "rm":
					Debug.Log("Removing player unit " + sender.Id);
					RemoveRemotePlayer(sender);
					break;
				}
			}
		}
		
		public void OnUserVariableUpdate(BaseEvent evt) {
			// When user variable is updated on any client, then this callback is being received
			// This is where most of the magic happens
			
		    ArrayList changedVars = (ArrayList)evt.Params["changedVars"];
		    SFSUser user = (SFSUser)evt.Params["user"];
			
			if (user == smartFox.MySelf) return;
			
			if (!remotePlayers.ContainsKey(user)) {
				// New client just started transmitting - lets create remote player
				Vector3 pos = new Vector3(0, 1, 0);
				if (user.ContainsVariable("x") && user.ContainsVariable("y") && user.ContainsVariable("z")) {
					pos.x = (float)user.GetVariable("x").GetDoubleValue();
					pos.y = (float)user.GetVariable("y").GetDoubleValue();
					pos.z = (float)user.GetVariable("z").GetDoubleValue();
				}
				float rotAngle = 0;
				if (user.ContainsVariable("rot")) {
					rotAngle = (float)user.GetVariable("rot").GetDoubleValue();
				}
				int numModel = 0;
				if (user.ContainsVariable("model")) {
					numModel = user.GetVariable("model").GetIntValue();
				}
				int numMaterial = 0;
				if (user.ContainsVariable("mat")) {
					numMaterial = user.GetVariable("mat").GetIntValue();
				}
				SpawnRemotePlayer(user, numModel, numMaterial, pos, Quaternion.Euler(0, rotAngle, 0));
			}
			
		    // Check if the remote user changed his position or rotation
	    	if (changedVars.Contains("x") && changedVars.Contains("y") && changedVars.Contains("z") && changedVars.Contains("rot")) {
	        	// Move the character to a new position...
				remotePlayers[user].GetComponent<SimpleRemoteInterpolation>().SetTransform(
					new Vector3((float)user.GetVariable("x").GetDoubleValue(), (float)user.GetVariable("y").GetDoubleValue(), (float)user.GetVariable("z").GetDoubleValue()),
					Quaternion.Euler(0, (float)user.GetVariable("rot").GetDoubleValue(), 0),
					true);
	    	}
			// Remote client got new name?
			if (changedVars.Contains("name")) {
				remotePlayers[user].GetComponentInChildren<TextMesh>().text = user.Name;
			}
			// Remote client selected new model?
			if (changedVars.Contains("model")) {
				SpawnRemotePlayer(user, user.GetVariable("model").GetIntValue(), user.GetVariable("mat").GetIntValue(), remotePlayers[user].transform.position, remotePlayers[user].transform.rotation);
			}
			// Remote client selected new material?
			if (changedVars.Contains("mat")) {
				remotePlayers[user].GetComponentInChildren<Renderer>().material = playerMaterials[ user.GetVariable("mat").GetIntValue() ];
			}
		}
		
		public void OnDebugMessage(BaseEvent evt) {
			string message = (string)evt.Params["message"];
			Debug.Log("[SFS DEBUG] " + message);
		}


		//----------------------------------------------------------
		// Public interface methods for GUI
		//----------------------------------------------------------
		
		public void ChangePlayerMaterial(int numMaterial) {
			localPlayer.GetComponentInChildren<Renderer>().material = playerMaterials[numMaterial];

			List<UserVariable> userVariables = new List<UserVariable>();
			userVariables.Add(new SFSUserVariable("mat", numMaterial));
			smartFox.Send(new SetUserVariablesRequest(userVariables));
		}
		
		public void ChangePlayerModel(int numModel) {
			SpawnLocalPlayer(numModel, smartFox.MySelf.GetVariable("mat").GetIntValue() );
		}
		
		//----------------------------------------------------------
		// Private player helper methods
		//----------------------------------------------------------
		
		private void SpawnLocalPlayer(int numModel, int numMaterial) {
			Vector3 pos;
			Quaternion rot;
			
			// See if there already exists a model - if so, take its pos+rot before destroying it
			if (localPlayer != null) {
				pos = localPlayer.transform.position;
				rot = localPlayer.transform.rotation;
				Camera.main.transform.parent = null;
				Destroy(localPlayer);
			} else {
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
			
			// Lets set the model and material choice and tell the others about it
			List<UserVariable> userVariables = new List<UserVariable>();
			userVariables.Add(new SFSUserVariable("model", numModel));
			userVariables.Add(new SFSUserVariable("mat", numMaterial));
			smartFox.Send(new SetUserVariablesRequest(userVariables));
		}
		
		private void SpawnRemotePlayer(SFSUser user, int numModel, int numMaterial, Vector3 pos, Quaternion rot) {
			// See if there already exists a model so we can destroy it first
			if (remotePlayers.ContainsKey(user) && remotePlayers[user] != null) {
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
		
		private void RemoveLocalPlayer() {
			// Someone dropped off the grid. Lets remove him
			SFSObject obj = new SFSObject();
			obj.PutUtfString("cmd", "rm");
			smartFox.Send(new ObjectMessageRequest(obj, smartFox.LastJoinedRoom));
		}
		
		private void RemoveRemotePlayer(SFSUser user) {
			if (user == smartFox.MySelf) return;
			
			if (remotePlayers.ContainsKey(user)) {
				Destroy(remotePlayers[user]);
				remotePlayers.Remove(user);
			}
		}	
	}
}