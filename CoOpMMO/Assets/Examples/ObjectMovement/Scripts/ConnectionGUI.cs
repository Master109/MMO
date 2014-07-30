using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using Sfs2X;
using Sfs2X.Core;
using Sfs2X.Entities;
using Sfs2X.Requests;
using Sfs2X.Logging;


namespace Sfs2XExamples.ObjectMovement
{
	public class ConnectionGUI : MonoBehaviour {
		
		//----------------------------------------------------------
		// Setup variables
		//----------------------------------------------------------
		public string serverName = "127.0.0.1";
		public int serverPort = 9933;
		public string zone = "BasicExamples";
		public GUISkin sfsSkin;
		public LogLevel logLevel = LogLevel.DEBUG;

		// Internal / private variables
		private SmartFox smartFox;
		private string username = "";
		private string loginErrorMessage = "";
		private string serverConnectionStatusMessage = "";
		private bool isJoining = false;
		
		//----------------------------------------------------------
		// Called when program starts
		//----------------------------------------------------------
		void Start() {
			serverName = Settings.ipAddress;
			serverPort = Int32.Parse(Settings.port);

			// In a webplayer (or editor in webplayer mode) we need to setup security policy negotiation with the server first
			if (Application.isWebPlayer || Application.isEditor) {
				if (!Security.PrefetchSocketPolicy(serverName, serverPort, 5000)) {
					Debug.LogError("Security Exception. Policy file load failed!");
				}
			}	
			
			// Lets connect
			smartFox = new SmartFox(true);
						
			// Register callback delegate
			smartFox.AddEventListener(SFSEvent.CONNECTION, OnConnection);
			smartFox.AddEventListener(SFSEvent.CONNECTION_LOST, OnConnectionLost);
			smartFox.AddEventListener(SFSEvent.LOGIN, OnLogin);
			smartFox.AddEventListener(SFSEvent.LOGIN_ERROR, OnLoginError);
			smartFox.AddEventListener(SFSEvent.ROOM_JOIN, OnRoomJoin);
			smartFox.AddEventListener(SFSEvent.LOGOUT, OnLogout);

			smartFox.AddLogListener(logLevel, OnDebugMessage);
			
			smartFox.Connect(serverName, serverPort);
		}
		
		//----------------------------------------------------------
		// As Unity is not thread safe, we process the queued up callbacks every physics tick
		//----------------------------------------------------------
		void FixedUpdate() {
			if (smartFox != null) {
				smartFox.ProcessEvents();
			}
		}

		//----------------------------------------------------------
		// Handle connection response from server
		//----------------------------------------------------------
		public void OnConnection(BaseEvent evt) {
			bool success = (bool)evt.Params["success"];
			string error = (string)evt.Params["errorMessage"];
			
			Debug.Log("On Connection callback got: " + success + " (error : <" + error + ">)");

			if (success) {
				Sfs2XExamples.ObjectMovement.SmartFoxConnection.Connection = smartFox;
				
				serverConnectionStatusMessage = "Connection succesful!";
			} else {
				serverConnectionStatusMessage = "Can't connect to server!";
			}
		}


		public void OnConnectionLost(BaseEvent evt) {
			// Reset all internal states so we kick back to login screen
			Debug.Log("OnConnectionLost");
			isJoining = false;
			
			serverConnectionStatusMessage = "Connection was lost, Reason: " + (string)evt.Params["reason"];
		}

		public void OnLogin(BaseEvent evt) {
			Debug.Log("Logged in successfully");
			
			// We either create the Game Room or join it if it exists already
			if (smartFox.RoomManager.ContainsRoom("Game Room")) {
				smartFox.Send(new JoinRoomRequest("Game Room"));
				
			} else {
				RoomSettings settings = new RoomSettings("Game Room");
				settings.MaxUsers = 40;
			 
				smartFox.Send(new CreateRoomRequest(settings, true));
			}
		}

		public void OnLoginError(BaseEvent evt) {
			Debug.Log("Login error: "+(string)evt.Params["errorMessage"]);
		}
		
		public void OnRoomJoin(BaseEvent evt) {
			Debug.Log("Joined room successfully");

			// Room was joined - lets load the game and remove all the listeners from this component
			smartFox.RemoveAllEventListeners();
			Application.LoadLevel("ObjectMovementGame");
		}
		
