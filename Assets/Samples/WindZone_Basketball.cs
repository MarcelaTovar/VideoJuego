using UnityEngine;

public class WindZone_Basketball : MonoBehaviour
{
    [Header("Wind Settings")]
    [SerializeField] float m_WindStrength = 2f;
    [SerializeField] float m_ChangeInterval = 4f;

    [Header("Only active during game")]
    [SerializeField] bool m_OnlyDuringGame = true;
    [SerializeField] MonoBehaviour m_MiniGameManagerRef; // arrastrá el MiniGameManager de esta cancha

    [Header("Visual Indicator (opcional)")]
    [SerializeField] Transform m_WindIndicator; // ej. una veleta o flecha 3D

    Vector3 m_CurrentWind;
    float m_Timer;

    void Update()
    {
        if (m_OnlyDuringGame && m_MiniGameManagerRef != null)
        {
            var manager = m_MiniGameManagerRef as XRMultiplayer.MiniGames.MiniGameManager;
            if (manager != null && manager.currentNetworkedGameState != XRMultiplayer.MiniGames.MiniGameManager.GameState.InGame)
            {
                return;
            }
        }

        m_Timer += Time.deltaTime;
        if (m_Timer >= m_ChangeInterval)
        {
            m_Timer = 0f;
            m_CurrentWind = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized * m_WindStrength;
            Debug.Log($"Nuevo viento: {m_CurrentWind}, indicador asignado: {m_WindIndicator != null}"); // 👈 nuevo

            if (m_WindIndicator != null && m_CurrentWind.sqrMagnitude > 0.01f)
            {
                m_WindIndicator.rotation = Quaternion.LookRotation(m_CurrentWind);
            }
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Ball")) return;
        if (other.attachedRigidbody == null || other.attachedRigidbody.isKinematic) return;

        // Solo el cliente que controla físicamente esta pelota debería aplicar la fuerza
        if (other.attachedRigidbody.TryGetComponent<Unity.Netcode.NetworkObject>(out var netObj))
        {
            if (!netObj.IsOwner) return;
        }

        other.attachedRigidbody.AddForce(m_CurrentWind, ForceMode.Force);
    }
}