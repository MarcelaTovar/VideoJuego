using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using XRMultiplayer.MiniGames;

public class Basketball_MissZone : MonoBehaviour
{
    public MiniGameManager miniGameManager;

    public void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Ball")) return;

        // 👇 nuevo: si todavía está en la mano, ignorar
        if (other.TryGetComponent<XRBaseInteractable>(out var interactable) && interactable.isSelected)
        {
            return;
        }

        bool scored = other.TryGetComponent<BallShotState>(out var shotState) && shotState.hasScored;
        Debug.Log($"¿Tiene BallShotState? {shotState != null}. ¿hasScored? {scored}");

        if (!scored)
        {
            Debug.Log("Muerte súbita: fallo detectado, terminando el nivel.");
            miniGameManager.StopGameOwnerRpc();
        }
    }
}
