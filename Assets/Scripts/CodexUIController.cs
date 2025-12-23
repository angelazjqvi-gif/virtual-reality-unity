using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CodexUIController : MonoBehaviour
{
    [Header("UI Refs")]
    public GameObject codexPanel;
    public TMP_Text titleText;     
    public TMP_Text infoText;         
    public Button closeButton;

    [Header("Right Portrait")]
    public Image portraitImage;          
    public Sprite defaultPortrait;      
    public bool cycleUnlockedOnly = true;        

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

        if (isOpen && Input.GetKeyDown(KeyCode.Tab))
            CycleNextPlayer();

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
        if (titleText != null)
        {
            titleText.text = "<size=80><color=#FF3B30><b>角色图鉴</b></color></size>";
        }

        infoText.text =
            $"{pd.playerName}\n" +
            $"HP {pd.currentHp}/{pd.baseMaxHp}\n" +
            $"Lv {pd.level}  EXP {pd.exp}/{need}\n" +
            $"ATK {pd.baseAtk}\n" +
            $"DEF {pd.baseDef}\n" +
            $"SPD {pd.baseSpd}\n" +
            $"CR {(pd.baseCr * 100f):0.#}%\n" +
            $"CD {pd.baseCd:0.00}x";

        UpdatePortrait(pd);
    }

    private void UpdatePortrait(GameSession.PlayerData pd)
    {
        if (portraitImage == null) return;

        Sprite sp = null;
        if (pd != null) sp = pd.codexPortrait; 

        if (sp == null) sp = defaultPortrait;

        portraitImage.sprite = sp;
        portraitImage.enabled = (sp != null);
        portraitImage.preserveAspect = true;
    }

    private void CycleNextPlayer()
    {
        if (GameSession.I == null) return;

        GameSession.I.EnsurePartySize(1);

        int count = (GameSession.I.party != null) ? GameSession.I.party.Count : 0;
        if (count <= 1) return;

        int cur = Mathf.Clamp(GameSession.I.activePlayerIndex, 0, count - 1);

        // 最多尝试 count 次，避免死循环
        for (int step = 1; step <= count; step++)
        {
            int next = (cur + step) % count;
            var pd = GameSession.I.party[next];

            if (cycleUnlockedOnly && pd != null && !pd.unlocked)
                continue;

            GameSession.I.SetActivePlayerIndex(next); // 这里会触发 OnActivePlayerChanged :contentReference[oaicite:4]{index=4}
            return;
        }
    }
}
