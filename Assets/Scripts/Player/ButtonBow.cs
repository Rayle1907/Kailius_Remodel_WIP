using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonBow : MonoBehaviour, IPointerDownHandler {
    public Bow player;

    private void Awake() {
        gameObject.SetActive(false);
    }

    public void OnPointerDown(PointerEventData eventData) {
    }
}
