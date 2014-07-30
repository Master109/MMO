/*******************************************************************************************
* TITLE: Unity Buddy Messenger
* VERSION:	1.0
* RELEASE:	2012-09-03
* COPYRIGHT:	2012 gotoAndPlay() - http://www.smartfoxserver.com
* DEVELOPER:	Andy S. Martin, www.guitarrpg.com, zippo227@gmail.com
* SFS BLOG: http://www.clubconsortya.blogspot.com/
*******************************************************************************************/
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
using Sfs2X.Entities.Managers;
using Sfs2X.Entities.Variables;
using Sfs2X.Requests;
using Sfs2X.Requests.Buddylist;
using Sfs2X.Logging;
using Sfs2X.Util;
using Sfs2X.Entities.Data;

namespace Sfs2XExamples.BuddyMessenger
{
	public class BuddyMessenger : MonoBehaviour
	{
		//Server
		private SmartFox smartFox;
		public string serverName = "127.0.0.1";
		public int serverPort = 9933;
		private string zone = "BasicExamples";
		private string username = "";
		private int roomSelection = 0;
		private string loginErrorMessage = "";
		private string[] roomStrings;
		
		//Connection
		private Room currentActiveRoom;
		
		/*
		 * These state variables inform the OnGUI function
		 * of what the user currently needs to view
		 * in the buddy messenger
		 */		
		private bool isLoggedIn;
		private bool isJoining;
		private bool isSwitchingRooms;
		
		//User variables
		private bool myOnline;
		private string myNick;	
		private string myMood;
		private string myState;
		private string myAge;
		private string buddyRequestName = "";
		private string buddyChatName = "";
		private int selectedBuddy = -1;
		
		//Buddy Variables
		private string BUDDYVAR_AGE = SFSBuddyVariable.OFFLINE_PREFIX + "age";
		private string BUDDYVAR_MOOD = "mood";
		
		//Visuals
		private GUIStyle listStyle, detailStyle;
		public Texture2D sfsLogo, icon_available, icon_away, 
			icon_blocked, icon_occupied, icon_offline;
		
		//Messages
		private string newMessage = "";
		private ArrayList messages = new ArrayList();
		private List<Buddy> buddies = new List<Buddy>();
		private Vector2 chatScrollPosition, userScrollPosition;	
		//The messages can only be edited by one function at a time
		private System.Object messagesLocker = new System.Object();	
		
		//Combo Box for states and users
		private ComboBox comboBoxStateControl, comboBoxUserControl;
		GUIContent[] comboBoxStateList, comboBoxUserList;    
		private Rect comboBoxStateRect, comboBoxUserRect;
		GUIContent[] buddyContents;
		
		//These variables help to keep the GUI from trying to display
		//information / lists that are not complete
		private bool isBuddyListInited, isStateListInited, isUserListInited;
		
		//Awake runs before Start
		void Awake() {
			//list style is used for the combo box
			listStyle = new GUIStyle();
			listStyle.normal.textColor = Color.white;
	        listStyle.onHover.background =
	        listStyle.hover.background = new Texture2D(2, 2);
	        listStyle.padding.left =
	        listStyle.padding.right =
	        listStyle.padding.top =
	        listStyle.padding.bottom = 4;
			
			//This helps the mydetails section look better
			detailStyle = new GUIStyle();
			detailStyle.margin.bottom = 5;
			detailStyle.padding.left =
	        detailStyle.padding.right = 15;
			
			//Scroll postions have to be initialized
			chatScrollPosition = new Vector2(0,0);
			userScrollPosition = new Vector2(0,0);
		}
		
