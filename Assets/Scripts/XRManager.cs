
using System.Collections.Generic;
using UnityEngine;
using Unity.XR.CoreUtils;


using UnityEngine.InputSystem;


public class XRManager : MonoBehaviour
{
    // Typisierte Actions-Klasse (wird von Unity generiert) 
    private InputSystem_Actions controls;
    //public float heightSpeed = 1.0f; // Geschwindigkeit der Höhenänderung (z.B. per Stick)
    public float heightButtonSpeed1 = 0.2f; // Geschwindigkeit für Taste X/Y (langsamer)
    public float rotationSpeed = 60f; // Rotationsgeschwindigkeit in Grad pro Sekunde
    public float panSpeed = 1.0f; // Geschwindigkeit für Panning
    public GameObject objectToCycle; 
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
    private int vertebraToRemove = 0;
    
    /// <summary>
    /// Start is called before the first frame update       
    /// </summary>
    void Start()
    {
        // Controls zuweisen und Events abonnieren
        controls = new InputSystem_Actions();
        controls.XR.PrevModel.performed += ctx => PrevModel();          // X Button Pressed
        controls.XR.NextModel.performed += ctx => NextModel();          // Y Button Pressed
        controls.XR.RemoveVertebrae.performed += ctx => RemoveVertebra(vertebraToRemove++); // Remove Vertebra Button
        
        // Event für linken Stick (Move) abonnieren
        controls.XR.LeftStickMove.performed += ctx => leftStickInput = ctx.ReadValue<Vector2>();
        controls.XR.LeftStickMove.canceled += ctx => leftStickInput = Vector2.zero;
         controls.XR.LeftStickMove.Enable();
        // Event für rechten Stick (Drehung) abonnieren
        controls.XR.RightStickMove.performed += ctx => rightStickInput = ctx.ReadValue<Vector2>();
        controls.XR.RightStickMove.canceled += ctx => rightStickInput = Vector2.zero;
          controls.XR.RightStickMove.Enable();
        controls.Enable();


        xrOrigin = GetComponent<XROrigin>();
        if (xrOrigin == null)
            xrOrigin = FindFirstObjectByType<XROrigin>();

        if (xrOrigin != null && xrOrigin.CameraFloorOffsetObject != null)
        {
            var pos = xrOrigin.CameraFloorOffsetObject.transform.localPosition;
            targetHeight = pos.y;
            targetYRotation = xrOrigin.CameraFloorOffsetObject.transform.localEulerAngles.y;
            targetPan = new Vector3(pos.x, 0f, pos.z);
        }
        else
        {
            Debug.LogWarning("XROrigin or its CameraFloorOffsetObject not found. Movement/rotation using xrOrigin will be skipped until it's available.");
        }

        // Modelle aus dem GameObject "Models" im Scene-Hierarchiebaum befüllen
        GameObject modelsParent = GameObject.Find("Models");
        // Wenn kein Models-Parent vorhanden ist, fallback auf objectToCycle oder dieses GameObject
        if (modelsParent == null)
        {
            if (objectToCycle == null)
            {
                objectToCycle = this.gameObject;
            }
            modelsParent = objectToCycle;
        }

        if (modelsParent != null)
        {
            models.Clear();
            foreach (Transform child in modelsParent.transform)
            {
                models.Add(child.gameObject);
            }

            // Show first model initially (oder das aktive Kind)
            if (models.Count > 0)
            {
                // Wenn eines der Kinder aktiv ist, wähle dieses als Startindex
                int firstActive = -1;
                for (int i = 0; i < models.Count; i++)
                {
                    if (models[i] != null && models[i].activeSelf)
                    {
                        firstActive = i;
                        break;
                    }
                }

                currentModelIndex = firstActive >= 0 ? firstActive : 0;
                ShowOnlyModel(currentModelIndex);
            }
        }
    }

