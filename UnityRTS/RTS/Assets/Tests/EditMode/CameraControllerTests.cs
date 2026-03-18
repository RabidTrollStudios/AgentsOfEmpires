using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using GameManager.GameElements;

namespace GameManager.Tests
{
	[TestFixture]
	public class CameraControllerTests
	{
		private const BindingFlags NonPublic =
			BindingFlags.NonPublic | BindingFlags.Instance;

		private GameObject cameraGo;
		private CameraController controller;
		private Camera cam;

		[SetUp]
		public void SetUp()
		{
			cameraGo = new GameObject("TestCamera");
			cam = cameraGo.AddComponent<Camera>();
			cam.orthographic = true;
			cam.orthographicSize = 10f;
			controller = cameraGo.AddComponent<CameraController>();
		}

		[TearDown]
		public void TearDown()
		{
			Object.DestroyImmediate(cameraGo);
		}

		private void InvokeStart() =>
			typeof(CameraController).GetMethod("Start", NonPublic)
				.Invoke(controller, null);

		private void InvokeUpdate() =>
			typeof(CameraController).GetMethod("Update", NonPublic)
				.Invoke(controller, null);

		private T GetField<T>(string name) =>
			(T)typeof(CameraController).GetField(name, NonPublic).GetValue(controller);

		private void SetField(string name, object value) =>
			typeof(CameraController).GetField(name, NonPublic).SetValue(controller, value);

		#region Start()

		[Test]
		public void Start_InitializesDraggingFlags()
		{
			InvokeStart();

			Assert.IsFalse(GetField<bool>("isDraggingLeftBtn"));
			Assert.IsFalse(GetField<bool>("isDraggingRightBtn"));
			Assert.AreEqual(Vector3.zero, GetField<Vector3>("mousePositionOld"));
		}

		#endregion

		#region Update — no input (all branches skip)

		[Test]
		public void Update_NoInput_DoesNotThrow()
		{
			InvokeStart();
			Assert.DoesNotThrow(() => InvokeUpdate());
		}

		[Test]
		public void Update_NoInput_DraggingFlagsRemainFalse()
		{
			InvokeStart();
			InvokeUpdate();

			Assert.IsFalse(GetField<bool>("isDraggingLeftBtn"));
			Assert.IsFalse(GetField<bool>("isDraggingRightBtn"));
		}

		[Test]
		public void Update_NoInput_CameraPositionUnchanged()
		{
			InvokeStart();
			var posBefore = cameraGo.transform.position;
			InvokeUpdate();
			Assert.AreEqual(posBefore, cameraGo.transform.position);
		}

		[Test]
		public void Update_NoInput_ZoomUnchanged()
		{
			InvokeStart();
			var sizeBefore = cam.orthographicSize;
			InvokeUpdate();
			Assert.AreEqual(sizeBefore, cam.orthographicSize);
		}

		#endregion

		#region Left-button drag state (via reflection)

		[Test]
		public void Update_LeftDragActive_NoInput_DragFlagPersists()
		{
			// Simulate a left drag already in progress — without actual mouse input
			// the continue block (GetMouseButton(0)) won't fire, but isDragging stays true
			// because the release check (GetMouseButtonUp(0)) is also false.
			InvokeStart();
			SetField("isDraggingLeftBtn", true);

			InvokeUpdate();

			Assert.IsTrue(GetField<bool>("isDraggingLeftBtn"),
				"isDraggingLeftBtn should persist when no mouse-up event fires");
		}

		#endregion

		#region Right-button drag state (via reflection)

		[Test]
		public void Update_RightDragActive_NoInput_DragFlagPersists()
		{
			InvokeStart();
			SetField("isDraggingRightBtn", true);

			InvokeUpdate();

			Assert.IsTrue(GetField<bool>("isDraggingRightBtn"),
				"isDraggingRightBtn should persist when no mouse-up event fires");
		}

		#endregion

		#region Camera component

		[Test]
		public void CameraController_RequiresCamera()
		{
			Assert.IsNotNull(cam, "Test camera should exist on the same GameObject");
			Assert.IsTrue(cam.orthographic, "Camera should be orthographic for zoom tests");
		}

		#endregion
	}
}
