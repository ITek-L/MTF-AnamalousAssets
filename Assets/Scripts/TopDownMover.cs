using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class TopDownMover : MonoBehaviour
{
    public float moveSpeed = 6f;

    Rigidbody2D rb;
    Vector2 move;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void Update()
    {
        var k = Keyboard.current;
        if (k == null) { move = Vector2.zero; return; }

        float x = (k.dKey.isPressed || k.rightArrowKey.isPressed) ? 1f :
                  (k.aKey.isPressed || k.leftArrowKey.isPressed) ? -1f : 0f;

        float y = (k.wKey.isPressed || k.upArrowKey.isPressed) ? 1f :
                  (k.sKey.isPressed || k.downArrowKey.isPressed) ? -1f : 0f;

        move = new Vector2(x, y);
        if (move.sqrMagnitude > 1f) move.Normalize();
    }

    void FixedUpdate()
    {
        rb.linearVelocity = move * moveSpeed;
    }
}
