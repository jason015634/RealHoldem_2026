using UnityEngine;

public sealed class CardView : MonoBehaviour
{
    public RectTransform RectTransform => (RectTransform)transform;

    private void Awake()
    {
        HideAnchorVisual();
    }

    private void OnEnable()
    {
        HideAnchorVisual();
    }

    private void HideAnchorVisual()
    {
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = gameObject.AddComponent<CanvasGroup>();
        }

        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
    }
}
