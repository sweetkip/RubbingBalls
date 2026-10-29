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
        Application.Quit();
    }
}