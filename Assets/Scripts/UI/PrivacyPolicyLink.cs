using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(TextMeshProUGUI))]
public sealed class PrivacyPolicyLink : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private string policyUrl;
    private TextMeshProUGUI explanation;

    private void Awake()
    {
        explanation = GetComponent<TextMeshProUGUI>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left ||
            eventData.dragging || string.IsNullOrWhiteSpace(policyUrl))
            return;

        int index = TMP_TextUtilities.FindIntersectingLink(
            explanation, eventData.position, eventData.pressEventCamera);

        if (index >= 0 && explanation.textInfo.linkInfo[index].GetLinkID() == "privacy-policy")
            Application.OpenURL(policyUrl);
    }
}
