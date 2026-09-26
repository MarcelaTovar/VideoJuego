using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;              // 👈 nuevo, para SelectEnterEventArgs
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class BallShotState : MonoBehaviour
{
    public bool hasScored = false;

    XRBaseInteractable m_Interactable;

    void Awake()
    {
        TryGetComponent(out m_Interactable);
    }

    void OnEnable()
    {
        if (m_Interactable != null)
            m_Interactable.selectEntered.AddListener(OnGrabbed);
    }

    void OnDisable()
    {
        if (m_Interactable != null)
            m_Interactable.selectEntered.RemoveListener(OnGrabbed);
    }

    void OnGrabbed(SelectEnterEventArgs args)
{
    hasScored = false;
    Debug.Log("Pelota agarrada, hasScored reseteado a false");
}
}