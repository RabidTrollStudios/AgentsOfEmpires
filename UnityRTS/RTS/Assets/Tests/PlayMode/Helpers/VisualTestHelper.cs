using Preloader;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GameManager.Tests.PlayMode
{
	/// <summary>
	/// Loads real visual assets (sprites, animator controllers) from the project
	/// and injects them into the PrefabLoader so visual code paths execute in tests.
	/// PlayMode tests run inside the Unity Editor, so AssetDatabase is available.
	/// </summary>
	internal static class VisualTestHelper
	{
		// Asset paths (relative to project root)
		private const string ARROW_SPRITE_PATH =
			"Assets/Tiny Swords/Units/Extra/Arrow/Arrow.png";
		private const string GOLD_SPRITE_PATH =
			"Assets/Tiny Swords/Pawn and Resources/Gold/Gold Resource/Gold_Resource.png";
		private const string DUST2_CONTROLLER_PATH =
			"Assets/Tiny Swords/Particle FX/Dust 2 Animation/Dust 2.controller";
		private const string FIRE1_CONTROLLER_PATH =
			"Assets/Tiny Swords/Particle FX/Fire 1 Animation/Fire 1.controller";
		private const string FIRE2_CONTROLLER_PATH =
			"Assets/Tiny Swords/Particle FX/Fire 2 Animation/Fire 2.controller";
		private const string FIRE3_CONTROLLER_PATH =
			"Assets/Tiny Swords/Particle FX/FIre 3 Animation/Fire 3.controller";
		private const string EXPLOSION_CONTROLLER_PATH =
			"Assets/Tiny Swords/Particle FX/Explosion 2 Animation/Explosion 2.controller";
		private const string LANCER_BLUE_CONTROLLER_PATH =
			"Assets/Tiny Swords/Units/Blue Units/Lancer/Lancer Blue Animations/Lancer_Blue.controller";
		private const string WARRIOR_BLUE_CONTROLLER_PATH =
			"Assets/Tiny Swords/Units/Blue Units/Warrior/Warrior Blue Animations/Warrior_Blue.controller";
		private const string ARCHER_BLUE_CONTROLLER_PATH =
			"Assets/Tiny Swords/Units/Blue Units/Archer/Archer Blue Animations/Archer_Blue.controller";
		private const string PAWN_BLUE_CONTROLLER_PATH =
			"Assets/Tiny Swords/Pawn and Resources/Pawn/Blue Pawn/Pawn Blue Animations/Pawn_Blue.controller";
		private const string MINE_CONTROLLER_PATH =
			"Assets/Animators/Mine Animator.controller";

		/// <summary>
		/// Inject all visual assets into the PrefabLoader on GameManager.Instance.
		/// Call this at the start of any test that exercises visual code paths.
		/// </summary>
		internal static void InjectVisualAssets()
		{
#if UNITY_EDITOR
			var prefabs = GetPrefabLoader();
			if (prefabs == null) return;

			prefabs.ArrowSprite = LoadAsset<Sprite>(ARROW_SPRITE_PATH);
			prefabs.GoldResourceSprite = LoadAsset<Sprite>(GOLD_SPRITE_PATH);
			prefabs.Dust2AnimatorController = LoadAsset<RuntimeAnimatorController>(DUST2_CONTROLLER_PATH);
			prefabs.Fire1AnimatorController = LoadAsset<RuntimeAnimatorController>(FIRE1_CONTROLLER_PATH);
			prefabs.Fire2AnimatorController = LoadAsset<RuntimeAnimatorController>(FIRE2_CONTROLLER_PATH);
			prefabs.Fire3AnimatorController = LoadAsset<RuntimeAnimatorController>(FIRE3_CONTROLLER_PATH);
			prefabs.ExplosionAnimatorController = LoadAsset<RuntimeAnimatorController>(EXPLOSION_CONTROLLER_PATH);
			prefabs.FireAnimatorController = LoadAsset<RuntimeAnimatorController>(FIRE1_CONTROLLER_PATH);
#endif
		}

		/// <summary>
		/// Assign a real animator controller to a unit's Animator component.
		/// Useful for testing animation-dependent code (lancer hashes, warrior attack toggle).
		/// </summary>
		internal static void AssignAnimatorController(
			GameElements.Unit unit, string controllerPath)
		{
#if UNITY_EDITOR
			var animator = unit.GetComponent<Animator>();
			if (animator == null) return;
			var controller = LoadAsset<RuntimeAnimatorController>(controllerPath);
			if (controller != null)
				animator.runtimeAnimatorController = controller;
#endif
		}

		/// <summary>Assign the Blue Lancer animator controller and initialize state hashes.</summary>
		internal static void SetupLancerAnimator(GameElements.Unit lancer)
		{
			AssignAnimatorController(lancer, LANCER_BLUE_CONTROLLER_PATH);
			lancer.InitLancerStateHashes();
		}

		/// <summary>Assign the Blue Warrior animator controller.</summary>
		internal static void SetupWarriorAnimator(GameElements.Unit warrior)
		{
			AssignAnimatorController(warrior, WARRIOR_BLUE_CONTROLLER_PATH);
		}

		/// <summary>Assign the Blue Archer animator controller.</summary>
		internal static void SetupArcherAnimator(GameElements.Unit archer)
		{
			AssignAnimatorController(archer, ARCHER_BLUE_CONTROLLER_PATH);
		}

		/// <summary>Assign the Blue Pawn animator controller.</summary>
		internal static void SetupPawnAnimator(GameElements.Unit pawn)
		{
			AssignAnimatorController(pawn, PAWN_BLUE_CONTROLLER_PATH);
		}

		/// <summary>Assign the Mine animator controller.</summary>
		internal static void SetupMineAnimator(GameElements.Unit mine)
		{
			AssignAnimatorController(mine, MINE_CONTROLLER_PATH);
		}

		internal static string LancerControllerPath => LANCER_BLUE_CONTROLLER_PATH;
		internal static string WarriorControllerPath => WARRIOR_BLUE_CONTROLLER_PATH;
		internal static string ArcherControllerPath => ARCHER_BLUE_CONTROLLER_PATH;
		internal static string PawnControllerPath => PAWN_BLUE_CONTROLLER_PATH;
		internal static string MineControllerPath => MINE_CONTROLLER_PATH;

#if UNITY_EDITOR
		private static PrefabLoader GetPrefabLoader()
		{
			if (GameManager.Instance == null) return null;
			var field = typeof(GameManager).GetField("Prefabs",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			return field?.GetValue(GameManager.Instance) as PrefabLoader;
		}

		private static T LoadAsset<T>(string path) where T : Object
		{
			var asset = AssetDatabase.LoadAssetAtPath<T>(path);
			if (asset == null)
				Debug.LogWarning($"VisualTestHelper: Could not load asset at '{path}'");
			return asset;
		}
#endif
	}
}
