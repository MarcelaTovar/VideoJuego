using UnityEngine;
using XRMultiplayer.MiniGames;

public class Basketball_target_area : MonoBehaviour
{
    public Minigame_Basketball basketballManager;

    [Header("Shot Quality")]
    [SerializeField] Collider m_SwishCollider; // collider chico, justo dentro del aro
    [SerializeField] float m_PowerShotVelocityThreshold = 6f;

    public void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Trigger entered by: {other.name}, tag: {other.tag}");
        if (!other.CompareTag("Ball")) return;

        int basePoints = 10;
        bool isSwish = m_SwishCollider != null && m_SwishCollider.bounds.Contains(other.transform.position);
        float speed = other.attachedRigidbody != null ? other.attachedRigidbody.linearVelocity.magnitude : 0f;
        bool isPowerShot = speed >= m_PowerShotVelocityThreshold;

        int bonus = 0;
        if (isSwish) bonus += 5;
        if (isPowerShot) bonus += 10;

        basketballManager.localPlayerHitTarget(basePoints + bonus, isSwish, isPowerShot);
    }
}