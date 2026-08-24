using UnityEngine;

public class NpcFacing : MonoBehaviour
{
    public Transform player;
    public bool isFlipped = false;
    public bool autoLookAtPlayer = false;

    private void Update()
    {
        if (autoLookAtPlayer && player != null)
        {
            LookAtPlayer();
        }
    }

    public void LookAtPlayer()
    {
        if (player == null)
        {
            return;
        }

        FaceDirection(Mathf.Sign(player.position.x - transform.position.x));
    }

    public void FaceDirection(float direction)
    {
        if (direction == 0f)
        {
            return;
        }

        Vector3 flipped = transform.localScale;
        flipped.z *= -1f;

        if (direction < 0f && isFlipped)
        {
            transform.localScale = flipped;
            transform.Rotate(0f, 180f, 0f);
            isFlipped = false;
        }
        else if (direction > 0f && !isFlipped)
        {
            transform.localScale = flipped;
            transform.Rotate(0f, 180f, 0f);
            isFlipped = true;
        }
    }

    public bool isFlip()
    {
        return isFlipped;
    }
}
