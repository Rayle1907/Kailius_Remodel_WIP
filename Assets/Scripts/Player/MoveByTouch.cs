using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MoveByTouch : MonoBehaviour {

    public float moveSpeed;
    public float jumpHeight;

    public GameObject row;
    public GameObject menu;
    public GameObject stats;
    public GameObject ControlesMoviles;

    public Joystick joystick;
    public ParticleSystem dust;

    private PlayerController playerController;

    private void Awake() {
        playerController = GetComponent<PlayerController>();
    }

    public float HorizontalInput => joystick != null ? joystick.Horizontal : 0f;


    // Start is called before the first frame update
    void Start() {
        gameObject.AddComponent<InGameSettings>().Initialize(this);
    }

    public void saltar() {
        if (ControlesMoviles != null && ControlesMoviles.activeSelf && playerController.TryJump(jumpHeight))
            CreateDust();
    }

    public void settings() {
        GetComponent<InGameSettings>().Toggle();
    }

    private void OnTriggerEnter2D(Collider2D collision) {
        if (ControlesMoviles.activeSelf) {
            if (collision.gameObject.CompareTag("Chest")) {
                collision.gameObject.GetComponent<Chest>().openChest();
            }
        }
    }

    private void OnTriggerStay2D(Collider2D collision) {
        if (ControlesMoviles.activeSelf) {
            if (collision.gameObject.CompareTag("Instructor")) {
                collision.gameObject.GetComponent<DialogueManager>().DialogueMobile();
            }
        }
    }

    void CreateDust() {
        dust.Play();
    }
}
