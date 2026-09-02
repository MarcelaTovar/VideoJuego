using UnityEngine;

public class Basketball_Controller : MonoBehaviour
{
    private Vector3 originPosition;
    private Rigidbody rb;
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        originPosition = this.transform.position;
    }

    public void DelayMove()
    {
        Invoke(nameof(MoveBall), 3f);
    }
    public void MoveBall()
    {
        rb.isKinematic = true;
        this.transform.position = originPosition;
    }
}
