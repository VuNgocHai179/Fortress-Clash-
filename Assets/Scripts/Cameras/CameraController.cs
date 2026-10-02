using UnityEngine;

namespace FortressClash.Cameras
{
    public class CameraController : MonoBehaviour
    {
        [Header("Camera Settings")]
        [SerializeField] private float panSpeed = 15f;

        [Header("Map Boundaries")]
        [SerializeField] private float baseMinX = -28.6f;
        [SerializeField] private float baseMaxX = 26.8f;

        private float minX;
        private float maxX;
        private Camera cam;
        private Vector3 dragOrigin;

        void Start()
        {
            cam = Camera.main;
            UpdateBoundaries();
        }

        void Update()
        {
            UpdateBoundaries();
            HandleCameraPan();
        }

        private void UpdateBoundaries()
        {
            if (cam == null) return;

            float currentAspect = (float)Screen.width / Screen.height;
            float baseAspect = 16f / 9f;

            float aspectDiff = baseAspect - currentAspect;

            minX = baseMinX - (aspectDiff * cam.orthographicSize * 2f);
            maxX = baseMaxX + (aspectDiff * cam.orthographicSize * 2f);
        }

        private void HandleCameraPan()
        {
            if (Input.GetMouseButtonDown(0))
            {
                dragOrigin = cam.ScreenToWorldPoint(Input.mousePosition);
            }

            if (Input.GetMouseButton(0))
            {
                Vector3 difference = dragOrigin - cam.ScreenToWorldPoint(Input.mousePosition);
                Vector3 targetPos = transform.position + new Vector3(difference.x, 0, 0);

                targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
                transform.position = targetPos;
            }

            float scroll = Input.GetAxis("Horizontal");
            if (Mathf.Abs(scroll) > 0)
            {
                Vector3 targetPos = transform.position + new Vector3(scroll * panSpeed * Time.deltaTime, 0, 0);

                targetPos.x = Mathf.Clamp(targetPos.x, minX, maxX);
                transform.position = targetPos;
            }
        }
    }
}