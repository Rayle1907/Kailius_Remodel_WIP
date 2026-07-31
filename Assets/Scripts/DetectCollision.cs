using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DetectCollision : MonoBehaviour
{
    // Start is called before the first frame update
    void Start() {}

    // Update is called once per frame
    void Update() {}

    private void OnCollisionEnter2D(Collision2D collision)
    {
        string tag = collision.collider.gameObject.tag;
        if (tag == "Coins")
        {
            Destroy(collision.collider.gameObject);
        }
    }
}
