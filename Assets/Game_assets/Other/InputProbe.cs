using UnityEngine;

public class InputProbe : MonoBehaviour
{
    void Update()
    {
        float h = Input.GetAxis("Horizon");
        float v = Input.GetAxis("Vert");
        if (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
            Debug.Log("AXIS h=" + h.ToString("F2") + " v=" + v.ToString("F2"));

        if (Input.GetButtonDown("ButtonA")) Debug.Log("BTN A (return/space)");
        if (Input.GetButtonDown("ButtonB")) Debug.Log("BTN B (escape)");
        if (Input.GetButtonDown("ButtonY")) Debug.Log("BTN Y (p)");
    }
}