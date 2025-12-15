using UnityEngine;

public class RoleSwapManager : MonoBehaviour
{
    [Header("Drag 2 player GameObjects here (whole GameObject, not script)")]
    public GameObject[] playerObjects;  // size=2

    [Header("Switch Key")]
    public KeyCode switchKey = KeyCode.Tab;

    [Header("Spawn/Swap Settings")]
    public bool keepSameWorldPosition = true;   // 切换时是否保持同一位置
    public bool keepSameRotation = true;

    private int currentIndex = 0;

    void Start()
    {
        // 安全检查
        if (playerObjects == null || playerObjects.Length == 0)
        {
            Debug.LogError("RoleSwapManager: playerObjects is empty!");
            enabled = false;
            return;
        }

        // 确保开局只有一个上场
        for (int i = 0; i < playerObjects.Length; i++)
        {
            if (playerObjects[i] != null)
                playerObjects[i].SetActive(i == currentIndex);
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(switchKey))
        {
            SwapToNext();
        }
    }

    public void SwapToNext()
    {
        if (playerObjects.Length < 2) return;

        GameObject current = playerObjects[currentIndex];
        int nextIndex = (currentIndex + 1) % playerObjects.Length;
        GameObject next = playerObjects[nextIndex];

        if (current == null || next == null) return;

        // 记录当前角色的位置/朝向（让新角色“在原地上场”）
        Vector3 pos = current.transform.position;
        Quaternion rot = current.transform.rotation;

        // 下场
        current.SetActive(false);

        // 上场
        if (keepSameWorldPosition) next.transform.position = pos;
        if (keepSameRotation) next.transform.rotation = rot;

        next.SetActive(true);
        currentIndex = nextIndex;
    }

    // 可选：外部直接切换到指定index
    public void SwapTo(int index)
    {
        if (index < 0 || index >= playerObjects.Length) return;
        if (index == currentIndex) return;

        GameObject current = playerObjects[currentIndex];
        GameObject next = playerObjects[index];
        if (current == null || next == null) return;

        Vector3 pos = current.transform.position;
        Quaternion rot = current.transform.rotation;

        current.SetActive(false);
        next.transform.position = pos;
        next.transform.rotation = rot;
        next.SetActive(true);

        currentIndex = index;
    }

    public GameObject GetCurrentPlayer() => playerObjects[currentIndex];
}