		//Start runs right before the continous updates of the scene
		void Start()
		{
			serverName = Settings.ipAddress;
			serverPort = Int32.Parse(Settings.port);

			smartFox = new SmartFox(true);
		 /*
		  * Add listeners
		  * NOTE: for sake of simplicty, most buddy-related events cause the whole
		  * buddylist in the interface to be recreated from scratch even if those
		  * events are caused by the current user himself. A more refined approach might
		  * update the specific list items.
		 */	
			// Register callback delegate
			smartFox.AddEventListener(SFSEvent.CONNECTION, OnConnection);
			smartFox.AddEventListener(SFSEvent.CONNECTION_LOST, OnConnectionLost);
			smartFox.AddEventListener(SFSEvent.LOGIN, OnLogin);
			smartFox.AddEventListener(SFSEvent.LOGIN_ERROR, OnLoginError);
			smartFox.AddEventListener(SFSEvent.LOGOUT, OnLogout);
			smartFox.AddEventListener(SFSEvent.ROOM_JOIN, OnJoinRoom);
			smartFox.AddEventListener(SFSEvent.PUBLIC_MESSAGE, OnPublicMessage);
			smartFox.AddEventListener(SFSEvent.PRIVATE_MESSAGE, OnPrivateMessage);
			smartFox.AddEventListener(SFSEvent.USER_COUNT_CHANGE, OnUserCountChange);
			
			// Callbacks for buddy events
			smartFox.AddEventListener(SFSBuddyEvent.BUDDY_LIST_INIT, OnBuddyListInit);
			smartFox.AddEventListener(SFSBuddyEvent.BUDDY_ERROR, OnBuddyError);
			smartFox.AddEventListener(SFSBuddyEvent.BUDDY_ONLINE_STATE_UPDATE, OnBuddyListUpdate);
			smartFox.AddEventListener(SFSBuddyEvent.BUDDY_VARIABLES_UPDATE, OnBuddyListUpdate);
			smartFox.AddEventListener(SFSBuddyEvent.BUDDY_ADD, OnBuddyAdded);
			smartFox.AddEventListener(SFSBuddyEvent.BUDDY_REMOVE, OnBuddyRemoved);
			smartFox.AddEventListener(SFSBuddyEvent.BUDDY_BLOCK, OnBuddyBlocked);
			smartFox.AddEventListener(SFSBuddyEvent.BUDDY_MESSAGE, OnBuddyMessage);
			smartFox.AddEventListener(SFSBuddyEvent.BUDDY_VARIABLES_UPDATE, OnBuddyVarsUpdate);
			
			smartFox.AddLogListener(LogLevel.DEBUG, OnDebugMessage);		
			Debug.Log(Application.platform.ToString());
			
			//Necessary for the web
			if(Application.isWebPlayer || Application.isEditor)
			{
	      		Security.PrefetchSocketPolicy(serverName, serverPort);
			}
				
			smartFox.Connect(serverName, serverPort);
		}
		
		//This, as it says, runs at a fixed interval
		void FixedUpdate()
		{
			smartFox.ProcessEvents();
		}
		
		//This runs every frame to show the User Interface 
		void OnGUI()
		{
			if (smartFox == null) return;
			//The logo is always visible
			GUI.Label(new Rect((Screen.width / 2) - 160, 0, 320, 48), sfsLogo);
			//A waiting screen while connecting to smart fox
			if (!smartFox.IsConnected)
			{
				GUI.Label(new Rect((Screen.width / 2) - 140, 50, 100, 50), "Connecting...");
			}
			//Will draw the login screen for the zone
			else if (!isLoggedIn)
			{
				DrawLogin();
			}
			//While you are joining the room
			else if (isJoining)
			{		
				GUI.Label(new Rect((Screen.width / 2) - 140, (Screen.height / 2) - 200, 100, 270), "Joining room");
			}
			//For when you choose to switch rooms
			else if (isSwitchingRooms)
			{
				SwitchRooms();
			}
			//The chat client has 3 functions to keep it up to date
			else if (currentActiveRoom != null)
			{
				DrawBuddyList();
				DrawBuddyChat();
				DrawMyDetails();
			}			
		}
		
		//Will draw the login screen for the zone
		private void DrawLogin() 
		{	 
			GUI.Label(new Rect((Screen.width / 2)  - 150, 75, 100, 100), "Zone: ");
			zone = GUI.TextField(new Rect((Screen.width / 2) - 50, 75, 200, 20), zone, 25);
			
			GUI.Label(new Rect((Screen.width / 2)- 150, 100, 100, 100), "Username: ");
			username = GUI.TextField(new Rect((Screen.width / 2) - 50, 100, 200, 20), username, 25);
			
			if (GUI.Button(new Rect((Screen.width / 2) + 50, 125, 100, 25), "Login") || (Event.current.type == EventType.keyDown && Event.current.character == '\n'))
			{
				Debug.Log("Sending login request");
				smartFox.Send(new LoginRequest(username, "", zone));
			}
			//Displays any error messages at the bottom
			GUI.Label(new Rect((Screen.width / 2) - 150, 175, 300, 100), loginErrorMessage);
		}
		
