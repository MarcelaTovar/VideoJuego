using UnityEngine;

namespace XRMultiplayer.MiniGames
{

public class Minigame_Basketball : MiniGameBase

{
    int currentPlayerScore = 0;
    public override void StartGame()
    {
        base.StartGame();
        // reset current player score
        currentPlayerScore = 0;
    }
    public override void FinishGame(bool submitScore = true)
    {
        base.FinishGame(submitScore);
        // show final score
    }

    //Creating a method for player score

    public void localPlayerHitTarget(int targetPoints)
    {
        if(m_MiniGameManager.currentNetworkedGameState == MiniGameManager.GameState.InGame)
        {
            currentPlayerScore += targetPoints;
            m_MiniGameManager.SubmitScoreRpc(currentPlayerScore,XRINetworkPlayer.LocalPlayer.OwnerClientId);
        }
    }
}
}
