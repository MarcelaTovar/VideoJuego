using Unity.Netcode;
using UnityEngine;

namespace XRMultiplayer.MiniGames
{
    public class Minigame_Basketball : MiniGameBase
    {
        int currentPlayerScore = 0;

        [Header("Combo")]
        [SerializeField] float m_ComboWindow = 4f;
        [SerializeField] float m_ComboMultiplierStep = 0.5f;
        [SerializeField] float m_MaxComboMultiplier = 3f;

        float m_LastHitTime = -999f;
        int m_ComboCount = 0;
        float m_CurrentMultiplier = 1f;

        // 👇 ACÁ van los campos del feedback — mismo nivel que los de arriba,
        // dentro de la clase pero fuera de cualquier método.
        [Header("Feedback")]
        [SerializeField] ParticleSystem m_HitParticles;
        [SerializeField] AudioSource m_AudioSource;
        [SerializeField] AudioClip m_NormalHitClip;
        [SerializeField] AudioClip m_SwishClip;
        [SerializeField] AudioClip m_ComboClip;

        public override void StartGame()
        {
            base.StartGame();
            currentPlayerScore = 0;
            m_ComboCount = 0;
            m_CurrentMultiplier = 1f;
            m_LastHitTime = -999f;
        }

        public override void FinishGame(bool submitScore = true)
        {
            base.FinishGame(submitScore);
        }

        public void localPlayerHitTarget(int targetPoints, bool isSwish, bool isPowerShot)
        {
            Debug.Log($"localPlayerHitTarget called. GameState: {m_MiniGameManager.currentNetworkedGameState}");
    
            if (m_MiniGameManager.currentNetworkedGameState != MiniGameManager.GameState.InGame)
                return;

            if (Time.time - m_LastHitTime <= m_ComboWindow)
            {
                m_ComboCount++;
                m_CurrentMultiplier = Mathf.Min(1f + m_ComboCount * m_ComboMultiplierStep, m_MaxComboMultiplier);
            }
            else
            {
                m_ComboCount = 0;
                m_CurrentMultiplier = 1f;
            }
            m_LastHitTime = Time.time;

            int finalPoints = Mathf.RoundToInt(targetPoints * m_CurrentMultiplier);
            currentPlayerScore += finalPoints;

            m_MiniGameManager.SubmitScoreRpc(currentPlayerScore, XRINetworkPlayer.LocalPlayer.OwnerClientId);

            PlayHitFeedback(isSwish, isPowerShot, m_ComboCount); // 👈 se llama desde acá

            if (m_ComboCount == 3 || m_ComboCount == 5 || m_ComboCount == 8)
            {
                BroadcastStreakRpc(XRINetworkPlayer.LocalPlayer.playerName, m_ComboCount);
            }
        }

        [Rpc(SendTo.Everyone)]
        void BroadcastStreakRpc(string playerName, int comboCount)
        {
            PlayerHudNotification.Instance.ShowText($"🔥 {playerName} está en racha de {comboCount}!");
        }

        // 👇 Y ACÁ van los métodos, como métodos normales de la clase
        void PlayHitFeedback(bool isSwish, bool isPowerShot, int comboCount)
        {
            if (m_HitParticles != null) m_HitParticles.Play();

            AudioClip clip = comboCount >= 3 ? m_ComboClip : (isSwish ? m_SwishClip : m_NormalHitClip);
            if (m_AudioSource != null && clip != null)
                m_AudioSource.PlayOneShot(clip);

            TriggerHaptics(isPowerShot ? 0.8f : 0.4f, 0.15f);
        }

        void TriggerHaptics(float amplitude, float duration)
        {
            var devices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
                UnityEngine.XR.InputDeviceCharacteristics.Controller, devices);

            foreach (var device in devices)
            {
                if (device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse)
                {
                    device.SendHapticImpulse(0, amplitude, duration);
                }
            }
        }
    }
}