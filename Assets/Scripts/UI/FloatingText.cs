using UnityEngine;
using TMPro;
using DG.Tweening;

public class FloatingText : MonoBehaviour
{
    public TextMeshProUGUI displayText;

    public void Init(int amount, Vector3 worldPosition)
    {
        if (displayText == null) displayText = GetComponent<TextMeshProUGUI>();

        displayText.text = $"+{amount}";
        displayText.color = Color.red;

        Canvas mainCanvas = FindAnyObjectByType<Canvas>();
        if (mainCanvas != null && transform.parent != mainCanvas.transform)
        {
            transform.SetParent(mainCanvas.transform, false);

            Vector2 screenPoint = Camera.main.WorldToScreenPoint(worldPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                mainCanvas.transform as RectTransform,
                screenPoint,
                mainCanvas.worldCamera,
                out Vector2 localPoint
            );
            transform.localPosition = localPoint;
        }

        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOLocalMoveY(transform.localPosition.y + 80f, 0.8f).SetEase(Ease.OutQuad));
        seq.Join(displayText.DOFade(0f, 0.8f).SetEase(Ease.InQuad));
        seq.OnComplete(() => {
            Destroy(gameObject);
        });
    }
}