using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image), typeof(CanvasGroup))]
public sealed class LayeredButton : Button
{
    [Serializable]
    private struct SpriteSet
    {
        public Sprite fill;
        public Sprite highlight;
        public Sprite lowlight;
    }

    [SerializeField] private Image fillImage;
    [SerializeField] private Image grassImage;
    [SerializeField] private Image highlightImage;
    [SerializeField] private Image lowlightImage;
    [SerializeField] private Image outlineImage;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private CanvasGroup visualGroup;
    [SerializeField] private SpriteSet normalSprites;
    [SerializeField] private SpriteSet pressedSprites;
    [SerializeField] private Color focusLabelColor = new Color(1f, 0.82f, 0.35f, 1f);
    [SerializeField, Range(0f, 1f)] private float disabledOpacity = 0.45f;

    public void SetLabel(string text)
    {
        label.text = text;
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        SetOverlaySorting(outlineImage, short.MaxValue - 1);
        SetOverlaySorting(grassImage, short.MaxValue);
    }

    private static void SetOverlaySorting(Image image, int sortingOrder)
    {
        if (image == null)
        {
            return;
        }

        Canvas overlayCanvas = image.GetComponent<Canvas>();
        if (overlayCanvas == null)
        {
            overlayCanvas = image.gameObject.AddComponent<Canvas>();
        }

        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = sortingOrder;
        image.transform.SetAsLastSibling();
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        SpriteSet sprites = state == SelectionState.Pressed ? pressedSprites : normalSprites;
        SetSprite(fillImage, sprites.fill);
        SetSprite(highlightImage, sprites.highlight);
        SetSprite(lowlightImage, sprites.lowlight);
        if (outlineImage != null)
        {
            outlineImage.color = Color.white;
        }
        if (label != null)
        {
            bool focused = state == SelectionState.Highlighted || state == SelectionState.Selected;
            label.color = focused ? focusLabelColor : Color.white;
        }
        if (visualGroup != null)
        {
            visualGroup.alpha = state == SelectionState.Disabled ? disabledOpacity : 1f;
        }
    }

    protected override void InstantClearState()
    {
        base.InstantClearState();
        DoStateTransition(currentSelectionState, true);
    }

    private static void SetSprite(Image image, Sprite sprite)
    {
        if (image == null)
        {
            return;
        }

        image.overrideSprite = null;
        image.sprite = sprite;
        image.color = Color.white;
    }
}
