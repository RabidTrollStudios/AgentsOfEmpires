using System;
using UnityEngine;

namespace GameManager.GameElements
{
	/// <summary>
	/// Controls the camera and user interface elements correlated to the camera
	/// </summary>
	public class CameraController : MonoBehaviour {

		const float dragMultiplier = 40f;
		const float scrollMultiplier = 10f;
		const float minZoom = 1f;
		const float maxZoom = 23f;

		bool isDraggingLeftBtn;
		Vector3 mousePositionOld;
		bool isDraggingRightBtn;


		// Use this for initialization
		void Start () {
			isDraggingLeftBtn = false;
			isDraggingRightBtn = false;
			mousePositionOld = Vector3.zero;

			// var cam = GetComponent<Camera>();
			// if (cam != null)
			// 	cam.orthographicSize = 23f;
		}

		// Update is called once per frame
		void Update () {

			// Handle Click & Drag behavior using left-button
			if (!Input.GetMouseButton(1) && Input.GetMouseButtonDown(0) && !isDraggingLeftBtn)
			{
				mousePositionOld = Input.mousePosition;
				isDraggingLeftBtn = true;
			}
			if (Input.GetMouseButton(0) && isDraggingLeftBtn)
			{
				Vector3 delta = new Vector3((Input.mousePosition.x - mousePositionOld.x) / Screen.width,
					(Input.mousePosition.y - mousePositionOld.y) / Screen.height,
					0);
				transform.position -= delta * dragMultiplier;

				mousePositionOld = Input.mousePosition;
			}
			if (Input.GetMouseButtonUp(0) && isDraggingLeftBtn)
			{
				isDraggingLeftBtn = false;
			}

			// Handle Zoom Behavior
			if (Math.Abs(Input.GetAxis("Mouse ScrollWheel")) > .00001f)
			{
				var cam = gameObject.GetComponent<Camera>();
				cam.orthographicSize = Math.Min(maxZoom, Math.Max(minZoom, cam.orthographicSize + -Input.GetAxis("Mouse ScrollWheel") * scrollMultiplier));
			}

			// Handle Zoom Behavior using right-button
			if (!Input.GetMouseButton(0) && Input.GetMouseButtonDown(1) && !isDraggingRightBtn)
			{
				mousePositionOld = Input.mousePosition;
				isDraggingRightBtn = true;
			}
			if (Input.GetMouseButton(1) && isDraggingRightBtn)
			{
				Vector3 delta = new Vector3((Input.mousePosition.x - mousePositionOld.x) / Screen.width,
					(Input.mousePosition.y - mousePositionOld.y) / Screen.height,
					0);
				var cam = gameObject.GetComponent<Camera>();
				cam.orthographicSize = Math.Min(maxZoom, Math.Max(minZoom, cam.orthographicSize - delta.y * dragMultiplier));

				mousePositionOld = Input.mousePosition;
			}
			if (Input.GetMouseButtonUp(1) && isDraggingRightBtn)
			{
				isDraggingRightBtn = false;
			}
		}
	}
}
