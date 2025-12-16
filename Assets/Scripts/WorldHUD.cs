using UnityEngine;

// ✅这是“占位”脚本：只负责让 RoleSwapManager 里的 WorldHUD.Refresh() 不报错
// 以后你要左上角HUD/头顶HUD，都可以在这里扩展
public class WorldHUD : MonoBehaviour
{
    public void Refresh()
    {
        // 先留空：不影响运行
    }
}
