using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;



public class DialogManager : MonoBehaviour
{
    [Header("Data")]
    public TextAsset dialogDataFile;

    [Header("Sprites (SpriteRenderer)")]
    public SpriteRenderer spriteLeft;
    public SpriteRenderer spriteRight;

    [Header("UI Text (TMP)")]
    public TMP_Text nameText;
    public TMP_Text dialogText;

    [Header("Character Sprites (按角色名映射)")]
    public List<Sprite> sprites = new List<Sprite>(); // 你在 Inspector 里拖两张立绘
    private readonly System.Collections.Generic.Dictionary<string, Sprite> imageDic =
        new System.Collections.Generic.Dictionary<string, Sprite>();

    [Header("Flow")]
    public int dialogIndex = 1;
    public string[] dialogRows;

    [Header("Next Button")]
    public Button next;

    [Header("Options")]
    public GameObject optionButton;   
    public Transform buttonGroup;     

    [Header("Speaker Highlight")]
    [Range(0f, 1f)] public float dimAlpha = 0.35f; // 另一边半透明
    public float brightAlpha = 1f;                 // 说话者亮度

    private void Awake()
    {
        if (sprites != null && sprites.Count >= 2)
        {
            imageDic["张舒然"] = sprites[0];
            imageDic["普通细胞"] = sprites[1];
        }
        else
        {
            Debug.LogWarning("sprites 列表数量不足（至少2个）。请在 Inspector 给 sprites 拖入两张角色立绘。");
        }
    }

    private void Start()
    {
        ReadText(dialogDataFile);

        // 开局：选项容器先清空并隐藏（防止一开始就出现预放的按钮）
        ClearOptions();
        if (buttonGroup != null) buttonGroup.gameObject.SetActive(false);

        // 开局：两边都显示但先变暗，后续由对话行高亮当前说话者
        if (spriteLeft != null)  { spriteLeft.gameObject.SetActive(true);  spriteLeft.color = new Color(1,1,1,dimAlpha); }
        if (spriteRight != null) { spriteRight.gameObject.SetActive(true); spriteRight.color = new Color(1,1,1,dimAlpha); }

        ShowDialogRow();
    }

    public void ReadText(TextAsset textAsset)
    {
        if (textAsset == null)
        {
            Debug.LogError("Dialog Data File 没有赋值！");
            dialogRows = new string[0];
            return;
        }

        dialogRows = textAsset.text.Split('\n');
        Debug.Log("读取成功");
    }

    // ========= 工具函数 =========
    private string Cell(string[] cells, int idx)
    {
        if (cells == null) return "";
        if (idx < 0 || idx >= cells.Length) return "";
        return cells[idx].Replace("\r", "").Trim();
    }

    private bool TryCellInt(string[] cells, int idx, out int value)
    {
        return int.TryParse(Cell(cells, idx), out value);
    }

    // ========= UI更新 =========
    public void UpdateText(string who, string text)
    {
        if (nameText != null) nameText.text = who;
        if (dialogText != null) dialogText.text = text;
    }

    /// <summary>
    /// 说话者高亮，另一边半透明。
    /// CSV 里的 position 用 “左/右”
    /// </summary>
    public void UpdateImage(string who, string position)
    {
        if (!imageDic.ContainsKey(who)) return;

        if (spriteLeft == null || spriteRight == null) return;

        // 两边都显示
        spriteLeft.gameObject.SetActive(true);
        spriteRight.gameObject.SetActive(true);

        // 先统一变暗
        spriteLeft.color  = new Color(1f, 1f, 1f, dimAlpha);
        spriteRight.color = new Color(1f, 1f, 1f, dimAlpha);

        // 再高亮说话者那边，并更新该边 sprite
        if (position == "左")
        {
            spriteLeft.sprite = imageDic[who];
            spriteLeft.color = new Color(1f, 1f, 1f, brightAlpha);
        }
        else if (position == "右")
        {
            spriteRight.sprite = imageDic[who];
            spriteRight.color = new Color(1f, 1f, 1f, brightAlpha);
        }
    }

