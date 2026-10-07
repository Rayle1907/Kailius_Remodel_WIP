using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Canvas))]
public sealed class MobileSafeArea : MonoBehaviour
{
    private RectTransform container;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    public RectTransform ContentRoot => container;

    private void OnEnable()
    {
        if (container == null)
        {
            GameObject area = new GameObject("Mobile Safe Area", typeof(RectTransform));
            container = (RectTransform)area.transform;
            container.SetParent(transform, false);
            container.localScale = Vector3.one;

            var fullScreenChildren = new List<Transform>();
            var safeAreaChildren = new List<Transform>();
            for (int index = 0; index < transform.childCount - 1; ++index)
            {
                Transform child = transform.GetChild(index);
                if (!(child is RectTransform))
                {
                    continue;
                }

                if (child.GetComponent<MobileSafeAreaFullScreen>() != null)
                {
                    fullScreenChildren.Add(child);
                }
                else
                {
                    safeAreaChildren.Add(child);
                }
            }

            foreach (Transform child in safeAreaChildren)
            {
                child.SetParent(container, false);
            }

            for (int index = 0; index < fullScreenChildren.Count; ++index)
            {
                fullScreenChildren[index].SetSiblingIndex(index);
            }

            container.SetAsLastSibling();
        }

        ApplySafeArea();
    }

    private void Update()
    {
        if (lastSafeArea != Screen.safeArea || lastScreenSize.x != Screen.width ||
            lastScreenSize.y != Screen.height)
        {
            ApplySafeArea();
        }
    }

    private void ApplySafeArea()
    {
        if (container == null || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        lastSafeArea = Screen.safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        container.anchorMin = new Vector2(lastSafeArea.xMin / Screen.width, lastSafeArea.yMin / Screen.height);
        container.anchorMax = new Vector2(lastSafeArea.xMax / Screen.width, lastSafeArea.yMax / Screen.height);
        container.offsetMin = Vector2.zero;
        container.offsetMax = Vector2.zero;
    }
}
