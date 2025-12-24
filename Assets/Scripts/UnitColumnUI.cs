using UnityEngine;
using TMPro;

public class UnitColumnUI : MonoBehaviour
{
    [Header("Texts")]
    public TMP_Text nameText;
    public TMP_Text hpText;
    public TMP_Text atkText;
    public TMP_Text defText;
    public TMP_Text spdText;
    public TMP_Text enText;

    public void Bind(BattleUnit u, bool isCurrent)
    {
        if (u == null) return;

        string team = u.isPlayer ? "[P]" : "[E]";
        if (nameText) nameText.text = $"{team}{u.name}{(isCurrent ? "  <b>(当前)</b>" : "")}";
        if (hpText)  hpText.text  = $"HP  {u.hp}/{u.maxHp}";
        if (atkText) atkText.text = $"ATK {u.atk}";
        if (defText) defText.text = $"DEF {u.def}";
        if (spdText) spdText.text = $"SPD {u.spd}";
        if (enText)  enText.text  = $"EN  {Mathf.RoundToInt(u.energy)}/{Mathf.RoundToInt(u.energyMax)}";
    }
}
