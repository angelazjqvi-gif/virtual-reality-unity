using UnityEngine;

public class WorldCameraFollow2D : MonoBehaviour
{
    [Header("Refs")]
    public RoleSwapManager roleSwap;  
    public Transform target;          

    [Header("Follow")]
    public Vector3 offset = new Vector3(0f, 0f, -10f);
    public float smooth = 10f;

    void OnEnable()
    {
        if (roleSwap != null)
            roleSwap.OnRoleSwapped += HandleRoleSwapped;
    }

    void OnDisable()
    {
        if (roleSwap != null)
            roleSwap.OnRoleSwapped -= HandleRoleSwapped;
    }

    void Start()
    {
        if (roleSwap != null)
        {
            var cur = roleSwap.GetCurrentPlayer(); 
            if (cur != null) target = cur.transform;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desired, smooth * Time.deltaTime);
    }

    void HandleRoleSwapped(int index, GameObject curPlayer)
    {
        if (curPlayer != null) target = curPlayer.transform;
    }
}
