using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FortressClash.UI
{
    /// <summary>
    /// Phát tiếng <c>buttonclick</c> mỗi khi người chơi CHẠM vào button này.
    ///
    /// <see cref="AudioManager"/> tự gắn component này vào MỌI Button (button trong scene lẫn button
    /// trong dialog sinh lúc chạy - xem <c>AudioManager.HookButtonsInScene</c> /
    /// <c>AudioManager.HookButtonSounds</c>) nên KHÔNG phải thêm tay vào từng nút.
    ///
    /// Dùng <see cref="IPointerClickHandler"/> chứ KHÔNG dùng <c>Button.onClick</c>: nhiều chỗ trong
    /// game gọi <c>onClick.RemoveAllListeners()</c> để gán lại hành động (DlgResult.SetContent,
    /// UIManager.OnDestroy, MenuController.OnDisable...) - nếu bám vào onClick thì tiếng click sẽ bị
    /// xoá mất theo.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonClickSound : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            // Nút bị khoá (interactable = false) thì Button không chạy hành động, nên cũng không kêu.
            Button button = GetComponent<Button>();
            if (button != null && !button.interactable) return;

            AudioManager.PlayButtonClick();
        }
    }
}
