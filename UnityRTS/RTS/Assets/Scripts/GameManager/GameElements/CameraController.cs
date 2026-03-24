using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameManager.GameElements
{
	/// <summary>
	/// Controls the camera and user interface elements correlated to the camera.
	/// Pan and zoom are clamped to the map bounds so the background is never visible.
	/// </summary>
	public class CameraController : MonoBehaviour {

		const float dragMultiplier = 40f;
		const float scrollMultiplier = 500f;
		const float minZoom = 1f;

		bool isDraggingLeftBtn;
		Vector3 mousePositionOld;
		bool isDraggingRightBtn;

		// Double-click detection
		private float lastClickTime;
		private Vector2 lastClickPos;
		private const float doubleClickTime = 0.3f;
		private const float doubleClickRadius = 10f; // max pixel drift between clicks
		private const float doubleClickZoom = 5f; // orthoSize = 5 → ~10 cells visible vertically

		private InputSystem_Actions _input;

		// Ground bounds — panning keeps the ground edge inside the empty UI area.
		private float groundMinX, groundMinY, groundMaxX, groundMaxY;
		// Water bounds — the viewport edge must never go past the water.
		private float waterMinX, waterMinY, waterMaxX, waterMaxY;
		private bool hasBounds;
		private float maxZoom = 23f;

		// UI insets in screen pixels.
		private float insetLeftPx, insetRightPx, insetTopPx, insetBottomPx;

		/// <summary>
		/// Configure camera bounds with two rectangles:
		/// - Ground bounds: panning keeps the ground inside the empty UI area.
		/// - Water bounds: the viewport never extends past the water (no black).
		/// UI insets (in screen pixels) define the area occupied by debug panels and ribbons.
		/// </summary>
		public void SetBounds(
			float gMinX, float gMinY, float gMaxX, float gMaxY,
			float wMinX, float wMinY, float wMaxX, float wMaxY,
			float leftPx = 0f, float rightPx = 0f, float topPx = 0f, float bottomPx = 0f)
		{
			groundMinX = gMinX; groundMinY = gMinY;
			groundMaxX = gMaxX; groundMaxY = gMaxY;
			waterMinX = wMinX; waterMinY = wMinY;
			waterMaxX = wMaxX; waterMaxY = wMaxY;
			insetLeftPx = leftPx;
			insetRightPx = rightPx;
			insetTopPx = topPx;
			insetBottomPx = bottomPx;
			hasBounds = true;

			// Max zoom = ortho size that fits the ground in the empty UI area
			var cam = GetComponent<Camera>();
			float emptyFracX = 1f - (leftPx + rightPx) / Screen.width;
			float emptyFracY = 1f - (topPx + bottomPx) / Screen.height;
			float fitW = ((gMaxX - gMinX) / Math.Max(emptyFracX, 0.1f)) / (2f * cam.aspect);
			float fitH = ((gMaxY - gMinY) / Math.Max(emptyFracY, 0.1f)) / 2f;
			maxZoom = Math.Max(fitW, fitH);

			ClampCamera();
		}

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

		void Update () {
			// Always enforce bounds, even before input is ready
			if (hasBounds)
				ClampCamera();

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

			// Handle double-click zoom
			if (!rightPressed && leftDown)
			{
				float timeSinceLast = Time.unscaledTime - lastClickTime;
				float dist = Vector2.Distance(mousePos, lastClickPos);

				if (timeSinceLast <= doubleClickTime && dist <= doubleClickRadius)
				{
					// Double-click detected — zoom to clicked world position
					var cam = GetComponent<Camera>();
					Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, 0f));
					cam.orthographicSize = Math.Min(maxZoom, Math.Max(minZoom, doubleClickZoom));
					transform.position = new Vector3(worldPos.x, worldPos.y, transform.position.z);
					lastClickTime = 0f; // reset so a third click doesn't re-trigger
					isDraggingLeftBtn = false;
				}
				else
				{
					lastClickTime = Time.unscaledTime;
					lastClickPos = mousePos;
				}
			}

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

			// Re-clamp after input changes so the camera never shows beyond bounds
			if (hasBounds)
				ClampCamera();
		}

		/// <summary>
		/// Clamp camera position so the viewport never extends beyond the map bounds.
		/// </summary>
		/// <summary>
		/// Clamp camera zoom and position with two constraints:
		/// 1. Pan constraint: the ground edge stays inside the empty UI area
		///    (viewport minus inset panels). Ground can scroll to fill that area.
		/// 2. Hard constraint: the viewport edge never goes past the water bounds
		///    (no black background visible).
		/// </summary>
		private void ClampCamera()
		{
			var cam = GetComponent<Camera>();

			cam.orthographicSize = Math.Min(cam.orthographicSize, maxZoom);
			float halfH = cam.orthographicSize;
			float halfW = halfH * cam.aspect;

			// Convert pixel insets to world-space at the current zoom level
			float wpxX = (halfW * 2f) / Screen.width;
			float wpxY = (halfH * 2f) / Screen.height;
			float leftW  = insetLeftPx  * wpxX;
			float rightW = insetRightPx * wpxX;
			float topW   = insetTopPx   * wpxY;
			float botW   = insetBottomPx * wpxY;

			float cx = transform.position.x;
			float cy = transform.position.y;

			// --- Pan constraint: ground fills the empty UI area ---
			// The "visible" region is the viewport minus the UI insets.
			// Left edge of visible = cam.x - halfW + leftW
			// We want: ground left edge <= visible left edge  AND  ground right edge >= visible right edge
			// i.e. groundMinX <= cx - halfW + leftW  =>  cx >= groundMinX + halfW - leftW
			//      groundMaxX >= cx + halfW - rightW  =>  cx <= groundMaxX - halfW + rightW
			float panMinX = groundMinX + halfW - leftW;
			float panMaxX = groundMaxX - halfW + rightW;
			float panMinY = groundMinY + halfH - botW;
			float panMaxY = groundMaxY - halfH + topW;

			if (panMinX <= panMaxX) { cx = Math.Max(cx, panMinX); cx = Math.Min(cx, panMaxX); }
			else cx = (groundMinX + groundMaxX) * 0.5f;

			if (panMinY <= panMaxY) { cy = Math.Max(cy, panMinY); cy = Math.Min(cy, panMaxY); }
			else cy = (groundMinY + groundMaxY) * 0.5f;

			// --- Hard constraint: viewport never past the water ---
			cx = Math.Max(cx, waterMinX + halfW);
			cx = Math.Min(cx, waterMaxX - halfW);
			cy = Math.Max(cy, waterMinY + halfH);
			cy = Math.Min(cy, waterMaxY - halfH);

			transform.position = new Vector3(cx, cy, transform.position.z);
		}
	}
}
