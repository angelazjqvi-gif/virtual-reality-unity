using UnityEngine;

public class BattleContext : MonoBehaviour
{
    public static BattleContext I;

    [Header("Return Info")]
    public string returnSceneName;
    public Vector3 returnPlayerPos;

    [Header("Battle Info")]
    public string[] enemyIds;
    public string backgroundId;

    void Awake()
    {
        if (I != null)
        {
            Destroy(gameObject);
            return;
        }
        I = this;
        DontDestroyOnLoad(gameObject);
    }
}

