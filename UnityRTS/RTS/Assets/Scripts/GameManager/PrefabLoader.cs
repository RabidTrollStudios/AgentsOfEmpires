using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
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
        /// Human Player Prefab
        /// </summary>
		public GameObject HumanPlayerPrefab;
        /// <summary>
        /// Orc Player Prefab
        /// </summary>
		public GameObject OrcPlayerPrefab;

		/// <summary>
        /// Human Peasant Prefab
        /// </summary>
		public GameObject HumanPeasantPrefab;
        /// <summary>
        /// Human Footman Prefab
        /// </summary>
		public GameObject HumanFootmanPrefab;
        /// <summary>
        /// Human Archer Prefab
        /// </summary>
		public GameObject HumanArcherPrefab;
        /// <summary>
        /// Human Base Prefab
        /// </summary>
		public GameObject HumanBasePrefab;
        /// <summary>
        /// Human Barracks Prefab
        /// </summary>
		public GameObject HumanBarracksPrefab;
        /// <summary>
        /// Human Refinery Prefab
        /// </summary>
		public GameObject HumanRefineryPrefab;

		/// <summary>
        /// Orc Peon prefab
        /// </summary>
		public GameObject OrcPeonPrefab;
        /// <summary>
        /// Orc Grunt Prefab
        /// </summary>
		public GameObject OrcGruntPrefab;
        /// <summary>
        /// Orc Axethrower Prefab
        /// </summary>
		public GameObject OrcAxethrowerPrefab;
        /// <summary>
        /// Orc Base Prefab
        /// </summary>
		public GameObject OrcBasePrefab;
        /// <summary>
        /// Orc Barracks Prefab
        /// </summary>
		public GameObject OrcBarracksPrefab;
        /// <summary>
        /// Orc Refinery Prefab
        /// </summary>
		public GameObject OrcRefineryPrefab;

		/// <summary>
        /// Mine Prefab
        /// </summary>
		public GameObject MinePrefab;

		/// <summary>
        /// Speed Textbox
        /// </summary>
		public Text SpeedText;
        /// <summary>
        /// Timer Textbox
        /// </summary>
		public Text TimerText;
        /// <summary>
        /// Human label textbox
        /// </summary>
        public Text HumanLabelText;
        /// <summary>
        /// Human score textbox
        /// </summary>
        public Text HumanScoreText;
        /// <summary>
        /// Orc label textbox
        /// </summary>
        public Text OrcLabelText;
        /// <summary>
        /// Orc Score textbox
        /// </summary>
        public Text OrcScoreText;
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
