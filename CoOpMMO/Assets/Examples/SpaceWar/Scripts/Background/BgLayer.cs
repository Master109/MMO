using UnityEngine;
using System.Collections;

namespace Sfs2XExamples.SpaceWar
{
	public class BgLayer : MonoBehaviour 
	{
		public float paralax = 0.05f;
		
		public void scroll(float scrollX, float scrollY)
		{
			renderer.material.mainTextureOffset = new Vector2(renderer.material.mainTextureOffset.x + scrollX * paralax, renderer.material.mainTextureOffset.y + scrollY * paralax);
		}
	}
}