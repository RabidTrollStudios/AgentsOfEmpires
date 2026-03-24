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
		#region Unit & Player Prefabs

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
		/// Blue Lancer Prefab
		/// </summary>
		public GameObject BlueLancerPrefab;
		/// <summary>
		/// Blue Tower Prefab
		/// </summary>
		public GameObject BlueTowerPrefab;
		/// <summary>
		/// Blue Monastery Prefab
		/// </summary>
		public GameObject BlueMonasteryPrefab;
		/// <summary>
		/// Blue Monk Prefab
		/// </summary>
		public GameObject BlueMonkPrefab;

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
		/// Red Lancer Prefab
		/// </summary>
		public GameObject RedLancerPrefab;
		/// <summary>
		/// Red Tower Prefab
		/// </summary>
		public GameObject RedTowerPrefab;
		/// <summary>
		/// Red Monastery Prefab
		/// </summary>
		public GameObject RedMonasteryPrefab;
		/// <summary>
		/// Red Monk Prefab
		/// </summary>
		public GameObject RedMonkPrefab;

		/// <summary>
        /// Mine Prefab
        /// </summary>
		public GameObject MinePrefab;

		#endregion

		#region VFX & Sprites

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
		/// Heal effect animator controller for monk healing
		/// </summary>
		public RuntimeAnimatorController HealEffectAnimatorController;

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

		#endregion

		#region Procedural Map Tiles

		[Header("Procedural Map Tiles")]
		[Tooltip("Ground tile for procedural generation (e.g. FlatGround_color1 RuleTile)")]
		public TileBase GroundTile;

		[Tooltip("Tree tile variants (1–4) for procedural grove rendering")]
		public TileBase[] TreeTiles = new TileBase[0];

		[Tooltip("Optional water background tile for procedural maps")]
		public TileBase WaterTile;

		[Tooltip("Animated shore/foam tile placed under edge ground tiles (e.g. Water Tile animated)")]
		public TileBase WaterEffectTile;

		#endregion

		#region HUD UI

		[NonSerialized] public Text SpeedText;
		[NonSerialized] public Text TimerText;
		public GameObject GameOverUI;
		public GameObject Grid;

		[Header("Score Ribbons")]
		public Sprite BlueSmallRibbon;
		public Sprite BlueSmallRibbonInner;
		public Sprite RedSmallRibbon;
		public Sprite RedSmallRibbonInner;
		public Sprite BlackSmallRibbon;
		public Sprite BlackSmallRibbonInner;

		// Runtime-populated by InstantiateScoreboard
		[NonSerialized] public Text BlueLabelText;
		[NonSerialized] public Text BlueScoreText;
		[NonSerialized] public Text RedLabelText;
		[NonSerialized] public Text RedScoreText;

		#endregion

		#region Agent Debugging Panel

		[Header("Agent Debugging Panel")]
		public GameObject AgentDebuggingPanelPrefab;
		public Transform UnitInfoCanvas;
		public GameObject UnitDebuggerPrefab;

		[Header("Blue Debug Panel Icons")]
		public Sprite BluePawnIcon;
		public Sprite BlueWarriorIcon;
		public Sprite BlueArcherIcon;
		public Sprite BlueLancerIcon;
		public Sprite BlueCastleIcon;
		public Sprite BlueBarracksIcon;
		public Sprite BlueArcheryIcon;
		public Sprite BlueTowerIcon;
		public Sprite BlueMonasteryIcon;
		public Sprite BlueMonkIcon;

		[Header("Red Debug Panel Icons")]
		public Sprite RedPawnIcon;
		public Sprite RedWarriorIcon;
		public Sprite RedArcherIcon;
		public Sprite RedLancerIcon;
		public Sprite RedCastleIcon;
		public Sprite RedBarracksIcon;
		public Sprite RedArcheryIcon;
		public Sprite RedTowerIcon;
		public Sprite RedMonasteryIcon;
		public Sprite RedMonkIcon;

		/// <summary>
		/// Returns the icon sprite mapping for the given agent color.
		/// Keys match the Category Data Row names in the Agent Debugging Panel.
		/// </summary>
		public Dictionary<string, Sprite> GetIconsForAgent(string agentName)
		{
			bool isBlue = agentName == GameManager.Constants.BLUE_ABBR;
			return new Dictionary<string, Sprite>
			{
				["Pawns"]    = isBlue ? BluePawnIcon    : RedPawnIcon,
				["Warriors"] = isBlue ? BlueWarriorIcon : RedWarriorIcon,
				["Archers"]  = isBlue ? BlueArcherIcon  : RedArcherIcon,
				["Lancers"]  = isBlue ? BlueLancerIcon  : RedLancerIcon,
				["Castles"]  = isBlue ? BlueCastleIcon  : RedCastleIcon,
				["Barracks"] = isBlue ? BlueBarracksIcon: RedBarracksIcon,
				["Archeries"]= isBlue ? BlueArcheryIcon : RedArcheryIcon,
				["Towers"]   = isBlue ? BlueTowerIcon   : RedTowerIcon,
				["Monasteries"] = isBlue ? BlueMonasteryIcon : RedMonasteryIcon,
				["Monks"]    = isBlue ? BlueMonkIcon    : RedMonkIcon,
			};
		}

		#endregion
	}
}
