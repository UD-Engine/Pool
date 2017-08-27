using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UDEngine;
using UDEngine.Components;
using UDEngine.Components.Bullet;
using UDEngine.Components.Collision;
using UDEngine.Internal;
using UDEngine.Interface;
using UDEngine.Enum;

namespace UDEngine.Components.Pool {
	/// <summary>
	/// This is one of the globals that controls the pooling
	/// Unlike the previous version of the Engine, we will have only pools CONCEPTUALLY
	/// All usable bullets, no matter what happens, will live in UPool -> Pool, without subindexing
	/// They will be tracked merely with List-like storage
	/// </summary>
	public class UBulletPoolManager : MonoBehaviour {

		#region UNITYFUNC
		// Use this for initialization
		void Start () {
			int prototypesLen = this.prototypes.Count;
			for (int i = 0; i < prototypesLen; i++) {
				UBulletObject obj = prototypes [i];

				// Set pool ID
				obj.poolID = i;
			}
		}

		// Update is called once per frame
		void Update () {

		}
		#endregion

		#region PROP
		/// <summary>
		/// The bullet prototypes. List index serve as the ID of the
		/// </summary>
		public List<UBulletObject> prototypes;
		#endregion

		#region METHOD
		public int GetPoolID(UBulletObject obj) {
			return obj.GetPoolID ();
		}
		#endregion
	}
}
