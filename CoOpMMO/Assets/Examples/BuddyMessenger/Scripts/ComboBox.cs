/*******************************************************************************************
* TITLE: Unity Buddy Messenger
* VERSION:	1.0
* RELEASE:	2012-09-03
* COPYRIGHT:	2012 gotoAndPlay() - http://www.smartfoxserver.com
* DEVELOPER:	Andy S. Martin, www.guitarrpg.com, zippo227@gmail.com
* SFS BLOG: http://www.clubconsortya.blogspot.com/
*******************************************************************************************/

using UnityEngine;

namespace Sfs2XExamples.BuddyMessenger
{
	public class ComboBox
	{
	    private bool forceToUnShow = false;
	    private int useControlID = -1;
	    private bool isClickedComboButton = false;
		private bool scrollable;
	    private int selectedItemIndex = 0;
	   
	    private Rect rect;
	    private GUIContent buttonContent;
	    private GUIContent[] listContent;
	    private string buttonStyle;
	    private string boxStyle;
	    private GUIStyle listStyle;
		private Vector2 scrollPosition;

	    public ComboBox( Rect rect, GUIContent buttonContent, GUIContent[] listContent, bool scrollable, GUIStyle listStyle )
		{
	        this.rect = rect;
	        this.buttonContent = buttonContent;
	        this.listContent = listContent;
	        this.buttonStyle = "button";
	        this.boxStyle = "box";
	        this.listStyle = listStyle;
			this.scrollable = scrollable;
	    }
		
		public void SetRect(Rect newRect) {
			rect = newRect;	
		}
		
		public void SetList(GUIContent[] list) {
			listContent = list;
		}
		
		public void SetButtonContent(GUIContent buttonContent) {
			this.buttonContent = buttonContent;
		}
	   
	    public int Show()
	    {
	        if( forceToUnShow )
	        {
	            forceToUnShow = false;
	            isClickedComboButton = false;
	        }

	        bool done = false;
	        int controlID = GUIUtility.GetControlID( FocusType.Passive );      

	        switch( Event.current.GetTypeForControl(controlID) )
	        {
	            case EventType.mouseUp:
	            {
	                if( isClickedComboButton )
	                {
	                    done = true;
	                }
	            }
	            break;
	        }      

	        if( GUI.Button( rect, buttonContent, buttonStyle ) )
	        {
	            if( useControlID == -1 )
	            {
	                useControlID = controlID;
	                isClickedComboButton = false;
	            }

	            if( useControlID != controlID )
	            {
	                forceToUnShow = true;
	                useControlID = controlID;
	            }
	            isClickedComboButton = true;
	        }
	       
	        if( isClickedComboButton )
	        {
				int newSelectedItemIndex;
				if(!scrollable) 
				{
		            Rect listRect = new Rect( rect.x, rect.y + listStyle.CalcHeight(listContent[0], 1.0f),
		                      rect.width, listStyle.CalcHeight(listContent[0], 1.0f) * listContent.Length );
		
		            GUI.Box( listRect, "", boxStyle );
					newSelectedItemIndex = GUI.SelectionGrid(listRect, selectedItemIndex, listContent, 1, listStyle );
				}
				else
				{
					Rect posRect = new Rect( rect.x, rect.y + listStyle.CalcHeight(listContent[0], 1.0f),
	                      rect.width, listStyle.CalcHeight(listContent[0], 1.0f) * 5);
		            Rect listRect = new Rect( rect.x, rect.y + listStyle.CalcHeight(listContent[0], 1.0f),
		                      rect.width - 20, listStyle.CalcHeight(listContent[0], 1.0f) * listContent.Length);
		
		            GUI.Box( posRect, "", boxStyle );
					scrollPosition = GUI.BeginScrollView(posRect, scrollPosition, listRect);
					newSelectedItemIndex = GUI.SelectionGrid(listRect, selectedItemIndex, listContent, 1, listStyle );
		            GUI.EndScrollView();
				}
				
				if( done )
				{
		            isClickedComboButton = false;
		
					if( newSelectedItemIndex != selectedItemIndex )
					{
		                selectedItemIndex = newSelectedItemIndex;
						buttonContent = listContent[selectedItemIndex];
					}
				}
	        }

	        
	        return selectedItemIndex;
	    }
		
		public int SelectedItemIndex{
	        get{
	            return selectedItemIndex;
	        }
	        set{
	            selectedItemIndex = value;
	        }
	    }
	}
}