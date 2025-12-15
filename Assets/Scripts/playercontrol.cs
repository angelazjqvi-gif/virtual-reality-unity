using UnityEngine;

public class playercontrol : MonoBehaviour
{
    private Animator ani;
    private Rigidbody2D rBody;
    private SpriteRenderer sr;

    [SerializeField] private float moveSpeed = 0.3f;

    // 记录移动方向，给 FixedUpdate 用（更稳）
    private Vector2 dir;

    void Start()
    {
        ani = GetComponent<Animator>();
        rBody = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();   //新增：用于 flipX
    }

    void Update()
    {
        // 获取输入（-1, 0, 1）
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // 新增：左右镜像（只要按左右就翻转）
        if (horizontal > 0)
            sr.flipX = false;   // 面向右
        else if (horizontal < 0)
            sr.flipX = true;    // 面向左

        // 你的动画方向参数（互斥设置，保持原逻辑）
        if (horizontal != 0)
        {
            ani.SetFloat("Horizontal", horizontal);
            ani.SetFloat("Vertical", 0);
        }

        if (vertical != 0)
        {
            ani.SetFloat("Horizontal", 0);
            ani.SetFloat("Vertical", vertical);
        }

        // 移动向量
        dir = new Vector2(horizontal, vertical);

        // Speed 参数：静止=0，移动>0（保持你的写法）
        ani.SetFloat("Speed", dir.magnitude);
    }

    void FixedUpdate()
    {
        rBody.velocity = dir * moveSpeed;
    }
}
