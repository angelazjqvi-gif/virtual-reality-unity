using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;

public class BattleUIOverlay : MonoBehaviour
{
    [Header("Text")]
    public TMP_Text speedQueueText;
    public TMP_Text hpListText;

    [Header("Units Panel")]
    public GameObject unitsPanel;           
    public Transform unitsColumnsRoot;      
    public UnitColumnUI columnPrefab;       
    public KeyCode toggleKey = KeyCode.U;   
    public bool startHidden = true;         
    public bool onlyRenderWhenVisible = true;

    private readonly List<UnitColumnUI> _columns = new List<UnitColumnUI>();
    private List<BattleUnit> _lastAllUnits;
    private BattleUnit _lastCurrentActor;

    void Awake()
    {
        if (unitsPanel != null && startHidden)
            unitsPanel.SetActive(false);
    }

    void Update()
    {
        if (unitsPanel != null && Input.GetKeyDown(toggleKey))
        {
            ToggleUnitsPanel();
        }
    }

    public void ToggleUnitsPanel()
    {
        if (unitsPanel == null) return;

        bool next = !unitsPanel.activeSelf;
        unitsPanel.SetActive(next);

        if (next)
        {
            RenderUnitsPanel(_lastAllUnits, _lastCurrentActor);
        }
    }

    public void RenderQueue(List<BattleUnit> preview, BattleUnit currentActor)
    {
        if (speedQueueText == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("行动顺序预览:");
        for (int i = 0; i < preview.Count; i++)
        {
            var u = preview[i];
            if (u == null) continue;

            string team = u.isPlayer ? "[P]" : "[E]";
            sb.AppendLine($"{i + 1}. {team}{u.name}  SPD {u.spd}");
        }
        speedQueueText.text = sb.ToString();
    }

    public void RenderUnits(List<BattleUnit> all)
    {
        if (hpListText == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("单位列表:");
        foreach (var u in all)
        {
            if (u == null) continue;
            string team = u.isPlayer ? "[P]" : "[E]";
            sb.AppendLine($"{team}{u.name}  HP {u.hp}/{u.maxHp}  ATK {u.atk}  DEF {u.def}  SPD {u.spd}  EN {Mathf.RoundToInt(u.energy)}/{Mathf.RoundToInt(u.energyMax)}");
        }
        hpListText.text = sb.ToString();
    }

    public void RenderUnits(List<BattleUnit> all, BattleUnit currentActor)
    {
        _lastAllUnits = all;
        _lastCurrentActor = currentActor;

        bool panelReady = (unitsPanel != null && unitsColumnsRoot != null && columnPrefab != null);
        if (panelReady)
        {
            if (onlyRenderWhenVisible && !unitsPanel.activeSelf)
                return;

            RenderUnitsPanel(all, currentActor);

            // 如果你不想同时显示旧的文本列表，这里可以清空
            if (hpListText != null) hpListText.text = "";
            return;
        }

        if (hpListText == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("单位列表:");
        foreach (var u in all)
        {
            if (u == null) continue;
            string team = u.isPlayer ? "[P]" : "[E]";
            sb.AppendLine($"{team}{u.name}  HP {u.hp}/{u.maxHp}  ATK {u.atk}  DEF {u.def}  SPD {u.spd}  EN {Mathf.RoundToInt(u.energy)}/{Mathf.RoundToInt(u.energyMax)}");
        }
        hpListText.text = sb.ToString();
    }

    private void RenderUnitsPanel(List<BattleUnit> all, BattleUnit currentActor)
    {
        if (unitsColumnsRoot == null || columnPrefab == null) return;
        if (all == null) all = new List<BattleUnit>();

        // 统计非空单位
        int needed = 0;
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null) needed++;

        // 扩容对象池
        while (_columns.Count < needed)
        {
            var col = Instantiate(columnPrefab, unitsColumnsRoot);
            col.gameObject.SetActive(true);
            _columns.Add(col);
        }

        // 绑定数据（每个单位一列）
        int idx = 0;
        for (int i = 0; i < all.Count; i++)
        {
            var u = all[i];
            if (u == null) continue;

            var col = _columns[idx];
            col.gameObject.SetActive(true);
            col.Bind(u, (currentActor != null && ReferenceEquals(u, currentActor)));
            idx++;
        }

        // 多余的列隐藏
        for (int i = idx; i < _columns.Count; i++)
            _columns[i].gameObject.SetActive(false);
    }

        // Compatibility with BattleManager versions that call uiOverlay.Render(queuePreview, currentIndex, allUnits)
    public void Render(List<BattleUnit> preview, int currentIndex, List<BattleUnit> all)
    {
        BattleUnit currentActor = null;
        if (preview != null && preview.Count > 0 && currentIndex >= 0 && currentIndex < preview.Count)
            currentActor = preview[currentIndex];
        RenderAll(preview, currentActor, all);
    }

    public void RenderAll(List<BattleUnit> preview, BattleUnit currentActor, List<BattleUnit> all)
        {
            RenderQueue(preview, currentActor);
            RenderUnits(all, currentActor);

        }
}
