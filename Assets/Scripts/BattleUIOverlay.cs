using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;

public class BattleUIOverlay : MonoBehaviour
{
    [Header("Text")]
    public TMP_Text speedQueueText;
    public TMP_Text hpListText;

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
        RenderUnits(all);
    }
}
