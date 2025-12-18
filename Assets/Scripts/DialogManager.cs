using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class DialogManager : MonoBehaviour
{
    [Header("Data")]
    public TextAsset dialogDataFile;

    [System.Serializable]
    public class CharacterData
    {
        public string characterName;
        public Sprite bodySprite;
        public Sprite portraitSprite;
    }
    public CharacterData[] characters;

    [Header("Stage Sprites")]
    public SpriteRenderer imgLeft;
    public SpriteRenderer imgRight;

    [Header("UI")]
    public Image portraitImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI dialogText;

    [Header("Buttons")]
    public Button nextButton;
    public Button optionButtonPrefab;
    public RectTransform buttonGroup;

    [Header("Visual")]
    [Range(0f,1f)] public float dimAlpha = 0.35f;

    class DialogRow
    {
        public string flag;
        public int id;
        public string name;
        public string pos;
        public string content;
        public int next;
    }

    Dictionary<int, DialogRow> rows = new Dictionary<int, DialogRow>();
    int currentId;

    void Start()
    {
        LoadCSV();
        nextButton.onClick.AddListener(OnNext);
        ShowDialog(currentId);
    }

    void LoadCSV()
    {
        rows.Clear();
        var lines = dialogDataFile.text.Split('\n');
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;
            var c = lines[i].Trim().Split(',');

            DialogRow r = new DialogRow
            {
                flag = c[0],
                id = int.Parse(c[1]),
                name = c[2],
                pos = c[3],
                content = c[4],
                next = c.Length > 5 && c[5] != "" ? int.Parse(c[5]) : -1
            };
            rows[r.id] = r;
        }
        currentId = rows.Values
    .Where(r => r.flag == "#")
    .OrderBy(r => r.id)
    .First().id;
    }

    void ShowDialog(int id)
    {
        ClearOptions();

        if (!rows.ContainsKey(id)) return;
        var r = rows[id];

        if (r.flag == "end")
        {
            nextButton.gameObject.SetActive(false);
            return;
        }

        nameText.text = r.name;
        dialogText.text = r.content;

        ApplyCharacter(r);
        ApplyPortrait(r.name);

        if (r.flag == "&")
        {
            nextButton.gameObject.SetActive(false);
            ShowOptions(id);
        }
        else
        {
            nextButton.gameObject.SetActive(true);
        }
    }

    void ApplyCharacter(DialogRow r)
    {
        var c = characters.FirstOrDefault(x => x.characterName == r.name);
        if (c == null) return;

        if (r.pos == "左")
        {
            imgLeft.sprite = c.bodySprite;
            imgLeft.color = Color.white;
            imgRight.color = new Color(1,1,1,dimAlpha);
        }
        else if (r.pos == "右")
        {
            imgRight.sprite = c.bodySprite;
            imgRight.color = Color.white;
            imgLeft.color = new Color(1,1,1,dimAlpha);
        }
    }

    void ApplyPortrait(string name)
    {
        var c = characters.FirstOrDefault(x => x.characterName == name);
        if (c == null || c.portraitSprite == null)
        {
            portraitImage.gameObject.SetActive(false);
            return;
        }

        portraitImage.sprite = c.portraitSprite;
        portraitImage.gameObject.SetActive(true);
    }

    void ShowOptions(int startId)
    {
        int id = startId;
        while (rows.ContainsKey(id) && rows[id].flag == "&")
        {
            var r = rows[id];
            Button b = Instantiate(optionButtonPrefab, buttonGroup);
            b.GetComponentInChildren<TextMeshProUGUI>().text = r.content;
            int jump = r.next;
            b.onClick.AddListener(() =>
            {
                currentId = jump;
                ShowDialog(currentId);
            });
            id++;
        }
    }

    void ClearOptions()
    {
        foreach (Transform t in buttonGroup)
            Destroy(t.gameObject);
    }

    public void OnNext()
    {
        if (!rows.ContainsKey(currentId)) return;
        currentId = rows[currentId].next;
        ShowDialog(currentId);
    }
}
