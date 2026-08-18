using UnityEngine;

public class PlayerController : MonoBehaviour {

    public float moveSpeed;
    public float jumpHeight;
  
    public GameObject row;
    public GameObject menu;
    public GameObject stats;
    public GameObject controllerMobile;
    public int damagePatrols = 25;

    private bool canJump;
    private bool canDoubleJump;
    private bool rotacionA = false;
    private bool rotacionD = false;

    private bool dead = false;
    private float attactRate = 0.6f;
    private float nextAttactTime = 0.5f;
    private Transform currentRespawn;
    public ParticleSystem dust;

    private Rigidbody2D body;
    private Animator playerAnimator;
    private SpriteRenderer playerSprite;
    private PlayerControllerUP jumpController;
    private Stats playerStats;
    private Transform rowTransform;
    private bool isMoving;

    void Awake() {
        body = GetComponent<Rigidbody2D>();
        playerAnimator = GetComponent<Animator>();
        playerSprite = GetComponent<SpriteRenderer>();
        jumpController = GetComponentInChildren<PlayerControllerUP>();
        playerStats = GetComponentInChildren<Stats>();
        rowTransform = row.transform;
    }

    void Start() {
        currentRespawn = FindClosestRespawn();
    }

    void Update() {

        if(!dead && !controllerMobile.activeSelf) {

            if(Input.GetKeyDown(KeyCode.Escape)) {
                if(!menu.activeSelf) {
                    stats.SetActive(false);
                    menu.SetActive(true);
                    Time.timeScale = 0;
                } else {
                    menu.SetActive(false);
                    stats.SetActive(true);
                    Time.timeScale = 1;
                }
            }

            canJump = jumpController.getJump();
            canDoubleJump = jumpController.getDoubleJump();

            if (Input.GetKeyDown(KeyCode.Space)) {
                if(canJump) {
                    CreateDust();
                    body.linearVelocity = new Vector2(body.linearVelocity.x, jumpHeight);
                    jumpController.setJump(false);
                } else if(canDoubleJump) {
                    CreateDust();
                    body.linearVelocity = new Vector2(body.linearVelocity.x, jumpHeight);
                    jumpController.setDoubleJump(false);
                    canDoubleJump = false;
                }

            }

            if (Input.GetKey(KeyCode.A)) {
                CreateDust();
                body.linearVelocity = new Vector2(-moveSpeed, body.linearVelocity.y);
                SetMoving(true);
                playerSprite.flipX = true;
                
                rotacionA = true;
                if (rotacionD == true) {
                    rowTransform.Rotate(0f, 180f, 0f);
                    rotacionD = false;
                }
            }

            if (Input.GetKey(KeyCode.D)) {
                CreateDust();
                body.linearVelocity = new Vector2(moveSpeed, body.linearVelocity.y);
                SetMoving(true);
                playerSprite.flipX = false;
                rotacionD = true;
                if(rotacionA == true) {
                    rowTransform.Rotate(0f, 180f, 0f);
                    rotacionA = false;
                }
            }


            // Voltear sprites del character
            if (!Input.GetKey(KeyCode.A) && !Input.GetKey(KeyCode.D)) {
                SetMoving(false);
            }
        }
    }

    // Salto solo cuando pisa el suelo
    private void OnCollisionEnter2D(Collision2D collision) {

        if (collision.transform.tag == "Patrols") {
            if (Time.time >= nextAttactTime) {
                playerStats.ApplyDamage(damagePatrols, "enemy_contact");
                nextAttactTime = Time.time + attactRate;

            }
            body.AddForce(new Vector2(5, 5) * 2, ForceMode2D.Impulse);
        }
    }

    // Scores
    private void OnTriggerEnter2D(Collider2D collision) {
        if (collision.gameObject.CompareTag("Coins")) {
            Destroy(collision.gameObject);
        }

        if (collision.gameObject.CompareTag("Gems")) {
            Destroy(collision.gameObject);
        }

        if (collision.gameObject.CompareTag("Stars")) {
            Destroy(collision.gameObject);
        }

        if (collision.gameObject.CompareTag("Heart")) {
            Destroy(collision.gameObject);
        }

        if (collision.gameObject.CompareTag("Sword")) {
            Destroy(collision.gameObject);
        }

        if (collision.gameObject.CompareTag("Shield")) {
            Destroy(collision.gameObject);
        }

        if (collision.gameObject.CompareTag("ReSpawn")) {
            currentRespawn = collision.transform;
        }
    }

    public void reSpawn() {
        if(currentRespawn == null) {
            currentRespawn = FindClosestRespawn();
            if(currentRespawn == null) {
                return;
            }
        }

        transform.position = new Vector3(currentRespawn.position.x, currentRespawn.position.y, transform.position.z);

    }

    private Transform FindClosestRespawn() {
        GameObject[] respawns = GameObject.FindGameObjectsWithTag("ReSpawn");
        Transform closest = null;
        float closestDistance = float.MaxValue;

        foreach(GameObject respawn in respawns) {
            float distance = (respawn.transform.position - transform.position).sqrMagnitude;
            if(distance < closestDistance) {
                closestDistance = distance;
                closest = respawn.transform;
            }
        }

        return closest;
    }

    public void destroy() {
        Object.Destroy(gameObject, 5.0f);
        dead = true;
    }

    public bool isDead() {
        return this.dead = true;
    }

    public void Revive() {
        dead = false;
    }

    void CreateDust() {
        if (!dust.isPlaying) {
            dust.Play();
        }
    }

    void SetMoving(bool moving) {
        if (isMoving == moving) {
            return;
        }

        isMoving = moving;
        playerAnimator.SetBool("moving", moving);
    }
}
