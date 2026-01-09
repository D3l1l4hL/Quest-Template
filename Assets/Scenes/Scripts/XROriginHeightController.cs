// Meta XR All-in-One SDK installieren letzte Version 1.3.2


using UnityEngine;
using Unity.XR.CoreUtils;
using UnityEngine.XR;

public class XROriginHeightController : MonoBehaviour
{
    public float heightSpeed = 1.0f; // Geschwindigkeit der Höhenänderung
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
                    rotY = rightThumbstick.x * rotationSpeed * Time.deltaTime;
                }
                if (Mathf.Abs(rightThumbstick.y) > 0.1f)
                {
                    rotX = -rightThumbstick.y * rotationSpeed * Time.deltaTime;
                }
                // Drehe das Objekt um die lokalen Achsen
                objectToFollow.transform.Rotate(rotX, rotY, 0f, Space.Self);
            }
        }

        // Linker Thumbstick: Skalierung des Objekts auf der X-Achse (links/rechts)
        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftHand.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 leftThumbstick))
        {
            if (objectToFollow != null)
            {
                if (Mathf.Abs(leftThumbstick.x) > 0.1f)
                {
                    Vector3 scale = objectToFollow.transform.localScale;
                    float scaleChange = leftThumbstick.x * Time.deltaTime;
                    scale.x = Mathf.Max(0.01f, scale.x + scaleChange); // Mindestgröße verhindern
                    objectToFollow.transform.localScale = scale;
                }
            }
            // ...optional: alte Rotation um Y-Achse für xrOrigin entfernen, falls nicht mehr benötigt...
        }



        // Keine Positions- oder Höhenänderung mehr, Tracking bleibt vollständig erhalten

        // Rotation direkt setzen
        xrOrigin.CameraFloorOffsetObject.transform.localRotation = Quaternion.Euler(0f, targetYRotation, 0f);
    }
}
