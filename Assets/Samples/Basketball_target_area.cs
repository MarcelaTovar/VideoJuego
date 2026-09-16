using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRMultiplayer.MiniGames;

public class Basketball_target_area : MonoBehaviour
{
    public Minigame_Basketball basketballManager;

    [Header("Shot Quality")]
    [SerializeField] Collider m_SwishCollider;
    [SerializeField] float m_PowerShotVelocityThreshold = 6f;
    [SerializeField] float m_MinReleaseSpeed = 1.5f; 

    public void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball")) return;

        
        if (other.TryGetComponent<XRBaseInteractable>(out var interactable) && interactable.isSelected)
        {
            return;
        }

        
        float speed = other.attachedRigidbody != null ? other.attachedRigidbody.linearVelocity.magnitude : 0f;
        if (speed < m_MinReleaseSpeed)
        {
            return;
        }

        int basePoints = 10;
        bool isSwish = m_SwishCollider != null && m_SwishCollider.bounds.Contains(other.transform.position);
        bool isPowerShot = speed >= m_PowerShotVelocityThreshold;

        int bonus = 0;
        if (isSwish) bonus += 5;
        if (isPowerShot) bonus += 10;

        basketballManager.localPlayerHitTarget(basePoints + bonus, isSwish, isPowerShot);
    }
}