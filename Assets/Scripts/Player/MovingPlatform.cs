using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(TilemapCollider2D), typeof(Rigidbody2D))]
[DefaultExecutionOrder(-50)]
public sealed class MovingPlatform : MonoBehaviour {
    public enum ActivationMode {
        Automatic,
        WhilePlayerRiding
    }

    public enum TravelMode {
        Once,
        PingPong
    }

    [SerializeField] private ActivationMode activationMode = ActivationMode.WhilePlayerRiding;
    [SerializeField] private TravelMode travelMode = TravelMode.Once;
    [SerializeField] private Vector2 destinationOffset = new Vector2(5f, 0f);
    [SerializeField, Min(0f)] private float speed = 2f;

    private readonly HashSet<PlayerController> riders = new HashSet<PlayerController>();
    private Rigidbody2D body;
    private Vector2 startPosition;
    private Vector2 destination;
    private Vector2 currentVelocity;
    private bool movingTowardDestination = true;
    private bool completed;

    public Vector2 Velocity => currentVelocity;

    private void Reset() {
        gameObject.tag = "Ground";

        Rigidbody2D platformBody = GetComponent<Rigidbody2D>();
        if (platformBody == null) return;

        platformBody.bodyType = RigidbodyType2D.Kinematic;
        platformBody.gravityScale = 0f;
        platformBody.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    private void Awake() {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;

        startPosition = body.position;
        destination = startPosition + destinationOffset;
    }

    private void FixedUpdate() {
        currentVelocity = Vector2.zero;
        if (completed || speed <= 0f || destinationOffset.sqrMagnitude <= Mathf.Epsilon) return;
        if (activationMode == ActivationMode.WhilePlayerRiding && riders.Count == 0) return;

        Vector2 currentPosition = body.position;
        Vector2 target = movingTowardDestination ? destination : startPosition;
        float step = speed * Time.fixedDeltaTime;
        float remainingDistance = Vector2.Distance(currentPosition, target);
        Vector2 nextPosition = remainingDistance <= step
            ? target
            : Vector2.MoveTowards(currentPosition, target, step);
        Vector2 displacement = nextPosition - currentPosition;

        if (displacement.sqrMagnitude <= Mathf.Epsilon) {
            if (remainingDistance <= Mathf.Epsilon) HandleEndpointReached();
            return;
        }

        currentVelocity = displacement / Time.fixedDeltaTime;
        body.MovePosition(nextPosition);

        if (remainingDistance <= step) HandleEndpointReached();
    }

    public void SetRider(PlayerController player, bool isRiding) {
        if (player == null) return;

        if (isRiding) riders.Add(player);
        else riders.Remove(player);
    }

    private void HandleEndpointReached() {
        if (travelMode == TravelMode.Once) {
            completed = true;
            return;
        }

        movingTowardDestination = !movingTowardDestination;
    }

    private void OnDisable() {
        currentVelocity = Vector2.zero;
        PlayerController[] currentRiders = new PlayerController[riders.Count];
        riders.CopyTo(currentRiders);
        riders.Clear();

        foreach (PlayerController rider in currentRiders) {
            if (rider != null) rider.ClearMovingPlatformSupport(this);
        }
    }
}
