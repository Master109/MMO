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


namespace Sfs2XExamples.Lobby
{
	public class LobbyGUI : MonoBehaviour {
		
		//----------------------------------------------------------
		// Setup variables
		//----------------------------------------------------------
		public string serverName = "127.0.0.1";	// Use Unity Inspector to change this value
		public int serverPort = 9933;			// Use Unity Inspector to change this value
		public GUISkin sfsSkin;

		// Internal / private variables
		private SmartFox smartFox;
		private string zone = "BasicExamples";
		private string username = "";
		private string password = "";
		private string loginErrorMessage = "";
		private string serverConnectionStatusMessage = "";
		private bool isLoggedIn;
		private bool isJoining = false;
		
		private string newMessage = "";
		private ArrayList messages = new ArrayList();
		// Locker to use for messages collection to ensure its cross-thread safety
		private System.Object messagesLocker = new System.Object();
		
		private Vector2 chatScrollPosition, roomScrollPosition, userScrollPosition;

		private int roomSelection = 0;
		private string [] roomStrings;
		
		//----------------------------------------------------------
		// Called when program starts
		//----------------------------------------------------------
		void Start() {
			serverName = Settings.ipAddress;
			serverPort = Int32.Parse(Settings.port);

			// In a webplayer (or editor in webplayer mode) we need to setup security policy negotiation with the server first
			if (Application.isWebPlayer || Application.isEditor) {
				if (!Security.PrefetchSocketPolicy(serverName, serverPort, 5000)) {
					Debug.LogError("Security Exception. Policy file loading failed!");
				}
			}	
			
			// Lets connect
			smartFox = new SmartFox(true);
						
			// Register callback delegate
			smartFox.AddEventListener(SFSEvent.CONNECTION, OnConnection);
			smartFox.AddEventListener(SFSEvent.CONNECTION_LOST, OnConnectionLost);
			smartFox.AddEventListener(SFSEvent.LOGIN, OnLogin);
			smartFox.AddEventListener(SFSEvent.LOGIN_ERROR, OnLoginError);
			smartFox.AddEventListener(SFSEvent.LOGOUT, OnLogout);
			smartFox.AddEventListener(SFSEvent.ROOM_JOIN, OnJoinRoom);
			smartFox.AddEventListener(SFSEvent.PUBLIC_MESSAGE, OnPublicMessage);

			smartFox.AddLogListener(LogLevel.DEBUG, OnDebugMessage);
			
			smartFox.Connect(serverName, serverPort);
		}
		
		//----------------------------------------------------------
		// As Unity is not thread safe, we process the queued up callbacks every physics tick
		//----------------------------------------------------------
		void FixedUpdate() {
			smartFox.ProcessEvents();
		}

		//----------------------------------------------------------
		// Handle connection response from server
		//----------------------------------------------------------
		public void OnConnection(BaseEvent evt) {
			bool success = (bool)evt.Params["success"];
			string error = (string)evt.Params["errorMessage"];
			
			Debug.Log("On Connection callback got: " + success + " (error : <" + error + ">)");

			if (success) {
				serverConnectionStatusMessage = "Connection successful!";
			} else {
				serverConnectionStatusMessage = "Can't connect to server!";
			}
		}


		public void OnConnectionLost(BaseEvent evt) {
			// Reset all internal states so we kick back to login screen
			Debug.Log("OnConnectionLost");
			isLoggedIn = false;
			isJoining = false;
			
			serverConnectionStatusMessage = "Connection lost; reason: " + (string)evt.Params["reason"];
		}

		public void OnLogin(BaseEvent evt) {
			// Make sure we got in and then populate the room list string array
			isLoggedIn = true;
			Debug.Log("Logged in successfully");
			ReadRoomListAndJoin();
		}

		public void OnLoginError(BaseEvent evt) {
			Debug.Log("Login error: "+(string)evt.Params["errorMessage"]);
		}
		
		void OnLogout(BaseEvent evt) {
			Debug.Log("OnLogout");
			isLoggedIn = false;
			isJoining = false;
		}
		
		void OnJoinRoom(BaseEvent evt) {
			Room room = (Room)evt.Params["room"];
			Debug.Log("Room " + room.Name + " joined successfully");
			
			lock (messagesLocker) {
				messages.Clear();
			}
			isJoining = false;
		}

		void OnPublicMessage(BaseEvent evt) {
			string message = (string)evt.Params["message"];
			User sender = (User)evt.Params["sender"];
		
			// We use lock here to ensure cross-thread safety on the messages collection 
			lock (messagesLocker) {
				messages.Add(sender.Name + " said: " + message);
			}
				
			chatScrollPosition.y = Mathf.Infinity;
			Debug.Log("User " + sender.Name + " said: " + message);
		}


