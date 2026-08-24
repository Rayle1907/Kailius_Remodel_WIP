using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bow : MonoBehaviour {

    public Transform bow;
    public Animator animator;
    public GameObject arrowPrefab;
    public GameObject sonido;

    // Kept temporarily because this component is serialized in existing scenes.
    // The bow mechanic has been removed, so animation events must do nothing.
    void ShootArrow() {
    }

    public void AttackButton() {
    }
}