		//Allows the user to switch rooms
		private void SwitchRooms() 
		{
			GUILayout.BeginArea(new Rect((Screen.width / 2) - 200, (Screen.height / 2) - 250, 400, 500));
			GUILayout.Box("Room List", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
			
			GUILayout.BeginArea(new Rect(0,20, 400, 480));			
			roomSelection = GUILayout.SelectionGrid(roomSelection, roomStrings, 1);
			GUILayout.EndArea();
			GUILayout.EndArea();
			if (roomSelection > -1)
			{
				JoinRoom(roomStrings[roomSelection]);
				isSwitchingRooms = false;
			}			
		}
		
		/*
		 * The list of your buddies is in a selection grid and
		 * activating a buddy will enable a private chat.
		 * The 3 command buttons will perform action on 
		 * the buddy requests based on your selection from the active
		 * users combo box.
		 */ 
		private void DrawBuddyList() 
		{
			//Prevents an error if the user list is not initialized
			if(isUserListInited)
				comboBoxUserControl.Show();
			
			Rect userRect = new Rect((Screen.width / 2)-355, (Screen.height / 2)-300, 300, 630);
			GUILayout.BeginArea(userRect);
			
			//Prevents an error if the buddy list is not initialized
			if(isBuddyListInited) 
			{
				GUILayout.Box ("Buddies (" + buddies.Count + ")", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
				Rect posRect = new Rect(0, 20, 290, 400);
				GUILayout.BeginArea(posRect);
				//listRect height is set to the length of the list
				Rect listRect;
				if(buddyContents.Length == 0)
					listRect = new Rect( 0, 0, 270, 10);
				else
				{
					//This creates a scrollable selection grid of buddies
					//Click to activate/deactivate a buddy chat with that buddy
					listRect = new Rect( 0, 0, 270, listStyle.CalcHeight(buddyContents[0], 1.0f) * buddyContents.Length);
					userScrollPosition = GUI.BeginScrollView (new Rect(0, 0, 290, 380), userScrollPosition, listRect);
					selectedBuddy = GUI.SelectionGrid(listRect, selectedBuddy, buddyContents, 1, listStyle);
					GUI.EndScrollView();
				}			
				GUILayout.EndArea();
				//Select or deselect a buddy if the user clicks on the selection grid
				if(selectedBuddy > -1) 
				{
					string buddy = buddies[selectedBuddy].Name;
					if(buddyChatName != buddy)
						buddyChatName = buddy;
					else
						buddyChatName = "";

					selectedBuddy = -1;
				}			
			}		
			
			GUILayout.BeginArea(new Rect(0, 405, 300, 270));
			GUILayout.BeginHorizontal("box");		
			
			/* 
			 * For these 3 functions, you want to ensure 
			 * that a user name has been typed in or 
			 * selected before sending a request. 
			 */
			
			//Adds this user to my buddy lists
			if(GUILayout.Button ("Add") && buddyRequestName != smartFox.MySelf.Name) 
			{
				//Check if it is empty or set to the default combo box contents
				if(buddyRequestName != "No users in room" && buddyRequestName != "Select User"
					&& buddyRequestName != "")
					smartFox.Send(new AddBuddyRequest(buddyRequestName));	
			}
			
			//Block or Unblock a buddy
			if(GUILayout.Button ("Block") && buddyRequestName != smartFox.MySelf.Name)
			{
				if(buddyRequestName != "No users in room" && buddyRequestName != "Select User"
					&& buddyRequestName != "")
				if(smartFox.BuddyManager.ContainsBuddy(buddyRequestName) )
				{
					bool blockStatus = smartFox.BuddyManager.GetBuddyByName(buddyRequestName).IsBlocked;
					smartFox.Send(new BlockBuddyRequest(buddyRequestName, !blockStatus));
				}
			}
			
			//Remove a buddy from yourl list
			if(GUILayout.Button ("Remove") && buddyRequestName != smartFox.MySelf.Name) 
			{
				if(buddyRequestName != "No users in room" && buddyRequestName != "Select User"
					&& buddyRequestName != "")
				if(smartFox.BuddyManager.ContainsBuddy(buddyRequestName))
				{
					buddyChatName = "";
					smartFox.Send(new RemoveBuddyRequest(buddyRequestName));
				}
			}	
			GUILayout.EndHorizontal();		
			
			/*
			 * The user will either type a name into the text field
			 * or they will select the name from the list of users
			 * in their room.
			 */
			GUILayout.BeginHorizontal(detailStyle);
			buddyRequestName = GUILayout.TextField(buddyRequestName);
			GUILayout.EndHorizontal();	
			if( Event.current.type == EventType.Repaint ) 
			{
				comboBoxUserRect = new Rect((Screen.width / 2)-350, (Screen.height / 2)+ 170, 290, 20);
				comboBoxUserControl.SetRect(comboBoxUserRect);
				if(comboBoxUserControl.SelectedItemIndex > -1)
				{
					buddyRequestName = comboBoxUserList[comboBoxUserControl.SelectedItemIndex].text;
					//Populate the text field and reset the combo box
					comboBoxUserControl.SelectedItemIndex = -1;
					if(comboBoxUserList[0].text == "No users in room")
					{
						comboBoxUserControl.SetButtonContent(new GUIContent("No users in room"));
						buddyRequestName = "";
					}
					else
						comboBoxUserControl.SetButtonContent(new GUIContent("Select user"));
				}
			}			
			GUILayout.EndArea();
			GUILayout.EndArea();
		}
		
		//Buddy Chat window	
		private void DrawBuddyChat() 
		{		
			GUILayout.BeginArea(new Rect((Screen.width / 2)-50, (Screen.height / 2)-300, 400, 375));
			//Show all messages or only buddy messages with selected buddy
			string chatType;
			if(buddyChatName == "")
				chatType = "Public messages";
			else
			{
				chatType = "Buddy chat with ";
				//Show the buddy's nickname if available
				if(smartFox.BuddyManager.GetBuddyByName(buddyChatName).NickName != "")
					chatType += smartFox.BuddyManager.GetBuddyByName(buddyChatName).NickName;
				else
					chatType += buddyChatName;
			}
			GUILayout.Box (chatType, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
			
			GUILayout.BeginArea(new Rect(5, 5, 50, 20));
			//Make the chat public without having to click on a user
			if(GUILayout.Button("Public"))
				buddyChatName = "";
			GUILayout.EndArea();
			
			//This starts the scrollable area of messages
			GUILayout.BeginArea(new Rect(5, 30, 390, 300));
			chatScrollPosition = GUILayout.BeginScrollView (chatScrollPosition, false, true);
			
			//You are in public chat so show all messages by user name
			if(buddyChatName == "")
			{
				foreach (string message in messages)
				{
					lock (messagesLocker) 
					{			
						GUILayout.Label(message);
					}
				}
			}
			//You are in buddy chat so only show messages with that buddy by nick name
			else 
			{
				/*
				 * Messages are stored according to whom
				 * or from whom they were sent.
				 * In a buddy chat, you want to only see messages
				 * with a certain user and label them with that
				 * user's nickname.
				 */
				foreach (string message in messages)
				{
					string nickMessage = message;
					string nick = "";
					//Check for messages from you
					if(message.StartsWith("To " + buddyChatName)) 
					{
						nick = "You";
						int insertPosition = 3 + buddyChatName.Length;
						nickMessage = nick + message.Substring(insertPosition);
						lock (messagesLocker) 
						{			
							GUILayout.Label(nickMessage);
						}
					}
					//Here check for the buddy's most recent nick name
					else if(message.StartsWith("From " + buddyChatName))
					{
						nick = smartFox.BuddyManager.GetBuddyByName(buddyChatName).NickName;
						//Make sure they have a nickname, otherwise the default labeling is fine
						if(nick != "")
						{
							int insertPosition = 5 + buddyChatName.Length;
							nickMessage = nick + message.Substring(insertPosition);	
						}
						lock (messagesLocker) 
						{			
							GUILayout.Label(nickMessage);
						}
					}				
				}
			}		
			GUILayout.EndScrollView();
			GUILayout.EndArea();
			
			//Send either a buddy msg to selected budy or public msg
			newMessage = GUI.TextField(new Rect(5, 340, 340, 20), newMessage);
			if(GUI.Button(new Rect(345, 340, 50, 20), "Send") || (Event.current.type == EventType.keyDown && Event.current.character == '\n')) 
			{
				if(newMessage != "") {
					if( buddyChatName != "" ) {
						SendMessageToBuddy(newMessage, smartFox.BuddyManager.GetBuddyByName(buddyChatName));
					}
					else {
						smartFox.Send(new PublicMessageRequest(newMessage));	
					}
					newMessage = "";
				}			
			}
			GUILayout.EndArea();
		}
		
		/*
		 * This will enable a user to  view & change 
		 * his or her user variables.
		 */	
		private void DrawMyDetails() 
		{
			//Wait until the state list is initialized to show the combo box
			if(isStateListInited)
				comboBoxStateControl.Show();
			
			Rect detailsRect = new Rect((Screen.width / 2)-50, (Screen.height / 2) + 75, 400, 255);
			GUILayout.BeginArea(detailsRect);
			
			GUILayout.Box ("My details", GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
			GUILayout.BeginArea(new Rect(0, 25, 400, 280));
			
			GUILayout.BeginVertical();
			//Passing in the style helps separate the content
			GUILayout.BeginHorizontal();
			
			GUILayout.Space(110);
			//Displays a list of rooms in the current zone to choose from
			if(GUILayout.Button("Switch rooms", GUILayout.Width (120) ))
			{
				isSwitchingRooms = true;
				roomSelection = -1;
			}
			//This space helps keep the buttons separted
			GUILayout.Space(20);
			if(GUILayout.Button("Logout", GUILayout.Width (120)))
			{
				smartFox.Send(new LogoutRequest());
			}
			GUILayout.EndHorizontal();
			
			//Your user variables need to be initialized before showing this area
			if(isBuddyListInited)
			{
				//Online state
				GUILayout.BeginHorizontal(detailStyle);
				GUILayout.Label("Online: ");
				myOnline = GUILayout.Toggle(myOnline, "(as buddy)", GUILayout.Width(110));
				if(smartFox.BuddyManager.MyOnlineState != myOnline) 
				{
					smartFox.Send(new GoOnlineRequest(myOnline));
				}
				//Black out the details GUI to discourage use when offline
				if (myOnline == false)
					GUI.color = Color.black;
				GUILayout.EndHorizontal();
				
				//Nick name
				GUILayout.BeginHorizontal(detailStyle);
				GUILayout.Label("Nickname: ", GUILayout.Width(120));
				myNick = GUILayout.TextField(myNick, GUILayout.Width(110));
				GUILayout.Space(20);
				if(GUILayout.Button("Set", GUILayout.Width(100))) 
				{
					SFSBuddyVariable nick = new SFSBuddyVariable(ReservedBuddyVariables.BV_NICKNAME, myNick);
					UpdateBuddyVars(nick);
				}
				GUILayout.EndHorizontal();
				
				//Age
				GUILayout.BeginHorizontal(detailStyle);
				GUILayout.Label("Age: ", GUILayout.Width(120));
				myAge = GUILayout.TextArea(myAge,GUILayout.Width(110));
				GUILayout.Space(20);
				if(GUILayout.Button("Set", GUILayout.Width(100))) 
				{
					int newAge = 0;
					if(int.TryParse(myAge, out newAge))
					{
						SFSBuddyVariable age = new SFSBuddyVariable(BUDDYVAR_AGE, newAge);
						UpdateBuddyVars(age);
					}
				}
				GUILayout.EndHorizontal();
				
				//Mood
				GUILayout.BeginHorizontal(detailStyle);
				GUILayout.Label("Mood: ", GUILayout.Width(120));
				myMood = GUILayout.TextField(myMood, GUILayout.Width(110));
				GUILayout.Space(20);
				if(GUILayout.Button("Set", GUILayout.Width(100))) 
				{
					SFSBuddyVariable mood = new SFSBuddyVariable(BUDDYVAR_MOOD, myMood);
					UpdateBuddyVars(mood);
				}
				GUILayout.EndHorizontal();
				
				//State
				GUILayout.BeginHorizontal(detailStyle);
				GUILayout.Label("State: ", GUILayout.Width(120));
				if( Event.current.type == EventType.Repaint ) 
				{
					//places the states combo box in the proper spot
					comboBoxStateRect = GUILayoutUtility.GetLastRect();
					comboBoxStateControl.SetRect( new Rect(detailsRect.xMin + comboBoxStateRect.xMax, 
							detailsRect.yMin + comboBoxStateRect.yMax,110, 20));
				}
				//spaces the set button past the states combo box
				GUILayout.Space(135);
				if(GUILayout.Button("Set", GUILayout.Width(100))) 
				{
					myState = comboBoxStateList[comboBoxStateControl.SelectedItemIndex].text;
					SFSBuddyVariable state = new SFSBuddyVariable(ReservedBuddyVariables.BV_STATE, myState);
					UpdateBuddyVars(state);				
				}			
				GUILayout.EndHorizontal();
				
				GUILayout.BeginHorizontal(detailStyle);		
				GUILayout.Label("Logged in as: " + smartFox.MySelf.Name);
				GUILayout.EndHorizontal();
			}
			GUILayout.EndVertical();
			GUILayout.EndArea();		
			GUILayout.EndArea();	
		}
		
		//Takes your current text and sends it as a message to the selected buddy
		public void SendMessageToBuddy(string message, Buddy buddy)
		{
			// Adds a custom parameter containing the recipient name,
			// to write messages corresponding with proper buddy
			ISFSObject customParams = new SFSObject();
			customParams.PutUtfString("recipient", buddy.Name);		
			smartFox.Send(new BuddyMessageRequest(message, buddy, customParams));
		}
		
		//This is used to set mood, age, state, etc.
		public void UpdateBuddyVars(SFSBuddyVariable buddyVar) 
		{
			List<BuddyVariable> myVars = new List<BuddyVariable>();
	    	myVars.Add(buddyVar);
	    	smartFox.Send(new SetBuddyVariablesRequest(myVars));
		}
		
		/*
		 * All users in your room are shown in the 
		 * combo box so that you can see people 
		 * you would like to add as buddies
		 */	
		public void InitUsers() 
		{
			isUserListInited = false;
			List<User> users = currentActiveRoom.UserList;
			//You don't want to show up in the user list
			users.Remove(smartFox.MySelf);
			comboBoxUserList = new GUIContent[users.Count];
				
			if(users.Count == 0) 
			{
				for(int i=0; i<users.Count; i++)
				{
		        	comboBoxUserList[i] = new GUIContent(users[i].Name);
				}
				comboBoxUserList = new GUIContent[1];
				comboBoxUserList[0] = new GUIContent("No users in room");
				comboBoxUserControl = new ComboBox(comboBoxUserRect, comboBoxUserList[0], comboBoxUserList, true, listStyle);			
			}
			else
				comboBoxUserControl = new ComboBox(comboBoxUserRect, new GUIContent("Select User"), comboBoxUserList, true, listStyle);	
			
			comboBoxUserControl.SelectedItemIndex = -1;		
			isUserListInited = true;
		}
		
		//sets all possible user states in a combo box
		public void InitStates(List<String> states, int index) 
		{
			isStateListInited = false;
			comboBoxStateList = new GUIContent[states.Count];
			for(int i=0; i<states.Count; i++)
			{
	        	comboBoxStateList[i] = new GUIContent(states[i]);
			}
	       
	        comboBoxStateControl = new ComboBox(comboBoxStateRect, comboBoxStateList[index], comboBoxStateList, false, listStyle);
			isStateListInited = true;
		}
		
		//Reads all room names and joins the default room, usually room 0
		private void ReadRoomListAndJoin()
		{
			Debug.Log("Room list: ");
			
			List<Room> roomList = smartFox.RoomManager.GetRoomList();
			List<string> roomNames = new List<string>();
			foreach (Room room in roomList)
			{
				if (room.IsHidden || room.IsPasswordProtected)
				{
					continue;
				}
				
				roomNames.Add(room.Name);
				Debug.Log("Room id: " + room.Id + " has name: " + room.Name);		
			}
			
			roomStrings = roomNames.ToArray();
			
			if (smartFox.LastJoinedRoom == null)
			{
				JoinRoom(roomStrings[roomSelection]);
			}
		}	
		
		//Joins a specific room by name
		private void JoinRoom(string roomName)
		{
			if (isJoining) return;
			//User is already in selected room
			if(currentActiveRoom != null)
				if(currentActiveRoom.Name == roomName)
					return;
			isJoining = true;
			currentActiveRoom = null;
			Debug.Log("Joining room: " + roomName);
			
			// Need to leave current room, if we have joined one
			if (smartFox.LastJoinedRoom == null)
				smartFox.Send(new JoinRoomRequest(roomName));
			else
				smartFox.Send(new JoinRoomRequest(roomName, "", smartFox.LastJoinedRoom.Id));
		}
		
		//Runs when you have added a buddy succesfully	
		private void OnBuddyAdded(BaseEvent evt) 
		{
			Buddy buddy = (Buddy)evt.Params["buddy"];
			lock(messagesLocker) 
			{
		    	messages.Add( buddy + " added" );
			}
			//sends a message to that buddy
			smartFox.Send(new PrivateMessageRequest("added you as a buddy", buddy.Id));
			OnBuddyListUpdate(evt);
		}
		
		//Runs when you have removed a buddy successfully
		private void OnBuddyRemoved(BaseEvent evt) 
		{
			lock(messagesLocker) 
			{
		    	messages.Add( (Buddy)evt.Params["buddy"] + " removed" );
			}
			OnBuddyListUpdate(evt);
		}  
		
		//Runs when you have blocked a buddy successfully
		private void OnBuddyBlocked(BaseEvent evt) 
		{
			Buddy buddy = (Buddy)evt.Params["buddy"];
			string message = (buddy.IsBlocked ? " blocked" : "unblocked");
			
			lock(messagesLocker) 
			{
			    messages.Add( (Buddy)evt.Params["buddy"] + " " + message );
			}
			smartFox.Send(new PrivateMessageRequest(message + " you", buddy.Id));
			
			OnBuddyListUpdate(evt);
		}
		
		//Anytime there is an error, it is logged in the debug console
		private void OnBuddyError(BaseEvent evt)
		{	
			Debug.Log("The following error occurred in the buddy list system: " + (string)evt.Params["errorMessage"]);
		}
		
		//You always want the buddy list up to date
		private void OnBuddyListUpdate(BaseEvent evt)
		{
			//while updating, set this to false to keep it from displaying
			isBuddyListInited = false;
			buddies.Clear();
			buddies = smartFox.BuddyManager.BuddyList;
			buddyContents = new GUIContent[buddies.Count];
		
			for(int i = 0; i < buddies.Count; i++)
			{
				Buddy buddy = buddies[i];
				//Stores the content for a buddy
				GUIContent buddyContent = new GUIContent();
				
				//Determines which icon best represents this buddy's state			
				if(!buddy.IsOnline) 
				{
					buddyContent.image = icon_offline;
				}			
				else if(buddy.IsBlocked) 
				{
					buddyContent.image = icon_blocked;
				}			
				else switch(buddy.State) 
				{
					case "Available":
						buddyContent.image = icon_available;					
					break;
					
					case "Away":
						buddyContent.image = icon_away;
					break;
					
					case "Occupied":
						buddyContent.image = icon_occupied;
					break;
				}
				//Show the nickname if it's not blank
				//Otherwise, show the buddy's user name
				buddyContent.text = (buddy.NickName != null && buddy.NickName != "" ? buddy.NickName : buddy.Name);
				
				//The custom variables
				BuddyVariable age = buddy.GetVariable(BUDDYVAR_AGE);
				BuddyVariable mood = buddy.GetVariable(BUDDYVAR_MOOD);
				
				//Set the user's age, default is 30
				buddyContent.text += " Age: ";
				
				int newAge = ((age != null && !age.IsNull()) ? age.GetIntValue() : 30);
				buddyContent.text += newAge.ToString();
				
				//Set the user's mood, default is Asleep
				buddyContent.text += " Mood: ";			
				buddyContent.text += ((mood != null && !mood.IsNull()) ? mood.GetStringValue() : "Asleep");
				
				//Append this buddy's content to the end of the list
				buddyContents.SetValue(buddyContent, i);
			}
			//Now it's complete, so set this to true to display it
			isBuddyListInited = true;
		}
		
		//This initializes all buddies and your buddy variables
		private void OnBuddyListInit(BaseEvent evt)
		{
			// Populate user's list of buddies
			OnBuddyListUpdate(evt);		
			
			// Set current user's nick name
			if(smartFox.BuddyManager.MyNickName != null && smartFox.BuddyManager.MyNickName != "")
			{
				myNick = smartFox.BuddyManager.MyNickName;
			}
			else
			{
				myNick = smartFox.MySelf.Name;
				SFSBuddyVariable newNick = new SFSBuddyVariable(ReservedBuddyVariables.BV_NICKNAME, myNick);
				UpdateBuddyVars(newNick);
			}
			
			//Buddy variables
			BuddyVariable age = smartFox.BuddyManager.GetMyVariable(BUDDYVAR_AGE);
			int newAge = ((age != null && !age.IsNull()) ? age.GetIntValue() : 30);
			myAge = newAge.ToString();
			
			//You can have a custom mood
			BuddyVariable mood = smartFox.BuddyManager.GetMyVariable(BUDDYVAR_MOOD);
			myMood = ((mood != null && !mood.IsNull()) ? mood.GetStringValue() : "Awake");		
			SFSBuddyVariable newMood = new SFSBuddyVariable(BUDDYVAR_MOOD, myMood);
			UpdateBuddyVars(newMood);
			
			// List of strings of states the server can manage
			List<String> states = smartFox.BuddyManager.BuddyStates;
			String state = (smartFox.BuddyManager.MyState != null ? smartFox.BuddyManager.MyState : "");
			
			if (states.IndexOf(state) > -1)
			{
				InitStates(states, states.IndexOf(state));
			}
			else
			{
				InitStates(states, 0);
			}
			//Your details can now be displayed
			myOnline = true;
		}
		
		/*
		 * This does a few checks to see if the sender
		 * is you or someone else, then it labels the
		 * message as to or from.
		 */
		private void OnBuddyMessage(BaseEvent evt)
		{
			Buddy buddy;
			Buddy sender;
			Boolean isItMe = (bool)evt.Params["isItMe"];
			string message = (string) evt.Params["message"];
			string buddyName;
			
			sender = (Buddy)evt.Params["buddy"];
			if (isItMe)
			{
				ISFSObject playerData = (SFSObject)evt.Params["data"];
				buddyName = playerData.GetUtfString("recipient");
				buddy = smartFox.BuddyManager.GetBuddyByName(buddyName);
			}
			else 
			{			
				buddy = sender;
				buddyName = "";
			}
			if (buddy != null) 
			{
				//Store the message according to the sender's name
				message = (isItMe ? "To " + buddyName : "From " + buddy.Name) + ": " + message;
				lock(messagesLocker) 
				{
		    		messages.Add( message );
				}
				//This forces the chat to show the most recent message
				chatScrollPosition.y = Mathf.Infinity;
			}
		}
		
		//Anytime a buddy variable is updated
		private void OnBuddyVarsUpdate(BaseEvent evt) 
		{
			Debug.Log (("Buddy variables update from: " + (Buddy)evt.Params["buddy"]));
		}
		
		//Public messages are sent to all users
		private void OnPublicMessage(BaseEvent evt)
		{
			try
			{
				string message = (string)evt.Params["message"];
				User sender = (User)evt.Params["sender"];
				
				// We use lock here to ensure cross-thread safety on the messages collection
				lock (messagesLocker)
				{
					messages.Add(sender.Name + " said: " + message);
				}
				//This forces the chat to show the most recent message
				chatScrollPosition.y = Mathf.Infinity;
				Debug.Log("User " + sender.Name + " said: " + message);
			}
			catch (Exception ex)
			{
				Debug.Log("Exception handling public message: " + ex.Message + ex.StackTrace);
			}
		}
		
		/*
		 * Use private messsages to send status
		 * updates to other users, and they will be
		 * visible only in the receiver's group messages
		 */
		private void OnPrivateMessage(BaseEvent evt)
		{
			try
			{
				string message = (string)evt.Params["message"];
				User sender = (User)evt.Params["sender"];
				if(sender.Name != smartFox.MySelf.Name)
				{
					// We use lock here to ensure cross-thread safety on the messages collection
					lock (messagesLocker)
					{
						messages.Add(sender.Name + " " + message);
					}
					//This forces the chat to show the most recent message
					chatScrollPosition.y = Mathf.Infinity;				
				}
			}
			catch (Exception ex)
			{
				Debug.Log("Exception handling private message: " + ex.Message + ex.StackTrace);
			}
		}
		
		//Alerts your console on the success of the connection
		private void OnConnection(BaseEvent evt)
		{
			bool success = (bool)evt.Params["success"];
			string error = (string)evt.Params["errorMessage"];
			
			Debug.Log("On Connection callback got: " + success + " (error : <" + error + ">)");
		}
		
		//Will give you the reason why you were disconnected
		private void OnConnectionLost(BaseEvent evt)
		{
			string reason = (string)evt.Params["Reason"];
			string message = "Connection lost";
			if (reason != ClientDisconnectionReason.MANUAL)
			{
				if(reason == ClientDisconnectionReason.IDLE)
					message += "\nYou have exceeded the maximum user idle time";
				else if(reason == ClientDisconnectionReason.KICK)
					message += "\nYou have been kicked";
				else if(reason == ClientDisconnectionReason.BAN)
					message += "\nYou have been banned";
				else if(reason == ClientDisconnectionReason.UNKNOWN)
					message += " due to unknown reason\nPlease check the server-side log";
			}
						
			Debug.Log(message);
			UnregisterSFSSceneCallbacks();
			Application.LoadLevel ("Buddy");
		}
		
		//Runs when you join a room and notifies you in your chat log
		private void OnJoinRoom(BaseEvent evt)
		{
			Room room = (Room)evt.Params["room"];
			Debug.Log("Room " + room.Name + " joined successfully");
			
			lock (messagesLocker)
			{
				messages.Clear();
				messages.Add("You joined " + room.Name);
			}
			
			currentActiveRoom = room;
			
			//Initialzes the list of users in your room
			InitUsers();
			isJoining = false;
		}
		
		//When you log in, this tells smart fox to initialize your buddy list
		private void OnLogin(BaseEvent evt)
		{
			try
			{
				if (evt.Params.Contains("success") && !(bool)evt.Params["success"])
				{
					loginErrorMessage = (string)evt.Params["errorMessage"];
					Debug.Log("Login error: " + loginErrorMessage);
				}
				else
				{
					isLoggedIn = true;
					Debug.Log("Logged in successfully");
					ReadRoomListAndJoin();
					smartFox.Send(new InitBuddyListRequest());
				}
			}
			catch (Exception ex)
			{
				Debug.Log("Exception handling login request: " + ex.Message + " " + ex.StackTrace);
			}
		}
		
		//For any login errors
		private void OnLoginError(BaseEvent evt)
		{
			Debug.Log("Login error: " + (string)evt.Params["errorMessage"]);
			loginErrorMessage = "Login error: " + (string)evt.Params["errorMessage"];
		}
		
		//Cleans up the variables when you log out
		private void OnLogout(BaseEvent evt)
		{
			Debug.Log("OnLogout");
			isLoggedIn = false;
			isJoining = false;
			isBuddyListInited = false;
			isUserListInited = false;
			currentActiveRoom = null;
			smartFox.BuddyManager.Inited = false;
		}	
		
		//Keeps your user list up to date
		private void OnUserCountChange(BaseEvent evt) 
		{
			List<User> users = currentActiveRoom.UserList;
			//You don't want to show up in your own user list
			users.Remove(smartFox.MySelf);
			isUserListInited = false;
			if(users.Count != 0)
			{
				comboBoxUserList = new GUIContent[users.Count];
				for(int i=0; i<users.Count; i++)
				{
		        	comboBoxUserList[i] = new GUIContent(users[i].Name);
				}
				//There are users in the room to select
				comboBoxUserControl.SetButtonContent(new GUIContent("Select User"));
			}
			else
			{
				comboBoxUserList = new GUIContent[1];
				//There are not users in the room to select
				comboBoxUserList[0] = new GUIContent("No users in room");
				comboBoxUserControl.SetButtonContent(comboBoxUserList[0]);
			}
			comboBoxUserControl.SetList(comboBoxUserList);
			comboBoxUserControl.SelectedItemIndex = -1;
			isUserListInited = true;
		}
		
		// This should be called when switching scenes so that callbacks from the backend do not trigger code in this scene
		private void UnregisterSFSSceneCallbacks()
		{
			smartFox.RemoveAllEventListeners();
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
		
		//Anytime a debug message is sent to you
		private void OnDebugMessage(BaseEvent evt)
		{
			string message = (string)evt.Params["message"];
			Debug.Log("[SFS DEBUG] " + message);
		}
	} 	
}