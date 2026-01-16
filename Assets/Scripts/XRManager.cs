
using System.Collections.Generic;
using UnityEngine;
using Unity.XR.CoreUtils;


using UnityEngine.InputSystem;


public class XRManager : MonoBehaviour
{
    // Typisierte Actions-Klasse (wird von Unity generiert)
    private InputSystem_Actions controls;
    //public float heightSpeed = 1.0f; // Geschwindigkeit der Höhenänderung (z.B. per Stick)
    public float heightButtonSpeed = 0.2f; // Geschwindigkeit für Taste X/Y (langsamer)
    public float rotationSpeed = 60f; // Rotationsgeschwindigkeit in Grad pro Sekunde
    public float panSpeed = 1.0f; // Geschwindigkeit für Panning
    public GameObject objectToFollow; 
    public List<GameObject> models = new List<GameObject>();
    private int currentModelIndex = -1;
    private bool prevPrimaryButton = false;
    private bool prevSecondaryButton = false;
    private XROrigin xrOrigin;
    private float targetHeight;
    private float targetYRotation;
    private Vector3 targetPan;

    private Vector2 leftStickInput = Vector2.zero;
    private Vector2 rightStickInput = Vector2.zero;
    
    /// <summary>
    /// Start is called before the first frame update       
    /// </summary>
    void Start()
    {
        // Controls zuweisen und Events abonnieren
        controls = new InputSystem_Actions();
        controls.XR.PrevModel.performed += ctx => PrevModel();
        controls.XR.NextModel.performed += ctx => NextModel();
        // Event für linken Stick (Move) abonnieren
        controls.XR.LeftStickMove.performed += ctx => leftStickInput = ctx.ReadValue<Vector2>();
        controls.XR.LeftStickMove.canceled += ctx => leftStickInput = Vector2.zero;
        // Event für rechten Stick (Drehung) abonnieren
        controls.XR.RightStickMove.performed += ctx => rightStickInput = ctx.ReadValue<Vector2>();
        controls.XR.RightStickMove.canceled += ctx => rightStickInput = Vector2.zero;
        controls.Enable();


        xrOrigin = GetComponent<XROrigin>();
        if (xrOrigin != null)
        {
            var pos = xrOrigin.CameraFloorOffsetObject.transform.localPosition;
            targetHeight = pos.y;
            targetYRotation = xrOrigin.CameraFloorOffsetObject.transform.localEulerAngles.y;
            targetPan = new Vector3(pos.x, 0f, pos.z);
        }

        // Modelle aus dem GameObject "Models" im Scene-Hierarchiebaum befüllen
        GameObject modelsParent = GameObject.Find("Models");
        if (modelsParent != null)
        {
            models.Clear();
            foreach (Transform child in modelsParent.transform)
            {
                models.Add(child.gameObject);
            }

            // Show first model initially
            if (models.Count > 0)
            {
                currentModelIndex = 0;
                ShowOnlyModel(0);
            }
        }
        else
        {
            Debug.LogWarning("Kein GameObject 'Models' gefunden!");
        }
    }

    /// <summary>
    /// Update is called once per frame 
    /// </summary>
    void Update()
    {
        // Rechter Thumbstick: Nur links/rechts für Drehung um Y-Achse jetzt über Input System Event
        if (objectToFollow != null && Mathf.Abs(rightStickInput.x) > 0.1f)
        {
            float rotY = -rightStickInput.x * rotationSpeed * Time.deltaTime;
            objectToFollow.transform.Rotate(0f, rotY, 0f, Space.Self);
        }

        // Linker Thumbstick: Objekt im Raum bewegen (X/Z) jetzt über Input System Event
        if (objectToFollow != null && leftStickInput.magnitude > 0.1f)
        {
            Vector3 move = new Vector3(leftStickInput.x, 0, leftStickInput.y);
            move = xrOrigin.Camera.transform.TransformDirection(move);
            move.y = 0; // Keine Höhenänderung durch Stick
            objectToFollow.transform.position += move * panSpeed * Time.deltaTime;
        }


        // Rotation direkt setzen
        xrOrigin.CameraFloorOffsetObject.transform.localRotation = Quaternion.Euler(0f, targetYRotation, 0f);

    }

    /// <summary>
    /// Zeigt nur das Modell am angegebenen Index an, alle anderen werden ausgeblendet
    /// </summary>
    private void ShowOnlyModel(int index)
    {
        for (int i = 0; i < models.Count; i++)
        {
            if (models[i] != null)
                models[i].SetActive(i == index);
        }
    }

    /// <summary>
    /// Zeigt alle Modelle an
    /// </summary>
    private void ShowAllModels()
    {
        for (int i = 0; i < models.Count; i++)
        {
            if (models[i] != null)
                models[i].SetActive(true);
        }
    }

    /// <summary>
    /// Zeigt das nächste Modell an oder alle Modelle, wenn das Ende erreicht ist
    /// </summary>
    private void NextModel()
    {
        currentModelIndex++;
        if (currentModelIndex >= models.Count)
        {
            ShowAllModels();
            currentModelIndex = -1;
        }
        else
        {
            ShowOnlyModel(currentModelIndex);
        }
    }

    /// <summary>
    /// Zeigt das vorherige Modell an oder alle Modelle, wenn am Anfang
    /// </summary>
    private void PrevModel()
    {
        if (currentModelIndex == -1)
        {
            currentModelIndex = models.Count - 1;
            ShowOnlyModel(currentModelIndex);
        }
        else
        {
            currentModelIndex--;
            if (currentModelIndex < 0)
            {
                ShowAllModels();
                currentModelIndex = -1;
            }
            else
            {
                ShowOnlyModel(currentModelIndex);
            }
        }
    }
}
