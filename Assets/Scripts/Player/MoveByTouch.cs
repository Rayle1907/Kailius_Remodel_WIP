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

    private SpikeRecovery spikeRecovery;

    private void Awake() {
        spikeRecovery = GetComponent<SpikeRecovery>();
    }

    private bool RecoveryBlocksMovement {
        get {
            if (spikeRecovery == null) spikeRecovery = GetComponent<SpikeRecovery>();
            return spikeRecovery != null && spikeRecovery.IsMovementLocked;
        }
    }

    private bool canJump;
    private bool canDoubleJump;
    public float HorizontalInput => joystick != null ? joystick.Horizontal : 0f;


    // Start is called before the first frame update
    void Start() {
        gameObject.AddComponent<InGameSettings>().Initialize(this);
    }

    // Update is called once per frame
    void Update() {
        if (Time.timeScale <= 0f || RecoveryBlocksMovement) return;
        if (ControlesMoviles != null && ControlesMoviles.activeSelf) {
            canJump = GetComponentInChildren<PlayerControllerUP>().getJump();
            canDoubleJump = GetComponentInChildren<PlayerControllerUP>().getDoubleJump();
        }
    }

    public void saltar() {
        if (Time.timeScale <= 0f || RecoveryBlocksMovement) return;
        if (ControlesMoviles.activeSelf) {
            if (canJump) {
                CreateDust();
                gameObject.GetComponent<Rigidbody2D>().linearVelocity = new Vector2(GetComponent<Rigidbody2D>().linearVelocity.x, jumpHeight);
                GetComponentInChildren<PlayerControllerUP>().setJump(false);
            } else if (canDoubleJump) {
                CreateDust();
                gameObject.GetComponent<Rigidbody2D>().linearVelocity = new Vector2(GetComponent<Rigidbody2D>().linearVelocity.x, jumpHeight);
                GetComponentInChildren<PlayerControllerUP>().setDoubleJump(false);
                canDoubleJump = false;
            }
        }
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