    // ========= 主流程 =========
    public void ShowDialogRow()
    {
        if (dialogRows == null || dialogRows.Length == 0) return;

        for (int i = 0; i < dialogRows.Length; i++)
        {
            string row = dialogRows[i].Replace("\r", "").Trim();
            if (string.IsNullOrEmpty(row)) continue;

            string[] cells = row.Split(',');
            string tag = Cell(cells, 0);

            // 普通对白：#,id,name,pos,text,nextId
            if (tag == "#" && TryCellInt(cells, 1, out int id) && id == dialogIndex)
            {
                string who = Cell(cells, 2);
                string pos = Cell(cells, 3);
                string text = Cell(cells, 4);

                UpdateText(who, text);
                UpdateImage(who, pos);

                // nextId 可能为空，空则不跳转（避免报错）
                if (TryCellInt(cells, 5, out int nextId))
                    dialogIndex = nextId;

                // 显示 Next，隐藏选项
                if (next != null) next.gameObject.SetActive(true);
                if (buttonGroup != null) buttonGroup.gameObject.SetActive(false);

                return;
            }

            // 分支选项：&,id, ,pos,optionText,jumpId
            // 你的CSV里每条选项也是用 &，并且是连续多行 &
            if (tag == "&" && TryCellInt(cells, 1, out int optId) && optId == dialogIndex)
            {
                if (next != null) next.gameObject.SetActive(false);

                if (buttonGroup != null) buttonGroup.gameObject.SetActive(true);
                GenerateOptions(i);

                return;
            }

            // 结束：end,id
            if (tag == "end" && TryCellInt(cells, 1, out int endId) && endId == dialogIndex)
            {
                Debug.Log("剧情结束");
                if (next != null) next.gameObject.SetActive(false);
                if (buttonGroup != null) buttonGroup.gameObject.SetActive(false);
                ClearOptions();
                return;
            }
        }

        Debug.LogWarning("没有找到 dialogIndex=" + dialogIndex + " 对应行，请检查 CSV。");
    }

    // 从 _startIndex 开始，把连续的 & 行全部生成按钮
    public void GenerateOptions(int startIndex)
    {
        ClearOptions();

        if (optionButton == null || buttonGroup == null)
        {
            Debug.LogError("optionButton 或 buttonGroup 未赋值！");
            return;
        }

        for (int i = startIndex; i < dialogRows.Length; i++)
        {
            string row = dialogRows[i].Replace("\r", "").Trim();
            if (string.IsNullOrEmpty(row)) continue;

            string[] cells = row.Split(',');
            string tag = Cell(cells, 0);

            if (tag != "&") break; // 选项段结束

            string optionText = Cell(cells, 4);
            if (string.IsNullOrEmpty(optionText)) continue;

            if (!TryCellInt(cells, 5, out int jumpId))
            {
                Debug.LogWarning("选项 jumpId 为空/非法: " + row);
                continue;
            }

            GameObject btnGO = Instantiate(optionButton, buttonGroup);

            // 文本
            TMP_Text t = btnGO.GetComponentInChildren<TMP_Text>();
            if (t != null) t.text = optionText;

            // 点击
            Button b = btnGO.GetComponent<Button>();
            if (b != null)
            {
                int captured = jumpId;
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => OnOptionClick(captured));
            }
        }
    }

    public void OnOptionClick(int nextId)
    {
        ClearOptions();
        if (buttonGroup != null) buttonGroup.gameObject.SetActive(false);

        // 恢复 Next
        if (next != null) next.gameObject.SetActive(true);

        dialogIndex = nextId;
        ShowDialogRow();
    }

    public void OnClickNext()
    {
        ShowDialogRow();
    }

    private void ClearOptions()
    {
        if (buttonGroup == null) return;

        for (int i = buttonGroup.childCount - 1; i >= 0; i--)
        {
            Destroy(buttonGroup.GetChild(i).gameObject);
        }
    }
}
