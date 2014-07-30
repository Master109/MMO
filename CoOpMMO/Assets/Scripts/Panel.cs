using UnityEngine;
using System.Collections;
using System;

//[ExecuteInEditMode]
public class Panel : MonoBehaviour 
{
	public GUISkin sfsSkin;

	public GameObject connectionSettings;
	public GameObject examples;



	private int leftHalfMargin;
	private int rightHalfMargin;

	private bool showExamples = false;

	private static Settings settings;

	void Start()
	{
		if(settings != null)
		{
			showExamples = true;
		}
		else
		{
			settings = Settings.Instance;
		}
		Application.runInBackground = true;
	}

	void Update()
	{
		
		if(showExamples)
		{
			connectionSettings.SetActive(false);
			examples.SetActive(true);
		}
		else
		{
			connectionSettings.SetActive(true);
			examples.SetActive(false);
		}
	}


	void OnGUI()
	{
		GUI.skin = sfsSkin;

		// left column
		leftHalfMargin = Screen.width/2 - 480;


		// examples
		if(showExamples)
		{
			
			if (GUI.Button(new Rect(leftHalfMargin, 190, 200, 29), "Connector")) 
			{
				Application.LoadLevel("Connector");
			}

			if (GUI.Button(new Rect(leftHalfMargin, 230, 200, 29), "Lobby")) 
			{
				Application.LoadLevel("lobby");
			}

			if (GUI.Button(new Rect(leftHalfMargin, 270, 200, 29), "Buddy Messenger")) 
			{
				Application.LoadLevel("BuddyMessenger");
			}
			
			if (GUI.Button(new Rect(leftHalfMargin, 310, 200, 29), "Object Movement")) 
			{
				Application.LoadLevel("ObjectMovementConnection");
			}
			
			if (GUI.Button(new Rect(leftHalfMargin, 350, 200, 29), "Tris")) 
			{
				Application.LoadLevel("TrisLogin");
			}

			if (GUI.Button(new Rect(leftHalfMargin, 390, 200, 29), "First Person Shooter")) 
			{
				Application.LoadLevel("FPSlogin");
			}
			
			if (GUI.Button(new Rect(leftHalfMargin, 430, 200, 29), "MMO Room Demo")) 
			{
				Application.LoadLevel("MMORoomConnection");
			}
			
			if (GUI.Button(new Rect(leftHalfMargin, 470, 200, 29), "SpaceWar")) 
			{
				Application.LoadLevel("SpaceWarGame");
			}

			
			if (GUI.Button(new Rect(leftHalfMargin + 340, 560, 120, 29), "Back")) 
			{
				showExamples = false;
			}
		}
		else // Connection settings
		{
			Settings.ipAddress = GUI.TextField(new Rect(leftHalfMargin + 70, 473, 130, 29), Settings.ipAddress, 15);
			Settings.port = GUI.TextField(new Rect(leftHalfMargin + 250, 473, 70, 29), Settings.port, 6);
			
			
			if (GUI.Button(new Rect(leftHalfMargin, 245, 200, 29), "Download SmartFoxServer 2X")) 
			{
				Application.OpenURL("http://smartfoxserver.com/download/sfs2x#p=installer");
			}
			
			
			if (GUI.Button(new Rect(leftHalfMargin + 220, 245, 200, 29), "Download latest patch")) 
			{
				Application.OpenURL("http://smartfoxserver.com/download/sfs2x#p=updates");
			}

			
			if (GUI.Button(new Rect(leftHalfMargin + 340, 473, 120, 29), "Show examples")) 
			{
				showExamples = true;
			}
		}


		// right column
		rightHalfMargin = Screen.width/2 + 20;
		
		if (GUI.Button(new Rect(rightHalfMargin, 235, 200, 29), "Visit the live examples")) 
		{
			Application.OpenURL("http://smartfoxserver.com/overview/demo#unity");
		}


		if (GUI.Button(new Rect(rightHalfMargin, 395, 200, 29), "Introduction to SFS2X+Unity")) 
		{
			Application.OpenURL("http://docs2x.smartfoxserver.com/ExamplesUnity/introduction");
		}
		
		if (GUI.Button(new Rect(rightHalfMargin, 435, 235, 29), "SmartFoxServer 2X Documentation")) 
		{
			Application.OpenURL("http://docs2x.smartfoxserver.com/");
		}
		
		if (GUI.Button(new Rect(rightHalfMargin, 475, 290, 29), "SFS2X+Unity video tutorials by GenesisRage")) 
		{
			Application.OpenURL("http://genesisrage.net/tutorials/unity-smartfox");
		}
		
		if (GUI.Button(new Rect(rightHalfMargin, 515, 200, 29), "SFS2X licensing options")) 
		{
			Application.OpenURL("http://www.smartfoxserver.com/products/sfs2x#p=licensing");
		}
	}

}