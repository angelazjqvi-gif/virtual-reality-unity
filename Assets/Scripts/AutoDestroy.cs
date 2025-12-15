using UnityEngine;

public class AutoDestroy : MonoBehaviour
{
    [Tooltip("特效存在时间（秒），设成略大于动画时长")]
    public float lifeTime = 1.5f;

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }
}
