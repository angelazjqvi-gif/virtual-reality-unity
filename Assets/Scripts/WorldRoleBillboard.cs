using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WorldRoleBillboard : MonoBehaviour
{
    [Header("Reference")]
    public RoleSwapManager swap;             // 拖你的 RoleSwapManager
    public Transform followTarget;           // 不拖也行，会自动用当前角色transform

    [Header("UI (Text or TMP)")]
    public Text uiText;
    public TMP_Text tmpText;

    [Header("Billboard")]
    public Vector3 offset = new Vector3(0, 2.0f, 0);
    public bool faceCamera = true;

    Camera cam;

    void Start()
    {
        cam = Camera.main;
        if (swap != null)
        {
            swap.OnRoleSwapped += (_, go) =>
            {
                followTarget = (go != null) ? go.transform : null;
                Refresh();
            };
        }
        Refresh();
    }

    void LateUpdate()
    {
        if (swap == null || GameSession.I == null) return;

        if (followTarget == null)
        {
            var cur = swap.GetCurrentPlayer();
            if (cur != null) followTarget = cur.transform;
        }

        if (followTarget != null)
            transform.position = followTarget.position + offset;

        if (faceCamera && cam != null)
            transform.forward = cam.transform.forward;

        Refresh();
    }

    public void Refresh()
    {
        if (GameSession.I == null) return;

        var p = GameSession.I.GetActivePlayerData();
        int need = GameSession.I.ExpToNextLevel(p.level);

        string s =
            $"{p.playerName}\n" +
            $"HP {p.currentHp}/{p.baseMaxHp}\n" +
            $"Lv {p.level}  EXP {p.exp}/{need}";

        if (uiText != null) uiText.text = s;
        if (tmpText != null) tmpText.text = s;
    }
}
