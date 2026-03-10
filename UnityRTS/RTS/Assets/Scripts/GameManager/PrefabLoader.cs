using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Preloader
{
    /// <summary>
    /// PrefabLoader holds a reference to all prefabs in the game
    /// </summary>
	public class PrefabLoader : MonoBehaviour
	{
		#region Prefabs

		/// <summary>
        /// Blue Player Prefab
        /// </summary>
		[FormerlySerializedAs("HumanPlayerPrefab")]
		public GameObject BluePlayerPrefab;
        /// <summary>
        /// Red Player Prefab
        /// </summary>
		[FormerlySerializedAs("OrcPlayerPrefab")]
		public GameObject RedPlayerPrefab;

		/// <summary>
        /// Blue Pawn Prefab
        /// </summary>
		[FormerlySerializedAs("HumanPeasantPrefab")]
		[FormerlySerializedAs("HumanPawnPrefab")]
		public GameObject BluePawnPrefab;
        /// <summary>
        /// Blue Warrior Prefab
        /// </summary>
		[FormerlySerializedAs("HumanFootmanPrefab")]
		[FormerlySerializedAs("BlueFootmanPrefab")]
		public GameObject BlueWarriorPrefab;
        /// <summary>
        /// Blue Archer Prefab
        /// </summary>
		[FormerlySerializedAs("HumanArcherPrefab")]
		public GameObject BlueArcherPrefab;
        /// <summary>
        /// Blue Base Prefab
        /// </summary>
		[FormerlySerializedAs("HumanBasePrefab")]
		public GameObject BlueBasePrefab;
        /// <summary>
        /// Blue Barracks Prefab
        /// </summary>
		[FormerlySerializedAs("HumanBarracksPrefab")]
		public GameObject BlueBarracksPrefab;
        /// <summary>
        /// Blue Archery Prefab
        /// </summary>
		[FormerlySerializedAs("HumanArcheryPrefab")]
		public GameObject BlueArcheryPrefab;

		/// <summary>
        /// Red Pawn prefab
        /// </summary>
		[FormerlySerializedAs("OrcPeonPrefab")]
		[FormerlySerializedAs("OrcPawnPrefab")]
		public GameObject RedPawnPrefab;
        /// <summary>
        /// Red Warrior Prefab
        /// </summary>
		[FormerlySerializedAs("OrcGruntPrefab")]
		[FormerlySerializedAs("RedGruntPrefab")]
		public GameObject RedWarriorPrefab;
        /// <summary>
        /// Red Archer Prefab
        /// </summary>
		[FormerlySerializedAs("OrcAxethrowerPrefab")]
		[FormerlySerializedAs("RedAxethrowerPrefab")]
		public GameObject RedArcherPrefab;
        /// <summary>
        /// Red Base Prefab
        /// </summary>
		[FormerlySerializedAs("OrcBasePrefab")]
		public GameObject RedBasePrefab;
        /// <summary>
        /// Red Barracks Prefab
        /// </summary>
		[FormerlySerializedAs("OrcBarracksPrefab")]
		public GameObject RedBarracksPrefab;
        /// <summary>
        /// Red Archery Prefab
        /// </summary>
		[FormerlySerializedAs("OrcArcheryPrefab")]
		public GameObject RedArcheryPrefab;

		/// <summary>
        /// Mine Prefab
        /// </summary>
		public GameObject MinePrefab;

		/// <summary>
		/// Arrow sprite for archer projectiles
		/// </summary>
		public Sprite ArrowSprite;

		/// <summary>
		/// Fire animator controller for flaming arrow effect
		/// </summary>
		public RuntimeAnimatorController FireAnimatorController;

		/// <summary>
		/// Explosion animator controller for arrow impact effect
		/// </summary>
		public RuntimeAnimatorController ExplosionAnimatorController;

		/// <summary>
		/// Fire animator controllers for building impact fires (fire_01, fire_02, fire_03)
		/// </summary>
		public RuntimeAnimatorController Fire1AnimatorController;
		public RuntimeAnimatorController Fire2AnimatorController;
		public RuntimeAnimatorController Fire3AnimatorController;

		/// <summary>
		/// Dust 2 animator controller for unit death effect
		/// </summary>
		public RuntimeAnimatorController Dust2AnimatorController;

		/// <summary>
		/// Gold resource sprite for mining nugget effect
		/// </summary>
		public Sprite GoldResourceSprite;

		/// <summary>
		/// Small bar base sprite (health bar frame)
		/// </summary>
		public Sprite SmallBarBase;

		/// <summary>
		/// Small bar fill sprite (health bar fill)
		/// </summary>
		public Sprite SmallBarFill;

		/// <summary>
		/// Big bar base sprite (building health bar frame)
		/// </summary>
		public Sprite BigBarBase;

		/// <summary>
        /// Speed Textbox
        /// </summary>
		public Text SpeedText;
        /// <summary>
        /// Timer Textbox
        /// </summary>
		public Text TimerText;
        /// <summary>
        /// Blue score textbox
        /// </summary>
		[FormerlySerializedAs("HumanScoreText")]
        public Text BlueScoreText;
        /// <summary>
        /// Red Score textbox
        /// </summary>
		[FormerlySerializedAs("OrcScoreText")]
        public Text RedScoreText;
        /// <summary>
        /// Game Over UI
        /// </summary>
		public GameObject GameOverUI;
        /// <summary>
        /// Grid
        /// </summary>
		public GameObject Grid;
        /// <summary>
        /// Unit Debugger Prefab
        /// </summary>
		public GameObject UnitDebuggerPrefab;

		#endregion
	}
}
