using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;

public class BattleUIOverlay : MonoBehaviour
{
    public TMP_Text speedQueueText;
    public TMP_Text hpListText;

    public void Render(List<BattleUnit> queue, int currentIndex, List<BattleUnit> all)
    {
        if (speedQueueText != null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("行动顺序（SPD）:");
            for (int i = 0; i < queue.Count; i++)
            {
                var u = queue[i];
                if (u == null) continue;
                string mark = (i == currentIndex) ? "👉 " : "   ";
                sb.AppendLine($"{mark}{u.name}  SPD={u.spd}");
            }
            speedQueueText.text = sb.ToString();
        }

        if (hpListText != null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("全体血量:");
            foreach (var u in all)
            {
                if (u == null) continue;
                string team = u.isPlayer ? "[P]" : "[E]";
                sb.AppendLine($"{team}{u.name}  HP {u.hp}/{u.maxHp}");
            }
            hpListText.text = sb.ToString();
        }
    }
}