		public void OnDebugMessage(BaseEvent evt) {
			string message = (string)evt.Params["message"];
			Debug.Log("[SFS DEBUG] " + message);
		}
		
		
		//----------------------------------------------------------
		// Private helper methods
		//----------------------------------------------------------
		
		private void ReadRoomListAndJoin() {
			// We use the list of rooms to make a selection grid as part of the GUI. So lets read the room list and put it into a string[]
			Debug.Log("Room list: ");
							
			List<Room> roomList = smartFox.RoomManager.GetRoomList();
			List<string> roomNames = new List<string>();
			foreach (Room room in roomList) {
				if (room.IsHidden || room.IsPasswordProtected) {
					continue;
				}	
				
				roomNames.Add(room.Name);
				Debug.Log("Room id: " + room.Id + " has name: " + room.Name);
					
			}
			
			roomStrings = roomNames.ToArray();
			
			if (smartFox.LastJoinedRoom==null && smartFox.RoomList.Count > 0) {
				JoinRoom(smartFox.RoomList[0].Name);
			}
		}

		void JoinRoom(string roomName) {
			if (isJoining) return;
			
			isJoining = true;
			Debug.Log("Joining room: "+roomName);
			
			// Need to leave current room, if we are joined one
			if (smartFox.LastJoinedRoom==null)
				smartFox.Send(new JoinRoomRequest(roomName));
			else	
				smartFox.Send(new JoinRoomRequest(roomName, "", smartFox.LastJoinedRoom.Id));
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
			else if (!isLoggedIn) {
				DrawLoginGUI();
			}
			else if (isJoining) {
				DrawMessagePanelGUI("Joining...");
			}
			else if (smartFox.LastJoinedRoom != null) {
				DrawLobbyGUI();
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
			GUILayout.Box ("Lobby", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
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
			
			GUILayout.Label("Client status: " + serverConnectionStatusMessage, centeredLabelStyle);
			
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
			GUILayout.Box ("Lobby", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
			GUILayout.BeginVertical();
			GUILayout.BeginArea(new Rect(20, 25, panelWidth-40, panelHeight-60), GUI.skin.customStyles[0]);
			
			// Lets show login box!
			GUILayout.FlexibleSpace();
				
			GUILayout.BeginHorizontal();
			GUILayout.Label("Zone: ");
			zone = GUILayout.TextField(zone, 25, GUILayout.MinWidth(200));
			GUILayout.EndHorizontal();
				
			GUILayout.BeginHorizontal();
			GUILayout.Label("Username: ");
			username = GUILayout.TextField(username, 25, GUILayout.MinWidth(200));
			GUILayout.EndHorizontal();

			GUILayout.BeginHorizontal();
			GUILayout.Label("Password: ");
			password = GUILayout.TextField(password, 25, GUILayout.MinWidth(200));
			GUILayout.EndHorizontal();

			GUILayout.Label(loginErrorMessage);
				
			// Center login button
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();		
			if (GUILayout.Button("Login")  || (Event.current.type == EventType.keyDown && Event.current.character == '\n')) {
				Debug.Log("Sending login request");
				smartFox.Send(new LoginRequest(username, password, zone));
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.FlexibleSpace();
			
			GUILayout.EndArea ();		
			
			GUILayout.BeginArea(new Rect(20, panelHeight-30, panelWidth-40, 80));
			// Display client status
			GUIStyle centeredLabelStyle = new GUIStyle(GUI.skin.label);
			centeredLabelStyle.alignment = TextAnchor.MiddleCenter;
			
			GUILayout.Label("Client status: " + serverConnectionStatusMessage, centeredLabelStyle);
			
			GUILayout.EndArea ();		
			GUILayout.EndVertical();
			GUILayout.EndArea ();		
		}
		
		// Lobby GUI consisting of a left side chat window, and right side room selection and user list in the given room
		void DrawLobbyGUI() {
			// Lets just quickly set up some GUI layout variables
			float chatPanelWidth = Screen.width * 3/4 - 10;
			float chatPanelHeight = Screen.height - 80;
			float chatPanelPosX = 10;
			float chatPanelPosY = 10;
			
			float roomPanelWidth = Screen.width * 1/4 - 10;
			float roomPanelHeight = Screen.height / 2 - 40 - 5;
			float roomPanelPosX = chatPanelPosX + chatPanelWidth;
			float roomPanelPosY = chatPanelPosY;

			float userPanelWidth = roomPanelWidth;
			float userPanelHeight = roomPanelHeight;
			float userPanelPosX = chatPanelPosX + chatPanelWidth;
			float userPanelPosY = chatPanelPosY + roomPanelHeight + 10;
			
			// Chat history panel
			GUILayout.BeginArea(new Rect(chatPanelPosX, chatPanelPosY, chatPanelWidth, chatPanelHeight));
			GUILayout.Box ("Chat", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
			GUILayout.BeginVertical();
			GUILayout.BeginArea(new Rect(20, 25, chatPanelWidth-40, chatPanelHeight-80), GUI.skin.customStyles[0]);
			
			chatScrollPosition = GUILayout.BeginScrollView (chatScrollPosition);
						
			// We use lock here to ensure cross-thread safety on the messages collection 
			lock (messagesLocker) {
				foreach (string message in messages) {
					GUILayout.Label(message);
				}
			}
				
			GUILayout.EndScrollView ();
			GUILayout.EndArea();

			// Send chat message text field and button
			GUILayout.BeginArea(new Rect(30, chatPanelHeight - 40, chatPanelWidth-60, 40));
			GUILayout.BeginHorizontal();
			GUILayout.Label("Public message:");
			newMessage = GUILayout.TextField(newMessage, 50, GUILayout.Width(chatPanelWidth - 220));

			if (GUILayout.Button("Send")  || (Event.current.type == EventType.keyDown && Event.current.character == '\n')) {
				smartFox.Send( new PublicMessageRequest(newMessage) );
				newMessage = "";
			}
			GUILayout.EndHorizontal();
						
			GUILayout.EndArea ();		
			GUILayout.EndVertical();
			GUILayout.EndArea ();
			
						
			// Room list
			GUILayout.BeginArea(new Rect(roomPanelPosX, roomPanelPosY, roomPanelWidth, roomPanelHeight));
			GUILayout.Box ("Rooms", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
			GUILayout.BeginVertical();
			GUILayout.BeginArea(new Rect(20, 25, roomPanelWidth-40, roomPanelHeight-80), GUI.skin.customStyles[0]);
			
			// We want some padding between buttons in the grid selection
			GUIStyle selectionStyle = new GUIStyle(GUI.skin.button);
			selectionStyle.margin = new RectOffset(4,4,4,4);
			
			roomScrollPosition = GUILayout.BeginScrollView (roomScrollPosition);
			roomSelection = GUILayout.SelectionGrid (roomSelection, roomStrings, 1, selectionStyle);
				
			if (roomStrings[roomSelection] != smartFox.LastJoinedRoom.Name) {
				JoinRoom(roomStrings[roomSelection]); 
				GUILayout.EndArea();
				return;
			}
			GUILayout.EndScrollView();
			
			GUILayout.EndArea ();
			
			GUILayout.BeginArea(new Rect(20, 25 + roomPanelHeight - 70, roomPanelWidth-40, 40));
			GUILayout.Label("Current room: " + smartFox.LastJoinedRoom.Name);
			
			GUILayout.EndArea ();
			GUILayout.EndVertical();
			GUILayout.EndArea ();
				
			
			// User list
			GUILayout.BeginArea(new Rect(userPanelPosX, userPanelPosY, userPanelWidth, userPanelHeight));
			GUILayout.Box ("Users", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
			GUILayout.BeginVertical();
			GUILayout.BeginArea(new Rect(20, 25, userPanelWidth-40, userPanelHeight-80), GUI.skin.customStyles[0]);
					
			userScrollPosition = GUILayout.BeginScrollView (userScrollPosition, GUILayout.Width (150), GUILayout.Height (160));
			foreach (User user in smartFox.LastJoinedRoom.UserList) {
				GUILayout.Label(user.Name);
			}
			GUILayout.EndScrollView ();

			GUILayout.EndArea ();
			
			// Current user info
			GUILayout.BeginArea(new Rect(20, 25 + userPanelHeight - 70, userPanelWidth-40, 40));
			GUILayout.BeginHorizontal();
			
			GUILayout.Label("Logged in as " + smartFox.MySelf.Name);
			
			GUILayout.FlexibleSpace();
			
			if (GUILayout.Button("Logout")) {
				smartFox.Send( new LogoutRequest() );
			}
			GUILayout.EndHorizontal();
			
			GUILayout.EndArea ();
			GUILayout.EndVertical();
			GUILayout.EndArea ();
		}
		
		// Disconnect from the socket when shutting down the game
		// ** Important for Windows users - can cause crashes otherwise
		public void OnApplicationQuit() {
			if (smartFox.IsConnected)
				smartFox.Disconnect();
			
			smartFox = null;
		}
		
		// Disconnect from the socket when the scene is unloaded
		// ** Important for Windows users - can cause crashes otherwise
		public void OnDestroy() {
			OnApplicationQuit();
		}
	}
}