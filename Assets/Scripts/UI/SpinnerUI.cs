using UnityEngine;

namespace FortressClash.UI
{
    /// <summary>
    /// Con quay "đang tìm trận" - gắn vào ảnh tròn nằm trong panel tìm trận (scene MultiPlayerConnect,
    /// object <c>Spinner</c>).
    ///
    /// Component này chỉ làm 1 việc: QUAY chính RectTransform của nó. Việc bật/tắt do
    /// <c>NetworkUI</c> quyết định (tắt khi đã báo lỗi, không còn tìm gì nữa) nên ở đây không có logic
    /// trạng thái nào.
    /// </summary>
    public class SpinnerUI : MonoBehaviour
    {
        [Tooltip("Tốc độ quay, độ/giây. Số âm = quay theo chiều kim đồng hồ.")]
        [SerializeField] private float degreesPerSecond = -180f;

        private void Update()
        {
            // unscaledDeltaTime: game có bị đóng băng (Time.timeScale = 0) thì con quay vẫn quay đều.
            transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
        }
    }
}
