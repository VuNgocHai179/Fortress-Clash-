using UnityEngine;

namespace FortressClash.Core
{
    /// <summary>
    /// Tiến trình chơi theo level: level nào đã được mở khóa và đang chơi level nào.
    ///
    /// Quy tắc: luôn mở sẵn level 1, các level sau CHỈ được mở khi người chơi THẮNG level liền trước
    /// (xem <see cref="CompleteLevel"/>, được gọi từ GameManager khi King đỏ chết).
    ///
    /// Lưu bằng PlayerPrefs nên tiến trình giữ nguyên khi tắt game.
    /// </summary>
    public static class LevelProgress
    {
        /// <summary>Số level hiện có trên màn chọn level (prefab LevelScene đang có 3 nút level1/2/3).</summary>
        public const int LevelCount = 3;

        /// <summary>Level đầu tiên - luôn được mở khóa.</summary>
        public const int FirstLevel = 1;

        private const string UnlockedKey = "FortressClash.UnlockedLevel";
        private const string SelectedKey = "FortressClash.SelectedLevel";

        /// <summary>Level cao nhất đã mở khóa. Level 1 luôn mở sẵn.</summary>
        public static int UnlockedLevel
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(UnlockedKey, FirstLevel), FirstLevel, LevelCount); }
        }

        /// <summary>
        /// Level đang được chơi trong scene Gameplay. Màn chọn level ghi vào đây trước khi load scene;
        /// scene Gameplay đọc ra để biết áp độ khó nào và thắng thì mở khóa level nào.
        /// </summary>
        public static int SelectedLevel
        {
            get { return Mathf.Clamp(PlayerPrefs.GetInt(SelectedKey, FirstLevel), FirstLevel, LevelCount); }
            set
            {
                PlayerPrefs.SetInt(SelectedKey, Mathf.Clamp(value, FirstLevel, LevelCount));
                PlayerPrefs.Save();
            }
        }

        /// <summary>Level đã được mở khóa chưa (level 1 luôn mở).</summary>
        public static bool IsUnlocked(int level)
        {
            return level >= FirstLevel && level <= UnlockedLevel;
        }

        /// <summary>
        /// Ghi nhận vừa THẮNG <paramref name="level"/>: mở khóa level kế tiếp.
        /// Trả về level vừa được mở (0 nếu không có gì mới - đã mở trước đó, hoặc đã là level cuối).
        /// </summary>
        public static int CompleteLevel(int level)
        {
            int next = level + 1;
            if (next > LevelCount || next <= UnlockedLevel) return 0;

            PlayerPrefs.SetInt(UnlockedKey, next);
            PlayerPrefs.Save();
            return next;
        }

        /// <summary>Xóa tiến trình, chỉ còn level 1 (dùng khi test / reset save).</summary>
        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(UnlockedKey);
            PlayerPrefs.DeleteKey(SelectedKey);
            PlayerPrefs.Save();
        }
    }
}
