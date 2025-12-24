using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class DialogManager : MonoBehaviour
{
    [Header("Data")]
    public TextAsset dialogDataFile;

    [Header("UI Text")]
    public TMP_Text nameText;
    public TMP_Text dialogText;

    // ===================== 可扩展：舞台槽位（任意数量） =====================
    [System.Serializable]
    public class StageSlot
    {
        public string posKey;              // CSV 的 position 列写这个
        public SpriteRenderer renderer;    // 对应的 SpriteRenderer
    }

    [Header("Stage Slots (SpriteRenderer)")]
    public List<StageSlot> slots = new List<StageSlot>();
    private readonly Dictionary<string, SpriteRenderer> slotDic = new Dictionary<string, SpriteRenderer>();

    // ===================== 可扩展：角色默认立绘（任意数量） =====================
    [System.Serializable]
    public class CharacterEntry
    {
        public string who;     // CSV 的名字
        public Sprite sprite;  // 默认立绘
    }

    [Header("Characters (Default Sprite)")]
    public List<CharacterEntry> characters = new List<CharacterEntry>();
    private readonly Dictionary<string, Sprite> imageDic = new Dictionary<string, Sprite>();

    [Header("Flow")]
    public int dialogIndex = 0;
    public string[] dialogRows;

    [Header("Next Button")]
    public Button next;

    [Header("Options")]
    public GameObject optionButton;
    public Transform buttonGroup;

    [Header("Speaker Highlight")]
    [Range(0f, 1f)] public float dimAlpha = 0.35f;
    public float brightAlpha = 1f;

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

    private string Clean(string s) => (s ?? "").Trim().Trim('\uFEFF');

    private void Awake()
    {
        // 槽位表
        slotDic.Clear();
        if (slots != null)
        {
            foreach (var s in slots)
            {
                if (s == null) continue;
                if (string.IsNullOrEmpty(s.posKey)) continue;
                if (s.renderer == null) continue;
                string key = Clean(s.posKey);
                if (!slotDic.ContainsKey(key))
                    slotDic.Add(key, s.renderer);
            }
        }

        // 角色表
        imageDic.Clear();
        if (characters != null)
        {
            foreach (var c in characters)
            {
                if (c == null) continue;
                if (string.IsNullOrEmpty(c.who)) continue;
                if (c.sprite == null) continue;
                string key = Clean(c.who);
                if (!imageDic.ContainsKey(key))
                    imageDic.Add(key, c.sprite);
            }
        }

        // 头像表
        portraitDic.Clear();
        if (portraitEntries != null)
        {
            foreach (var e in portraitEntries)
            {
                if (e == null) continue;
                if (string.IsNullOrEmpty(e.who)) continue;
                if (string.IsNullOrEmpty(e.expression)) continue;
                if (e.sprite == null) continue;

                string key = PortraitKey(Clean(e.who), Clean(e.expression));
                if (!portraitDic.ContainsKey(key))
                    portraitDic.Add(key, e.sprite);
            }
        }

        // 身体表
        bodyDic.Clear();
        if (bodyEntries != null)
        {
            foreach (var e in bodyEntries)
            {
                if (e == null) continue;
                if (string.IsNullOrEmpty(e.who)) continue;
                if (string.IsNullOrEmpty(e.expression)) continue;
                if (e.sprite == null) continue;

                string key = BodyKey(Clean(e.who), Clean(e.expression));
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

        // 初始都 dim
        DimAllSlots();

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
        return Clean(cells[idx].Replace("\r", ""));
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

    private void DimAllSlots()
    {
        foreach (var kv in slotDic)
        {
            var r = kv.Value;
            if (r == null) continue;
            r.gameObject.SetActive(true);
            r.color = new Color(1f, 1f, 1f, dimAlpha);
        }
    }

    // ===================== 核心：按 CSV position 高亮任意槽位 =====================
    public void UpdateStage(string who, string position)
    {
        if (slotDic.Count == 0) return;

        string pos = Clean(position);

        // 1) 永远先 dim，避免继承上一句
        DimAllSlots();

        // 2) 找槽位并高亮
        if (!slotDic.TryGetValue(pos, out var target) || target == null)
        {
            Debug.LogWarning($"CSV position='{pos}' 没有对应槽位，请在 slots 里配置该 posKey");
            return;
        }
        target.color = new Color(1f, 1f, 1f, brightAlpha);

        // 3) 换图（有就换）
        string w = Clean(who);
        if (imageDic.TryGetValue(w, out var sp) && sp != null)
            target.sprite = sp;
    }

    public void UpdatePortrait(string who, string portraitExpression)
    {
        if (portraitImage == null) return;

        portraitImage.gameObject.SetActive(true);

        string exp = string.IsNullOrEmpty(portraitExpression) ? "normal" : Clean(portraitExpression);
        string key = PortraitKey(Clean(who), exp);

        if (portraitDic.TryGetValue(key, out Sprite sp) && sp != null)
        {
            portraitImage.sprite = sp;
            portraitImage.enabled = true;
        }
    }

    // ✅ Body 也按任意槽位 position 走
    public void UpdateBodyExpression(string who, string position, string bodyExpression)
    {
        string pos = Clean(position);
        if (!slotDic.TryGetValue(pos, out var target) || target == null) return;

        string w = Clean(who);
        string exp = string.IsNullOrEmpty(bodyExpression) ? "normal" : Clean(bodyExpression);

        Sprite sp = null;
        if (!bodyDic.TryGetValue(BodyKey(w, exp), out sp) || sp == null)
            bodyDic.TryGetValue(BodyKey(w, "normal"), out sp);

        if (sp == null) return;

        target.sprite = sp;
    }

    public void ShowDialogRow()
    {
        if (dialogRows == null || dialogRows.Length == 0) return;

        for (int i = 0; i < dialogRows.Length; i++)
        {
            string row = Clean(dialogRows[i].Replace("\r", ""));
            if (string.IsNullOrEmpty(row)) continue;

            string[] cells = row.Split(',');
            string tag = Cell(cells, 0);

            // 普通对白：#,id,name,pos,text,nextId,(6 bodyExp),(7 portraitExp)
            if (tag == "#" && TryCellInt(cells, 1, out int id) && id == dialogIndex)
            {
                string who = Cell(cells, 2);
                string pos = Cell(cells, 3);
                string text = Cell(cells, 4);
                string bodyExp = Cell(cells, 6);
                string portraitExp = Cell(cells, 7);

                UpdateText(who, text);
                UpdateStage(who, pos);
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
            string row = Clean(dialogRows[i].Replace("\r", ""));
            if (string.IsNullOrEmpty(row)) continue;

            string[] cells = row.Split(',');
            string tag = Cell(cells, 0);
            if (tag != "&") break;

            string optionText = Cell(cells, 4);
            if (string.IsNullOrEmpty(optionText)) continue;

            if (!TryCellInt(cells, 5, out int jumpId))
            {
                Debug.LogWarning("选项 jumpId 为空/非法: " + row);
                continue;
            }

            GameObject btnGO = Instantiate(optionButton, buttonGroup);

            TMP_Text t = btnGO.GetComponentInChildren<TMP_Text>();
            if (t != null) t.text = optionText;

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
            Destroy(buttonGroup.GetChild(i).gameObject);
    }
}