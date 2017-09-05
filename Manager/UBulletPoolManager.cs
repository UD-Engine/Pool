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

using DG.Tweening;

namespace UDEngine.Components.Pool {
	/// <summary>
	/// This is one of the globals that controls the pooling
	/// Unlike the previous version of the Engine, we will have only pools CONCEPTUALLY
	/// All usable bullets, no matter what happens, will live in UPool -> Pool, without subindexing
	/// They will be tracked merely with List-like storage
	/// </summary>
	public class UBulletPoolManager : MonoBehaviour {

		#region UNITYFUNC
		// Moving to Awake() to avoid unexpected uninitialized bug
		void Awake () {
			this.pools = new List<Stack<UBulletObject>> ();

			int prototypesLen = this.prototypes.Count;
			for (int i = 0; i < prototypesLen; i++) {
				UBulletObject obj = prototypes [i];

				// Set pool ID
				obj.poolID = i;

				// Insert a new pool entry
				pools.Add(new Stack<UBulletObject>());
			}

			if (this.poolTransform == null) {
				this.poolTransform = (new GameObject ("_Pool")).transform;
				this.poolTransform.parent = this.transform;
			}
		}

		void Start () {
		}

		// Update is called once per frame
		void Update () {

		}
		#endregion

		#region PROP
		/// <summary>
		/// The bullet prototypes. List index serve as the ID
		/// </summary>
		public List<UBulletObject> prototypes;

		/// <summary>
		/// The pools. As far as I know, using Queue/Stack does NOT improve much performance
		/// </summary>
		public List<Stack<UBulletObject>> pools;

		/// <summary>
		/// The transform where all bullets would live in
		/// </summary>
		public Transform poolTransform;

		/// <summary>
		/// The collision monitor.
		/// </summary>
		public UCollisionMonitor collisionMonitor;
		#endregion

		#region METHOD
		public List<UBulletObject> GetPrototypes() {
			return this.prototypes;
		}
		public int GetPrototypesCount() {
			return this.prototypes.Count;
		}
		public int GetPoolID(UBulletObject obj) {
			return obj.GetPoolID ();
		}
		public Transform GetPoolTransform() {
			return this.poolTransform;
		}

		/// <summary>
		/// Preloads the bullet by ID.
		/// NOTICE: ALL preloaded bullets would be INACTIVE by default
		/// </summary>
		/// <param name="id">Identifier.</param>
		/// <param name="count">Count.</param>
		public void PreloadBulletByID(int id, int count) {
			for (int i = 0; i < count; i++) {
				// This GetComponent<UBulletObject> is a must... though slow...
				// But since it is happened at the beginning of stage, should not be a huge problem
				UBulletObject bulletObject = (Instantiate(this.prototypes[id].trans.gameObject) as GameObject).GetComponent<UBulletObject>();

				// Setting the pool manager of the new bullet to self
				bulletObject.poolManager = this;
				// Make it a child of poolTransform
				bulletObject.trans.parent = poolTransform;
				// Make this newly created bullet inactive
				bulletObject.gameObject.SetActive(false);
				// Push the newly created bullet to corresponding stack 
				this.pools[id].Push(bulletObject);
			}
		}

		/// <summary>
		/// Fetchs the bullet with ID.
		/// Here we will do separation of tasks
		/// a.k.a we will NOT worry about ANYTHING about position, rotation, etc. during fetching
		/// NOTICE: ALL fetched bullet would be INACTIVE at the beginning
		/// NOTICE: the last shouldActivateCollider is defaulted to TRUE
		/// </summary>
		/// <returns>The bullet by ID.</returns>
		/// <param name="id">ID.</param>
		public UBulletObject FetchBulletByID(int id, bool shouldActivate = false, bool shouldAddToMonitor = true, bool shouldActivateCollider = true) {
			Stack<UBulletObject> thePool = this.pools [id];
			if (thePool.Count <= 0) {
				UBulletObject bulletObject = (Instantiate (this.prototypes [id].trans.gameObject) as GameObject).GetComponent<UBulletObject> ();
				// Setting the pool manager of the new bullet to self
				bulletObject.poolManager = this;
				// Make it a child of poolTransform
				bulletObject.trans.parent = poolTransform;
				if (shouldActivate) {
					// Setting recyclable to FALSE to avoid unexpected removal from colliderMonitor
					bulletObject.collider.SetRecyclable (false);
					if (shouldAddToMonitor) {
						collisionMonitor.AddBulletCollider (bulletObject.collider);
						if (shouldActivateCollider) {
							bulletObject.collider.SetEnable (true);
						}
					}
				} else {
					bulletObject.gameObject.SetActive (false);
				}

				bulletObject.trans.DOKill ();
				return bulletObject;
			} else {
				UBulletObject bulletObject = this.pools [id].Pop ();
				if (shouldActivate) {
					bulletObject.gameObject.SetActive(true);
					// Setting recyclable to FALSE to avoid unexpected removal from colliderMonitor
					bulletObject.collider.SetRecyclable (false);
					if (shouldAddToMonitor) {
						collisionMonitor.AddBulletCollider (bulletObject.collider);
						if (shouldActivateCollider) { // activate collider ONLY when the whole gameObject is first activated
							bulletObject.collider.SetEnable (true);
						}
					}
				}

				bulletObject.trans.DOKill ();
				return bulletObject;
			}
		}

