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

    [Header("UI Text")]
    public TMP_Text nameText;
    public TMP_Text dialogText;

    [Header("Character Sprites")]
    public List<Sprite> sprites = new List<Sprite>(); 
    private readonly System.Collections.Generic.Dictionary<string, Sprite> imageDic =
        new System.Collections.Generic.Dictionary<string, Sprite>();

    [Header("Flow")]
    public int dialogIndex = 0;
    public string[] dialogRows;

    [Header("Next Button")]
    public Button next;

    [Header("Options")]
    public GameObject optionButton;   
    public Transform buttonGroup;     

    [Header("Speaker Highlight")]
    [Range(0f, 1f)] public float dimAlpha = 0.35f; // 另一边半透明
    public float brightAlpha = 1f;                 // 说话者亮度
    
    [System.Serializable]
    public class PortraitEntry
    {
        public string who;            
        public string expression;     
        public Sprite sprite;         
    }
    
    [System.Serializable]
    public class BodyEntry
    {
        public string who;
        public string expression;
        public Sprite sprite;
    }

    [Header("Body Expression (SpriteRenderer)")]
    public List<BodyEntry> bodyEntries = new List<BodyEntry>();

    private readonly Dictionary<string, Sprite> bodyDic = new Dictionary<string, Sprite>();
    private string BodyKey(string who, string exp) => (who ?? "") + "|" + (exp ?? "");


    [Header("Portrait")]
    public Image portraitImage;                       
    public List<PortraitEntry> portraitEntries = new List<PortraitEntry>();

    private readonly Dictionary<string, Sprite> portraitDic = new Dictionary<string, Sprite>();
    
    private string PortraitKey(string who, string exp) => (who ?? "") + "|" + (exp ?? "");

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

        portraitDic.Clear();
        if (portraitEntries != null)
        {
            for (int i = 0; i < portraitEntries.Count; i++)
            {
                var e = portraitEntries[i];
                if (e == null) continue;
                if (string.IsNullOrEmpty(e.who)) continue;
                if (string.IsNullOrEmpty(e.expression)) continue;
                if (e.sprite == null) continue;

                string key = PortraitKey(e.who.Trim(), e.expression.Trim());
                if (!portraitDic.ContainsKey(key))
                    portraitDic.Add(key, e.sprite);
            }
        }

        bodyDic.Clear();
        if (bodyEntries != null)
        {
            for (int i = 0; i < bodyEntries.Count; i++)
            {
                var e = bodyEntries[i];
                if (e == null) continue;
                if (string.IsNullOrEmpty(e.who)) continue;
                if (string.IsNullOrEmpty(e.expression)) continue;
                if (e.sprite == null) continue;

                string key = BodyKey(e.who.Trim().Trim('\uFEFF'), e.expression.Trim().Trim('\uFEFF'));
                if (!bodyDic.ContainsKey(key))
                    bodyDic.Add(key, e.sprite);
            }
        }
    }

    private void Start()
    {
        ReadText(dialogDataFile);

        
        ClearOptions();
        if (buttonGroup != null) buttonGroup.gameObject.SetActive(false);

        
        if (spriteLeft != null)  { spriteLeft.gameObject.SetActive(true);  spriteLeft.color = new Color(1,1,1,dimAlpha); }
        if (spriteRight != null) { spriteRight.gameObject.SetActive(true); spriteRight.color = new Color(1,1,1,dimAlpha); }

         if (portraitImage != null) portraitImage.gameObject.SetActive(true);

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

    
    public void UpdateText(string who, string text)
    {
        if (nameText != null) nameText.text = who;
        if (dialogText != null) dialogText.text = text;
    }
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

    public void UpdatePortrait(string who, string portraitExpression)
    {
        if (portraitImage == null) return;

        portraitImage.gameObject.SetActive(true);

        // portraitExpression 为空时默认 normal
        string exp = string.IsNullOrEmpty(portraitExpression) ? "normal" : portraitExpression.Trim();
        string key = PortraitKey((who ?? "").Trim(), exp);

        if (portraitDic.TryGetValue(key, out Sprite sp) && sp != null)
        {
            portraitImage.sprite = sp;
            portraitImage.enabled = true;
        }
        else
        {
            // 找不到就不报错、不清空
            // portraitImage.sprite = null;
            // portraitImage.enabled = false;
        }
    }
    
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
                string bodyExp = Cell(cells, 6);
                string portraitExp = Cell(cells, 7);

                UpdateText(who, text);
                UpdateImage(who, pos);
                UpdateBodyExpression(who, pos, bodyExp);
                UpdatePortrait(who, portraitExp);

                
                if (TryCellInt(cells, 5, out int nextId))
                    dialogIndex = nextId;

                if (next != null) next.gameObject.SetActive(true);
                if (buttonGroup != null) buttonGroup.gameObject.SetActive(false);

                return;
            }

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


    public void UpdateBodyExpression(string who, string position, string bodyExpression)
    {
        if (spriteLeft == null || spriteRight == null) return;

        string w = (who ?? "").Trim().Trim('\uFEFF');
        string exp = string.IsNullOrEmpty(bodyExpression) ? "normal" : bodyExpression.Trim().Trim('\uFEFF');

        // 1) 先精确匹配 who|exp
        Sprite sp = null;
        string key = BodyKey(w, exp);
        if (!bodyDic.TryGetValue(key, out sp) || sp == null)
        {
            // 2) 回退 normal，避免找不到导致沿用上一句
            string key2 = BodyKey(w, "normal");
            bodyDic.TryGetValue(key2, out sp);
        }
        if (sp == null) return;

        // 只换说话者那一侧的立绘
        if (position == "左")
        {
            spriteLeft.sprite = sp;
        }
        else if (position == "右")
        {
            spriteRight.sprite = sp;
        }
    }

}