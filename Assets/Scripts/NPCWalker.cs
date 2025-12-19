using UnityEngine;

public class NPCWalker : MonoBehaviour
{
    [Header("Path")]
    public Transform[] waypoints;
    public float speed = 2f;
    public float arriveDistance = 0.1f;

    [Header("Avoidance")]
    public float detectDistance = 0.6f;
    public LayerMask blockLayers; 

    [Header("Animator")]
    public Animator animator;

    private int currentIndex = 0;
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        if (animator == null)
            animator = GetComponent<Animator>();
    }

    void FixedUpdate()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Transform target = waypoints[currentIndex];
        Vector2 dir = (target.position - transform.position);
        float dist = dir.magnitude;

        Vector2 moveDir = dir.normalized;

        if (Mathf.Abs(moveDir.x) > Mathf.Abs(moveDir.y))
        {
            animator.SetFloat("moveX", moveDir.x > 0 ? 1f : -1f);
            animator.SetFloat("moveY", 0f);
        }
        else
        {
            animator.SetFloat("moveX", 0f);
            animator.SetFloat("moveY", moveDir.y > 0 ? 1f : -1f);
        }

        RaycastHit2D hit = Physics2D.Raycast(
            transform.position,
            moveDir,
            detectDistance,
            blockLayers
        );

        bool blocked = hit.collider != null;

        animator.SetBool("isWalking", !blocked && dist > arriveDistance);

        if (blocked)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        if (dist > arriveDistance)
        {
            rb.velocity = moveDir * speed;

            if (moveDir.x != 0)
            {
                Vector3 scale = transform.localScale;
                scale.x = Mathf.Abs(scale.x) * (moveDir.x > 0 ? 1 : -1);
                transform.localScale = scale;
            }
        }
        else
        {
            rb.velocity = Vector2.zero;
            currentIndex = (currentIndex + 1) % waypoints.Length;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(
            transform.position,
            transform.position + transform.right * detectDistance
        );
    }
#endif
}