		public void EmptyPoolByID(int id, bool shouldClearObjects = true) {
			Stack<UBulletObject> thePool = this.pools [id];
			if (shouldClearObjects) {
				foreach (UBulletObject obj in thePool) {
					// Notice here NO safety checks for tweening removal, etc. are done
					// We are under the assumption that these should already be handled
					// in possibly Recycle()
					Destroy (obj.trans.gameObject);
				}
			}
			// Overwrite old pool
			this.pools [id] = new Stack<UBulletObject> ();
		}

		/// <summary>
		/// Recycles the bullet.
		/// </summary>
		/// <param name="bulletObject">Bullet object.</param>
		/// <param name="shouldRecycleChildren">If set to <c>true</c> should recycle children.</param>
		/// <param name="shouldSplitChildrenOnRecycle">If set to <c>true</c> should split children to individual pools on recycle.</param>
		public void RecycleBullet(UBulletObject bulletObject, bool shouldRecycleChildren = false, bool shouldSplitChildrenOnRecycle = false) {
			// Stopping all running tweens and sequences
			bulletObject.trans.DOKill();
			bulletObject.actor.KillAllTweenSequences();

			// FIX: Terminate all callbacks (MUST DO!!!)
			bulletObject.actor.ClearAllCallbacks ();

			bulletObject.collider.SetEnable (false);
			// Removing current bullet collision detection is done by signaling the collisionMonitor
			// To avoid racing, ALWAYS setRecyclable to true in calling 
			bulletObject.collider.SetRecyclable (true);
			bulletObject.gameObject.SetActive (false);

			// Push to the pool stack
			this.pools [bulletObject.GetPoolID ()].Push (bulletObject);

			if (shouldRecycleChildren && bulletObject.children.Count >= 0) {
				foreach (UBulletObject childObject in bulletObject.children) {
					this._RecycleChildBulletHelper(childObject, shouldRecycleChildren, shouldSplitChildrenOnRecycle);
				}
			}
		}

		/// <summary>
		/// Recycles the child bullet helper.
		/// This is used to help recycling the children bullets
		/// The different from the above method is that the PUSH TO STACK is dependent on shouldSplitChildrenOnRecycle
		/// This takes into account of the idea that MAYBE you want to reclaim the children bullets on recycle
		/// while the FIRST direct call to PUSH in above method is a MUST, while children ones are NOT
		/// </summary>
		/// <param name="bulletObject">Bullet object.</param>
		/// <param name="shouldRecycleChildren">If set to <c>true</c> should recycle children.</param>
		/// <param name="shouldSplitChildrenOnRecycle">If set to <c>true</c> should split children to individual pools on recycle.</param>
		private void _RecycleChildBulletHelper(UBulletObject bulletObject, bool shouldRecycleChildren = false, bool shouldSplitChildrenOnRecycle = false) {
			// Stopping all running tweens and sequences
			bulletObject.trans.DOKill();
			bulletObject.actor.KillAllTweenSequences();

			// FIX: Terminate all callbacks (MUST DO!!!)
			bulletObject.actor.ClearAllCallbacks ();

			bulletObject.collider.SetEnable (false);
			// Removing current bullet collision detection is done by signaling the collisionMonitor
			// To avoid racing, ALWAYS setRecyclable to true in calling 
			bulletObject.collider.SetRecyclable (true);
			bulletObject.gameObject.SetActive (false);

			if (shouldSplitChildrenOnRecycle) {
				this.pools [bulletObject.GetPoolID ()].Push (bulletObject);
			}

			if (shouldRecycleChildren && bulletObject.children.Count >= 0) {
				foreach (UBulletObject childObject in bulletObject.children) {
					this._RecycleChildBulletHelper(childObject, shouldRecycleChildren, shouldSplitChildrenOnRecycle);
				}
			}
		}

		#endregion
	}
}
