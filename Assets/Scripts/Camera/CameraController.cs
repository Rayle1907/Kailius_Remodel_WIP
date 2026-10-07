using UnityEngine;

public class CameraController : MonoBehaviour {
    public GameObject objetivo;
    public float Velocidad = 2f;
    public float ZOriginal = 0;
    public float smoothRate = 0.3f;
    private Vector3 velocidadCamara;
    private Vector3 shakeOffset;
    private float shakeDuration;
    private float shakeRemaining;
    private float shakeAmplitude;

    public Vector3 UnshakenPosition => transform.position - shakeOffset;

    public void Shake(float duration, float amplitude) {
        if (!isActiveAndEnabled || duration <= 0f || amplitude <= 0f) {
            return;
        }
        shakeDuration = duration;
        shakeRemaining = duration;
        shakeAmplitude = amplitude;
    }


    // Start is called before the first frame update
    void Start() {
        ZOriginal = transform.position.z;
        velocidadCamara = new Vector3(Velocidad, Velocidad, 0);

    }

    void LateUpdate() {
        Vector3 position = UnshakenPosition;
        if (objetivo != null && Time.deltaTime > 0f) {
            position = Vector3.SmoothDamp(position, objetivo.transform.position, ref velocidadCamara, this.smoothRate);
        }
        position.z = ZOriginal;
        if (Time.deltaTime > 0f) {
            shakeRemaining = Mathf.Max(0f, shakeRemaining - Time.deltaTime);
            Vector2 offset = shakeRemaining > 0f
                ? Random.insideUnitCircle * (shakeAmplitude * shakeRemaining / shakeDuration)
                : Vector2.zero;
            shakeOffset = new Vector3(offset.x, offset.y, 0f);
        }
        transform.position = position + shakeOffset;
    }

    void OnDisable() {
        transform.position -= shakeOffset;
        shakeOffset = Vector3.zero;
        shakeRemaining = 0f;
    }
}
