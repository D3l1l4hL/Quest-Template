// Meta XR All-in-One SDK installieren letzte Version 1.3.2


using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR;

public class XROriginHeightController : MonoBehaviour
{
    //public float heightSpeed = 1.0f; // Geschwindigkeit der Höhenänderung (z.B. per Stick)
    public float heightButtonSpeed = 0.2f; // Geschwindigkeit für Taste X/Y (langsamer)
    public float rotationSpeed = 60f; // Rotationsgeschwindigkeit in Grad pro Sekunde
    public float panSpeed = 1.0f; // Geschwindigkeit für Panning
    public GameObject objectToFollow; // Das Objekt, dem gefolgt werden soll
    private XROrigin xrOrigin;
    private float targetHeight;
    private float targetYRotation;
    private Vector3 targetPan;

    void Start()
    {
        xrOrigin = GetComponent<XROrigin>();
        if (xrOrigin != null)
        {
            var pos = xrOrigin.CameraFloorOffsetObject.transform.localPosition;
            targetHeight = pos.y;
            targetYRotation = xrOrigin.CameraFloorOffsetObject.transform.localEulerAngles.y;
            targetPan = new Vector3(pos.x, 0f, pos.z);
        }
    }

    void Update()
    {
        // Rechter Thumbstick: Objekt drehen (Y-Achse: rechts/links, X-Achse: vor/zurück)
        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightHand.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 rightThumbstick))
        {
            if (objectToFollow != null)
            {
                float rotY = 0f;
                float rotX = 0f;
                if (Mathf.Abs(rightThumbstick.x) > 0.1f)
                {
                    rotY = -rightThumbstick.x * rotationSpeed * Time.deltaTime;
                }
                if (Mathf.Abs(rightThumbstick.y) > 0.1f)
                {
                    rotX = rightThumbstick.y * rotationSpeed * Time.deltaTime; // Vor/Zurück umgedreht
                }
                // Drehe das Objekt um die lokalen Achsen
                objectToFollow.transform.Rotate(rotX, rotY, 0f, Space.Self);
            }
        }

        // Linker Thumbstick: Objekt im Raum bewegen (X/Z), Taste X/Y für auf/ab (Y)
        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        bool xButton = false;
        bool yButton = false;
        leftHand.TryGetFeatureValue(CommonUsages.primaryButton, out xButton); // X-Button (meist primaryButton)
        leftHand.TryGetFeatureValue(CommonUsages.secondaryButton, out yButton); // Y-Button (meist secondaryButton)
        if (objectToFollow != null)
        {
            // Thumbstick: X/Z bewegen
            if (leftHand.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 leftThumbstick))
            {
                if (leftThumbstick.magnitude > 0.1f)
                {
                    Vector3 move = new Vector3(leftThumbstick.x, 0, leftThumbstick.y);
                    move = xrOrigin.Camera.transform.TransformDirection(move);
                    move.y = 0; // Keine Höhenänderung durch Stick
                    objectToFollow.transform.position += move * panSpeed * Time.deltaTime;
                }
            }
            // Taste X: nach unten (Y-)
            if (xButton)
            {
                objectToFollow.transform.position += Vector3.down * heightButtonSpeed * Time.deltaTime;
            }
            // Taste Y: nach oben (Y+)
            if (yButton)
            {
                objectToFollow.transform.position += Vector3.up * heightButtonSpeed * Time.deltaTime;
            }
        }



        // Keine Positions- oder Höhenänderung mehr, Tracking bleibt vollständig erhalten

        // Rotation direkt setzen
        xrOrigin.CameraFloorOffsetObject.transform.localRotation = Quaternion.Euler(0f, targetYRotation, 0f);
    }
}
