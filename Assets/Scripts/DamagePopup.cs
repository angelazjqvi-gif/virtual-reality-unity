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

    [Header("Heal (NEW)")]
    public Color healColor = Color.green;
    public float healScale = 1.1f;
    public string healPrefix = "+";

    private float timer;

    public void Setup(int damage, bool isCrit)
    {
        if (isCrit)
            text.text = critPrefix +"-"+ damage;
        else
            text.text = "-" + damage.ToString();

        text.color = isCrit ? critColor : normalColor;

        transform.localScale = Vector3.one * (isCrit ? critScale : normalScale);

        timer = 0f;
    }
    public void SetupHeal(int healAmount)
    {
        if (healAmount < 0) healAmount = -healAmount; // 容错
        if (healAmount < 1) healAmount = 1;

        text.text = healPrefix + healAmount.ToString();
        text.color = healColor;
        transform.localScale = Vector3.one * healScale;

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
