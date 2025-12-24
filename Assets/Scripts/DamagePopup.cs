using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    [Header("Text")]
    public TMP_Text text;

    [Header("Normal")]
    public Color normalColor = Color.white;
    public float normalScale = 1f;

    [Header("Critical")]
    public Color critColor = Color.red;
    public float critScale = 1.3f;
    public string critPrefix = "暴击 ";

    [Header("Motion")]
    public float lifeTime = 0.8f;
    public float floatSpeed = 40f;

    [Header("Heal")]
    public Color healColor = Color.green;
    public float healScale = 1.1f;
    public string healPrefix = "+";

    [Header("Buff")]
    public Color buffColor = new Color(0.2f, 0.85f, 1f, 1f);
    public float buffScale = 1.05f;

    private float timer;

    public void Setup(int damage, bool isCrit)
    {
        if (text == null) return;

        if (isCrit)
            text.text = critPrefix + "-" + damage;
        else
            text.text = "-" + damage;

        text.color = isCrit ? critColor : normalColor;
        transform.localScale = Vector3.one * (isCrit ? critScale : normalScale);
        timer = 0f;
    }

    public void SetupHeal(int healAmount)
    {
        if (text == null) return;

        if (healAmount < 0) healAmount = -healAmount;
        if (healAmount < 1) healAmount = 1;

        text.text = healPrefix + healAmount;
        text.color = healColor;
        transform.localScale = Vector3.one * healScale;
        timer = 0f;
    }

    // Generic text popup (e.g., ATK+10)
    public void SetupText(string content)
    {
        SetupText(content, buffColor, buffScale);
    }

    public void SetupText(string content, Color color, float scale)
    {
        if (text == null) return;

        text.text = content;
        text.color = color;
        transform.localScale = Vector3.one * scale;
        timer = 0f;
    }

    void Update()
    {
        transform.position += Vector3.up * floatSpeed * Time.deltaTime;

        timer += Time.deltaTime;
        if (timer >= lifeTime)
        {
            Destroy(gameObject);
        }
    }
}
