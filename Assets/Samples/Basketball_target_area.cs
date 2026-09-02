using UnityEngine;

using  XRMultiplayer.MiniGames;

public class Basketball_target_area : MonoBehaviour
{
    public Minigame_Basketball basketballManager;
    public void onTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Ball"))
        {
            //Increment player score
            basketballManager.localPlayerHitTarget(10);
        }
    }
}