		void OnLogout(BaseEvent evt) {
			Debug.Log("OnLogout");
			isJoining = false;
		}
		
		public void OnDebugMessage(BaseEvent evt) {
			string message = (string)evt.Params["message"];
			Debug.Log("[SFS DEBUG] " + message);
		}

		//----------------------------------------------------------
		// Unity engine callbacks
		//----------------------------------------------------------

		void OnGUI() {
			if (smartFox == null) return;
			GUI.skin = sfsSkin;
		
			// Determine which state we are in and show the GUI accordingly
			if (!smartFox.IsConnected) {
				DrawMessagePanelGUI("Not connected");
			}
			else if (isJoining) {
				DrawMessagePanelGUI("Joining.....");
			}
			else {
				DrawLoginGUI();
			}
		}


		// Generic single message panel
		void DrawMessagePanelGUI(string message) {
			// Lets just quickly set up some GUI layout variables
			float panelWidth = 400;
			float panelHeight = 300;
			float panelPosX = Screen.width/2 - panelWidth/2;
			float panelPosY = Screen.height/2 - panelHeight/2;
			
			// Draw the box
			GUILayout.BeginArea(new Rect(panelPosX, panelPosY, panelWidth, panelHeight));
			GUILayout.Box ("Object Movement Example", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
			GUILayout.BeginVertical();
			GUILayout.BeginArea(new Rect(20, 25, panelWidth-40, panelHeight-60), GUI.skin.customStyles[0]);
			
			// Center label
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUILayout.BeginVertical();
			GUILayout.FlexibleSpace();
				
			GUILayout.Label(message);
				
			GUILayout.FlexibleSpace();
			GUILayout.EndVertical();
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			
			GUILayout.EndArea ();		
			
			GUILayout.BeginArea(new Rect(20, panelHeight-30, panelWidth-40, 80));
			// Display client status
			GUIStyle centeredLabelStyle = new GUIStyle(GUI.skin.label);
			centeredLabelStyle.alignment = TextAnchor.MiddleCenter;
			
			GUILayout.Label("Client Status: " + serverConnectionStatusMessage, centeredLabelStyle);
			
			GUILayout.EndArea ();		
			GUILayout.EndVertical();
			GUILayout.EndArea ();		
		}
		
		// Login GUI allowing for username, password and zone selection
		private void DrawLoginGUI() {
			// Lets just quickly set up some GUI layout variables
			float panelWidth = 400;
			float panelHeight = 300;
			float panelPosX = Screen.width/2 - panelWidth/2;
			float panelPosY = Screen.height/2 - panelHeight/2;
			
			// Draw the box
			GUILayout.BeginArea(new Rect(panelPosX, panelPosY, panelWidth, panelHeight));
			GUILayout.Box ("Object Movement Login", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
			GUILayout.BeginVertical();
			GUILayout.BeginArea(new Rect(20, 25, panelWidth-40, panelHeight-60), GUI.skin.customStyles[0]);
			
			// Lets show login box!
			GUILayout.FlexibleSpace();
				
			GUILayout.BeginHorizontal();
			GUILayout.Label("Username: ");
			username = GUILayout.TextField(username, 25, GUILayout.MinWidth(200));
			GUILayout.EndHorizontal();

			GUILayout.Label(loginErrorMessage);
				
			// Center login button
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();		
			if (GUILayout.Button("Login")  || (Event.current.type == EventType.keyDown && Event.current.character == '\n')) {
				Debug.Log("Sending login request");
				smartFox.Send(new LoginRequest(username, "", zone));
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.FlexibleSpace();
			
			GUILayout.EndArea ();		
			
			GUILayout.BeginArea(new Rect(20, panelHeight-30, panelWidth-40, 80));
			// Display client status
			GUIStyle centeredLabelStyle = new GUIStyle(GUI.skin.label);
			centeredLabelStyle.alignment = TextAnchor.MiddleCenter;
			
			GUILayout.Label("Client Status: " + serverConnectionStatusMessage, centeredLabelStyle);
			
			GUILayout.EndArea ();		
			GUILayout.EndVertical();
			GUILayout.EndArea ();		
		}
	}
}