    /// <summary>
    /// Update is called once per frame 
    /// </summary>
    void Update()
    {
        // Polling fallback: direkten Wert der Actions pro Frame lesen (Üpolling)
        // if (controls != null)
        // {
        //     if (controls.XR.LeftStickMove.enabled)
        //         leftStickInput = controls.XR.LeftStickMove.ReadValue<Vector2>();
        //     if (controls.XR.RightStickMove.enabled)
        //         rightStickInput = controls.XR.RightStickMove.ReadValue<Vector2>();
        // }

        // Debug-Ausgabe: Thumbstick-Werte loggen
        Debug.Log($"LeftStick: {leftStickInput}, RightStick: {rightStickInput}");

        // Rechter Thumbstick: Nur links/rechts für Drehung um Y-Achse jetzt über Input System Event
        if (objectToCycle != null && Mathf.Abs(rightStickInput.x) > 0.1f)
        {
            float rotY = -rightStickInput.x * rotationSpeed * Time.deltaTime;
            objectToCycle.transform.Rotate(0f, rotY, 0f, Space.Self);
        }

        // Linker Thumbstick: Objekt im Raum bewegen (X/Z) jetzt über Input System Event
        if (objectToCycle != null && Mathf.Abs(leftStickInput.x) > 0.1f && xrOrigin != null && xrOrigin.Camera != null)
        {
            Vector3 move = new Vector3(leftStickInput.x, 0, leftStickInput.y);
            move = xrOrigin.Camera.transform.TransformDirection(move);
            move.y = 0; // Keine Höhenänderung durch Stick
            objectToCycle.transform.position += move * panSpeed * Time.deltaTime;
        }

        // Rotation direkt setzen (nur wenn xrOrigin verfügbar ist)
        if (xrOrigin != null && xrOrigin.CameraFloorOffsetObject != null)
        {
            xrOrigin.CameraFloorOffsetObject.transform.localRotation = Quaternion.Euler(0f, targetYRotation, 0f);
        }

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

    /// <summary>
    /// Entfernt einen Wirbel aus der Wirbelsäule und bewegt ihn zum Benutzer
    /// </summary>
    /// <param name="vertebraIndex">Index des Wirbels (0-basiert)</param>
    public void RemoveVertebra(int vertebraIndex)
    {
        if (currentModelIndex < 0 || currentModelIndex >= models.Count || models[currentModelIndex] == null)
        {
            Debug.LogWarning("Kein gültiges Modell ausgewählt.");
            return;
        }

        GameObject spine = models[currentModelIndex];
        vertebraIndex %= spine.transform.childCount; // Wrap around if index exceeds child count
        if (vertebraIndex < 0 || vertebraIndex >= spine.transform.childCount)
        {
            Debug.LogWarning("Ungültiger Wirbel-Index.");
            return;
        }

        Transform vertebra = spine.transform.GetChild(vertebraIndex);
        if (vertebra == null)
        {
            Debug.LogWarning("Wirbel nicht gefunden.");
            return;
        }

        // Wirbel aus der Hierarchie entfernen (aber nicht zerstören)
        vertebra.SetParent(null);

        // Position zum Benutzer berechnen (vor der Kamera)
        if (xrOrigin != null && xrOrigin.Camera != null)
        {
            Vector3 cameraPos = xrOrigin.Camera.transform.position;
            Vector3 cameraForward = xrOrigin.Camera.transform.forward;
            Vector3 targetPos = cameraPos + cameraForward * 0.5f; // 0.5m vor der Kamera

            // Bewegung starten
            StartCoroutine(MoveVertebraToUser(vertebra, targetPos));
        }
        else
        {
            Debug.LogWarning("XR Origin oder Kamera nicht gefunden.");
        }
    }

    /// <summary>
    /// Coroutine, um den Wirbel zum Benutzer zu bewegen
    /// </summary>
    private System.Collections.IEnumerator MoveVertebraToUser(Transform vertebra, Vector3 targetPos)
    {
        float duration = 1.0f; // Dauer der Bewegung in Sekunden
        float elapsed = 0f;
        Vector3 startPos = vertebra.position;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            vertebra.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        vertebra.position = targetPos;
    }
}
