using System.Collections;
using System.Reflection;
using UnityEngine;
using TMPro;   // 顶部加上


public class HealTrigger : MonoBehaviour
{
    [Header("Trigger")]
    public string playerTag = "Player";

    [Header("Heal")]
    public bool healWholeParty = true;
    public bool oneTime = false;

    public TMP_Text hintText;        // Inspector 拖 HealHintText 进来
    public float hintDuration = 1.5f;


    bool used;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (used && oneTime) return;

        // 保险：如果碰到的是玩家子物体，也能识别到
        Transform root = other.attachedRigidbody ? other.attachedRigidbody.transform : other.transform.root;
        if (!root.CompareTag(playerTag)) return;

        bool healed = false;

        // 1) 如果玩家物体（或父物体）身上有 BattleUnit，就把它回满
        var bu = root.GetComponentInChildren<BattleUnit>();
        if (bu != null)
        {
            bu.hp = bu.maxHp;
            healed = true;
        }

        // 2) 同时尝试把 GameSession 里的 Party 回满（不依赖你具体字段名，尽量不报错）
        healed |= TryHealGameSessionParty();

        if (!healed)
        {
            Debug.LogWarning("[HealTrigger] 触发到了，但没找到可回血对象：玩家身上没有 BattleUnit，或 GameSession/Party 没找到。");
            return;
        }
        ShowHint("已完成回血");


        if (oneTime)
        {
            used = true;
            // 你要一次性就禁用碰撞体（更直观）
            var col = GetComponent<Collider2D>();
            if (col) col.enabled = false;
        }
    }

    bool TryHealGameSessionParty()
    {
        // GameSession.I 不存在就直接跳过
        var gsType = typeof(GameSession);
        var instProp = gsType.GetProperty("I", BindingFlags.Public | BindingFlags.Static);
        var instField = gsType.GetField("I", BindingFlags.Public | BindingFlags.Static);

        object gs = instProp != null ? instProp.GetValue(null) : (instField != null ? instField.GetValue(null) : null);
        if (gs == null) return false;

        // 找 party 字段/属性（不确定你叫 party / Party）
        object partyObj = null;

        var partyField = gsType.GetField("party", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (partyField != null) partyObj = partyField.GetValue(gs);

        if (partyObj == null)
        {
            var partyProp = gsType.GetProperty("party", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (partyProp != null) partyObj = partyProp.GetValue(gs);
        }

        if (partyObj == null) return false;

        // party 通常是 List/数组，都能当 IEnumerable 处理
        var enumerable = partyObj as IEnumerable;
        if (enumerable == null) return false;

        bool any = false;

        foreach (var member in enumerable)
        {
            if (member == null) continue;

            // 找 baseMaxHp / currentHp（不确定大小写）
            var mt = member.GetType();

            var baseMax = GetIntLike(member, mt, "baseMaxHp");
            if (baseMax <= 0) baseMax = GetIntLike(member, mt, "BaseMaxHp"); // 再试一次

            if (baseMax <= 0) continue;

            // currentHp = baseMaxHp
            if (SetIntLike(member, mt, "currentHp", baseMax) ||
                SetIntLike(member, mt, "CurrentHp", baseMax))
            {
                any = true;
            }

            if (!healWholeParty) break;
        }

        return any;
    }

    int GetIntLike(object obj, System.Type t, string name)
    {
        var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (f != null)
        {
            object v = f.GetValue(obj);
            if (v is int i) return i;
            if (v is float ff) return Mathf.RoundToInt(ff);
        }

        var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (p != null)
        {
            object v = p.GetValue(obj);
            if (v is int i) return i;
            if (v is float ff) return Mathf.RoundToInt(ff);
        }

        return 0;
    }

    bool SetIntLike(object obj, System.Type t, string name, int value)
    {
        var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (f != null)
        {
            if (f.FieldType == typeof(int)) { f.SetValue(obj, value); return true; }
            if (f.FieldType == typeof(float)) { f.SetValue(obj, (float)value); return true; }
        }

        var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (p != null && p.CanWrite)
        {
            if (p.PropertyType == typeof(int)) { p.SetValue(obj, value); return true; }
            if (p.PropertyType == typeof(float)) { p.SetValue(obj, (float)value); return true; }
        }

        return false;
    }

    Coroutine hintCo;

    void ShowHint(string msg)
    {
        if (hintText == null)
        {
            Debug.LogError("[HealTrigger] hintText is NULL (你没绑上TMP_Text)");
            return;
        }

        if (hintCo != null) StopCoroutine(hintCo);
        hintCo = StartCoroutine(HintRoutine(msg));
    }

    IEnumerator HintRoutine(string msg)
    {
        hintText.gameObject.SetActive(true);
        hintText.text = msg;
        yield return new WaitForSeconds(hintDuration);
        hintText.gameObject.SetActive(false);
    }
}
