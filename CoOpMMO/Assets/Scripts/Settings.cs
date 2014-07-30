using UnityEngine;
using System.Collections;

public class Settings : MonoBehaviour 
{
	public static string ipAddress = "127.0.0.1";
	public static string port = "9933";

	private static Settings instance;
	// Static singleton property
	public static Settings Instance
	{
		get 
		{ 
			if(instance != null)
			{
				return instance;
			}
			else
			{
				instance = new GameObject("Settings").AddComponent<Settings>();
				return instance;
			}
		}
	}
	
	void Awake()
	{
		if(instance == null)
		{ 	
			instance = this;
			GameObject.DontDestroyOnLoad(this.gameObject);
		} 
		else 
		{					
			GameObject.Destroy(this.gameObject);
		}
	}


	
	void OnGUI()
	{
		if(Application.loadedLevelName != "Panel")
		{
			GUIStyle guiStyle = new GUIStyle();
			guiStyle.fontSize = 16;
			guiStyle.normal.textColor = Color.white;
			if(Application.loadedLevelName == "BuddyMessenger")
			{
				GUI.Label(new Rect(10, 10, 250, 30), "Press ESC to return to the main menu", guiStyle);
			}
			else
			{
				GUI.Label(new Rect(Screen.width / 2 - 130, Screen.height - 50, 250, 30), "Press ESC to return to the main menu", guiStyle);
			}
		}
	}

	void Update() 
	{
		if(Application.loadedLevelName != "Panel" && Input.GetKeyDown(KeyCode.Escape))
		{
			// Check if there's a SmartFoxConnection static class
			GameObject smartFoxConnection = GameObject.Find("SmartFoxConnection");
			if(smartFoxConnection != null) {
				smartFoxConnection.SendMessage("Disconnect");
			}

			Screen.showCursor = true;
			Screen.lockCursor = false;
			Application.LoadLevel("Panel");
		}
	}


}
