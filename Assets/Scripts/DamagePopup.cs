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

    private float timer;

    // ⭐ 唯一入口
    public void Setup(int damage, bool isCrit)
    {
        // 1. 文本
        if (isCrit)
            text.text = critPrefix +"-"+ damage;
        else
            text.text = "-" + damage.ToString();

        // 2. 颜色（关键）
        text.color = isCrit ? critColor : normalColor;

        // 3. 缩放
        transform.localScale = Vector3.one * (isCrit ? critScale : normalScale);

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
