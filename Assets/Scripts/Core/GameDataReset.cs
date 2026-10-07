using FortressClash.Cutscene;

namespace FortressClash.Core
{
    /// <summary>
    /// Xoá TOÀN BỘ dữ liệu game đã lưu trên máy, để chơi lại như máy mới.
    ///
    /// Hiện game ghi nhớ 3 thứ, cả ba đều nằm trong PlayerPrefs:
    ///  - tiến trình mở khoá / level đang chọn  -> <see cref="LevelProgress.ResetProgress"/>
    ///  - cờ "đã xem cốt truyện"                -> <see cref="CutsceneLauncher.ResetSeen"/>
    ///  - mức âm lượng chỉnh trong Settings     -> <see cref="AudioManager.ClearSavedVolumes"/>
    /// Sau này có thêm dữ liệu lưu mới ở đâu thì nhớ xoá nó ở ĐÂY, không thì nút "Reset Data" chỉ xoá
    /// được một nửa mà người chơi không biết vì sao.
    ///
    /// KHÔNG dùng <c>PlayerPrefs.DeleteAll()</c>: nó xoá luôn cả khoá của package/plugin khác
    /// (Netcode, ParrelSync, Analytics...) chứ không chỉ dữ liệu của game.
    /// </summary>
    public static class GameDataReset
    {
        /// <summary>Xoá hết dữ liệu game đã lưu (không đụng tới dữ liệu của package khác).</summary>
        public static void ClearAll()
        {
            LevelProgress.ResetProgress();
            CutsceneLauncher.ResetSeen();
            AudioManager.ClearSavedVolumes();   // mức âm lượng đã chỉnh trong Settings
        }

        /// <summary>
        /// Xoá hết dữ liệu rồi chơi lại cốt truyện từ đầu.
        ///
        /// Scene "Cutscene" tự chạy hết rồi load MainMenu (xem CutsceneBook.Finish), nên bên gọi không
        /// phải làm gì thêm: cả scene hiện tại (kèm dialog đang mở) bị huỷ khi scene mới load.
        /// </summary>
        public static void ResetAllAndReplayStory()
        {
            ClearAll();
            CutsceneLauncher.Play();
        }
    }
}
