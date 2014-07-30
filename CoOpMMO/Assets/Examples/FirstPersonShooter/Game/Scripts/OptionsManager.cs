using System;

namespace Sfs2XExamples.FPS
{
	public static class OptionsManager {

		private static bool invertMouseY = false;
		
		public static bool InvertMouseY {
			get {
				return invertMouseY;
			}
			set {
				invertMouseY = value;
			}
		}
	}
}