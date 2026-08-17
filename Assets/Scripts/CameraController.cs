using UnityEngine;

namespace FortressClash.Cameras
{
    public class CameraController : MonoBehaviour
    {
        [Header("Camera Settings")]
        private float panSpeed = 15f;

        [Header("Map Boundaries")]
        private float minX = -24.5f;
        private float maxX = 22.9f;

        private Vector3 dragOrigin;

        void Update()
        {
            HandleCameraPan();
        }

        private void HandleCameraPan()
        {
            // 1. Handle movement via Drag & Drop (Mouse/Touch)
            if (Input.GetMouseButtonDown(0))
            {
                // Save the mouse position when the user first clicks
                dragOrigin = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            }

            if (Input.GetMouseButton(0))
            {
                // Calculate the difference between the original and current mouse position
                Vector3 difference = dragOrigin - Camera.main.ScreenToWorldPoint(Input.mousePosition);
                Vector3 targetPos = transform.position + new Vector3(difference.x, 0, 0);

                // Clamp position to prevent the camera from going out of bounds
                targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
                transform.position = targetPos;
            }

            // 2. Handle movement via Keyboard (A/D or Arrow keys)
            float scroll = Input.GetAxis("Horizontal");
            if (Mathf.Abs(scroll) > 0)
            {
                Vector3 targetPos = transform.position + new Vector3(scroll * panSpeed * Time.deltaTime, 0, 0);

                // Clamp position to prevent the camera from going out of bounds
                targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
                transform.position = targetPos;
            }
        }
    }
}