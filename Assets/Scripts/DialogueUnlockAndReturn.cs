using UnityEngine;
using UnityEngine.SceneManagement;

public class DialogueUnlockAndReturn : MonoBehaviour
{
    public DialogManager dialog;     
    public int unlockPartyIndex = 2; 
    public bool returnToWorld = true;

    bool done;

    void Update()
    {
        if (done || dialog == null) return;

        if (dialog.next != null && !dialog.next.gameObject.activeInHierarchy)
        {
            done = true;
            DoUnlockAndReturn();
        }
    }

    public void DoUnlockAndReturn()
    {
        if (GameSession.I != null)
            GameSession.I.UnlockPartyMember(unlockPartyIndex);

        if (!returnToWorld) return;
        if (GameSession.I != null && !string.IsNullOrEmpty(GameSession.I.targetWorldScene))
            SceneManager.LoadScene(GameSession.I.targetWorldScene);
    }
}
