using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameManager.GameElements
{
	/// <summary>
	/// Controls the camera and user interface elements correlated to the camera
	/// </summary>
	public class CameraController : MonoBehaviour {

		const float dragMultiplier = 40f;
		const float scrollMultiplier = 500f;
		const float minZoom = 1f;
		const float maxZoom = 23f;

		bool isDraggingLeftBtn;
		Vector3 mousePositionOld;
		bool isDraggingRightBtn;

		private InputSystem_Actions _input;


		// Use this for initialization
		void Start () {
			isDraggingLeftBtn = false;
			isDraggingRightBtn = false;
			mousePositionOld = Vector3.zero;
		}

		void OnEnable()
		{
			_input = new InputSystem_Actions();
			_input.asset.bindingMask = null;
			_input.Camera.Enable();
		}

		void OnDisable()
		{
			_input?.Camera.Disable();
			_input?.Dispose();
			_input = null;
		}

		// Update is called once per frame
		void Update () {
			if (_input == null) return;

			var mouse = _input.Camera;
			bool leftPressed = mouse.LeftClick.IsPressed();
			bool leftDown = mouse.LeftClick.WasPressedThisFrame();
			bool leftUp = mouse.LeftClick.WasReleasedThisFrame();
			bool rightPressed = mouse.RightClick.IsPressed();
			bool rightDown = mouse.RightClick.WasPressedThisFrame();
			bool rightUp = mouse.RightClick.WasReleasedThisFrame();
			Vector2 mousePos = mouse.MousePosition.ReadValue<Vector2>();
			Vector2 scroll = mouse.Scroll.ReadValue<Vector2>();

			// Handle Click & Drag behavior using left-button
			if (!rightPressed && leftDown && !isDraggingLeftBtn)
			{
				mousePositionOld = mousePos;
				isDraggingLeftBtn = true;
			}
			if (leftPressed && isDraggingLeftBtn)
			{
				Vector3 delta = new Vector3((mousePos.x - mousePositionOld.x) / Screen.width,
					(mousePos.y - mousePositionOld.y) / Screen.height,
					0);
				transform.position -= delta * dragMultiplier;

				mousePositionOld = mousePos;
			}
			if (leftUp && isDraggingLeftBtn)
			{
				isDraggingLeftBtn = false;
			}

			// Handle Zoom Behavior
			float scrollY = scroll.y;
			if (Math.Abs(scrollY) > .00001f)
			{
				var cam = gameObject.GetComponent<Camera>();
				// Normalize scroll: old Input.GetAxis("Mouse ScrollWheel") was ~0.1 per notch,
				// new Input System scroll.y is 120 per notch
				float normalizedScroll = scrollY / 120f * 0.1f;
				cam.orthographicSize = Math.Min(maxZoom, Math.Max(minZoom, cam.orthographicSize + -normalizedScroll * scrollMultiplier));
			}

			// Handle Zoom Behavior using right-button
			if (!leftPressed && rightDown && !isDraggingRightBtn)
			{
				mousePositionOld = mousePos;
				isDraggingRightBtn = true;
			}
			if (rightPressed && isDraggingRightBtn)
			{
				Vector3 delta = new Vector3((mousePos.x - mousePositionOld.x) / Screen.width,
					(mousePos.y - mousePositionOld.y) / Screen.height,
					0);
				var cam = gameObject.GetComponent<Camera>();
				cam.orthographicSize = Math.Min(maxZoom, Math.Max(minZoom, cam.orthographicSize - delta.y * dragMultiplier));

				mousePositionOld = mousePos;
			}
			if (rightUp && isDraggingRightBtn)
			{
				isDraggingRightBtn = false;
			}
		}
	}
}
