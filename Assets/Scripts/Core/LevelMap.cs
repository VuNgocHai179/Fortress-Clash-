using UnityEngine;

namespace FortressClash.Core
{
    /// <summary>
    /// Đổi ẢNH NỀN SÂN ĐẤU theo level đang chơi.
    ///
    /// Level đang chơi nằm ở <see cref="LevelProgress.SelectedLevel"/> - màn chọn level ghi vào PlayerPrefs
    /// TRƯỚC khi load scene Gameplay, nên tới đây chỉ cần đọc ra là biết phải dùng nền nào:
    ///   - <c>maps[0]</c> là nền của level 1, <c>maps[1]</c> là level 2, cứ thế tiếp tục.
    ///   - Level lớn hơn số ảnh đã gán (hoặc ô nào bỏ trống) thì dùng ảnh CUỐI CÙNG, không bao giờ để nền trắng.
    ///   - Chưa gán ảnh nào cả thì giữ nguyên sprite có sẵn trong scene, game chạy bình thường.
    ///
    /// CÁCH DÙNG: gắn component này lên chính GameObject "Background" trong scene Gameplay rồi kéo
    /// Map1 -> Map2 -> Map3 vào ô Maps theo đúng thứ tự level.
    ///
    /// Scene MultiPlayer KHÔNG gắn component này: đấu mạng là hai người đánh nhau ngay, không có khái niệm
    /// level nên nền giữ nguyên như trong scene.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public class LevelMap : MonoBehaviour
    {
        [Tooltip("Ảnh nền theo từng level: phần tử 0 = level 1, phần tử 1 = level 2... " +
                 "Kéo Map1, Map2, Map3 vào đây theo đúng thứ tự.")]
        [SerializeField] private Sprite[] maps;

        /// <summary>Ảnh nền đang được dùng (null nếu chưa gán gì).</summary>
        public Sprite CurrentMap => GetMapFor(LevelProgress.SelectedLevel);

        // Awake (không phải Start) để nền đúng ngay từ frame đầu, không chớp ảnh của level trước.
        private void Awake()
        {
            Apply(LevelProgress.SelectedLevel);
        }

        /// <summary>Đổi nền sang ảnh của <paramref name="level"/> (1 = phần tử đầu tiên trong Maps).</summary>
        public void Apply(int level)
        {
            Sprite sprite = GetMapFor(level);
            if (sprite == null) return;   // chưa gán gì -> giữ nguyên nền đang có trong scene

            SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null) spriteRenderer.sprite = sprite;
        }

        /// <summary>
        /// Ảnh nền của 1 level. Trả về null khi ô Maps còn trống hoàn toàn - lúc đó bên gọi phải giữ
        /// nguyên nền cũ thay vì xoá trắng.
        /// </summary>
        private Sprite GetMapFor(int level)
        {
            if (maps == null || maps.Length == 0) return null;

            // Level 1 ứng với maps[0]; level vượt quá số ảnh thì kẹp về ảnh cuối cùng.
            int index = Mathf.Clamp(level - LevelProgress.FirstLevel, 0, maps.Length - 1);

            // Ô tương ứng bị bỏ trống thì lấy ảnh cuối cùng đã gán, tránh nền trắng giữa trận.
            return maps[index] != null ? maps[index] : maps[maps.Length - 1];
        }
    }
}
