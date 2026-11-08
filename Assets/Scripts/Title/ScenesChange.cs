using UnityEngine;

/// <summary>
/// Title menu buttons call GoToLoadScene with the old scene names. Routed through Yowol.GameFlow:
/// "MainStageSecen" (play) starts a new game at the Intro, "RoadViewScene" (load) continues the last stage.
/// </summary>
public class ScenesChange : MonoBehaviour
{
    public void GoToLoadScene(string sceneName)
    {
        switch (sceneName)
        {
            case "MainStageSecen":
            case "Intro":
                Yowol.GameFlow.NewGame();
                break;
            case "RoadViewScene":
            case "Continue":
                Yowol.GameFlow.Load(Yowol.GameFlow.HasContinue ? "Continue" : Yowol.GameFlow.Intro);
                break;
            default:
                Yowol.GameFlow.Load(sceneName);
                break;
        }
    }
}
