using UnityEngine;
using System.Collections;

namespace Sfs2XExamples.MMORoom
{
	public class PlayerController : MonoBehaviour {
		
		public float forwardSpeed = 10;
		public float backwardSpeed = 8;
		public float rotationSpeed = 40;
		
		// Dirty flag for checking if movement was made or not
		public bool MovementDirty {get; set;}

		void Start() {
			MovementDirty = false;
		}
		
		void Update () {
			// Forward/backward makes player model move
			float translation = Input.GetAxis("Vertical");
			if (translation != 0) 
			{
				this.transform.Translate(0, 0, translation * Time.deltaTime * forwardSpeed);
				
				// Avoid going outside the perimeter of the "map"
				Vector3 pos = this.transform.position;
				
				if (pos.x < -100) pos.x = -100;
				if (pos.x > 100) pos.x = 100;
				
				if (pos.z < -100) pos.z = -100;
				if (pos.z > 100) pos.z = 100;
				
				this.transform.position = pos;
				
				MovementDirty = true;
			}
		
			// Left/right makes player model rotate around own axis
			float rotation = Input.GetAxis("Horizontal");
			if (rotation != 0) {
				this.transform.Rotate(Vector3.up, rotation * Time.deltaTime * rotationSpeed);
				MovementDirty = true;
			}
		}
	}
}
