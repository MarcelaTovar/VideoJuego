using UnityEngine;

public class MovingHoop : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float m_SpeedX = 1f;
    [SerializeField] float m_SpeedY = 0.6f;
    [SerializeField] float m_RangeX = 1.5f;
    [SerializeField] float m_RangeY = 0.5f;

    [Header("Optional: pause when nobody's playing")]
    [SerializeField] bool m_OnlyMoveDuringGame = true;
    [SerializeField] MonoBehaviour m_MiniGameManagerRef; // arrastrá el MiniGameManager de esta cancha

    Vector3 m_StartPos;

    void Awake()
    {
        m_StartPos = transform.position;
    }

    void Update()
    {
        if (m_OnlyMoveDuringGame && m_MiniGameManagerRef != null)
        {
            var manager = m_MiniGameManagerRef as XRMultiplayer.MiniGames.MiniGameManager;
            if (manager != null && manager.currentNetworkedGameState != XRMultiplayer.MiniGames.MiniGameManager.GameState.InGame)
            {
                return; // no se mueve en PreGame/PostGame
            }
        }

        float x = Mathf.Sin(Time.time * m_SpeedX) * m_RangeX;
        float y = Mathf.Sin(Time.time * m_SpeedY) * m_RangeY;
        transform.position = m_StartPos + new Vector3(x, y, 0);
    }
}