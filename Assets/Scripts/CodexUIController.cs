using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CodexUIController : MonoBehaviour
{
    [Header("UI Refs")]
    public GameObject codexPanel;     // 你的 CodexPanel（面板根物体）
    public TMP_Text infoText;         // 面板里显示文字的 TMP_Text
    public Button closeButton;        // 面板里的“关闭”按钮（可不填）

    [Header("Toggle Key")]
    public KeyCode toggleKey = KeyCode.C;

    private bool isOpen = false;

    void Start()
    {
        if (codexPanel != null) codexPanel.SetActive(false);
        isOpen = false;

        // 绑定关闭按钮
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        // 订阅“当前角色切换”事件：切人后自动刷新显示
        if (GameSession.I != null)
        {
            GameSession.I.OnActivePlayerChanged += OnActivePlayerChanged;
        }

        // 初始化一次文本
        RefreshText();
    }

    void OnDestroy()
    {
        if (GameSession.I != null)
        {
            GameSession.I.OnActivePlayerChanged -= OnActivePlayerChanged;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            Toggle();
        }

        if (isOpen)
        {
            RefreshText();
        }
    }

    private void OnActivePlayerChanged(int idx)
    {
        // 切人时刷新
        RefreshText();
    }

    public void Toggle()
    {
        if (isOpen) Hide();
        else Show();
    }

    public void Show()
    {
        isOpen = true;
        if (codexPanel != null) codexPanel.SetActive(true);
        RefreshText();
    }

    public void Hide()
    {
        isOpen = false;
        if (codexPanel != null) codexPanel.SetActive(false);
    }

    private void RefreshText()
    {
        if (infoText == null) return;

        if (GameSession.I == null)
        {
            infoText.text = "(GameSession.I is null)";
            return;
        }

        // 确保至少有1个角色数据
        GameSession.I.EnsurePartySize(1);

        var pd = GameSession.I.GetActivePlayerData();
        if (pd == null)
        {
            infoText.text = "(No Active PlayerData)";
            return;
        }

        int need = GameSession.I.ExpToNextLevel(pd.level);
        if (need <= 0) need = 1;

        infoText.text =
            $"{pd.playerName}\n" +
            $"HP {pd.currentHp}/{pd.baseMaxHp}\n" +
            $"Lv {pd.level}  EXP {pd.exp}/{need}"+
            $"ATK {pd.baseAtk}\n" +
            $"DEF {pd.baseDef}\n" +
            $"SPD {pd.baseSpd}\n" +
            $"CR {(pd.baseCr * 100f):0.#}%\n" +
            $"CD {pd.baseCd:0.00}x";
    }
}
