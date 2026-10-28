using UnityEngine;

public class DoYouReallyWantToCloseRubbingBalls: MonoBehaviour
{
    [SerializeField] private SlidePanel confirmPanel;

    public void Ask()
    {
        confirmPanel.Open();
    }
    public void Cancel()
    {
        confirmPanel.Close();
    }

    public void Confirm()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit()
#endif
    }
}