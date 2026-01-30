
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
    private int activeVertebraIndex = -1;
    private bool vertebraOut = false;
    private Transform activeVertebra = null;
    private Transform activeVertebraOriginalParent = null;
    private int activeVertebraSiblingIndex = -1;
    // Reihenfolge der Wirbel, wie vom Anwender gewünscht
    private string[] vertebraOrder = new string[] {
        "T2","T3","T4","T5","T6","T7","T8","Th_09","Th_10","Th_11","Th12",
        "L1","L2","L3","L4","l5","Kreuzbein","Steißbein"
    };
    private int orderIndex = 0; // aktueller Index in `vertebraOrder`
    private Vector3 activeVertebraLocalPos = Vector3.zero;
    private Quaternion activeVertebraLocalRot = Quaternion.identity;
    private Vector3 activeVertebraLocalScale = Vector3.one;
    private float pulledScaleMultiplier = 1.3f;
    private float pullDistance = 0.5f;
    private float pullDuration = 0.5f;
    // Simple step-by-step move settings
    public float simpleMoveDistance = 0.00025f; // move toward camera by 2.5 cm (90% reduced)
    public float minDistanceFromCamera = 0.00015f; // don't move closer than this distance to the camera
    
    /// <summary>
    /// Start is called before the first frame update       
    /// </summary>
    void Start()
    {
        // Controls zuweisen und Events abonnieren
        controls = new InputSystem_Actions();
        controls.XR.PrevModel.performed += ctx => PrevModel();          // X Button Pressed
        controls.XR.NextModel.performed += ctx => NextModel();          // Y Button Pressed
        controls.XR.RemoveVertebrae.performed += ctx => OnRemoveVertebraPressed(); // Toggle pull/retract
        
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
    /// Handler für Knopfdruck: zieht Wirbel heraus oder schiebt ihn zurück
    /// </summary>
    private void OnRemoveVertebraPressed()
    {
        // Simple toggle behaviour requested: step-by-step.
        if (currentModelIndex < 0 || currentModelIndex >= models.Count || models[currentModelIndex] == null)
            return;

        GameObject spine = models[currentModelIndex];
        int childCount = spine.transform.childCount;
        if (childCount == 0)
            return;

        // Use vertebraOrder to find the next vertebra by name
        if (activeVertebra == null)
        {
            // Find next vertebra in the specified order
            if (vertebraToRemove >= vertebraOrder.Length)
            {
                Debug.Log("Alle Wirbel in der Reihenfolge wurden bearbeitet.");
                return;
            }

            string targetName = vertebraOrder[vertebraToRemove];
            Transform found = null;
            for (int i = 0; i < childCount; i++)
            {
                var child = spine.transform.GetChild(i);
                if (NamesMatch(child.name, targetName))
                {
                    found = child;
                    break;
                }
            }

            if (found != null)
            {
                Debug.Log($"Found vertebra '{found.name}' at order index {vertebraToRemove}");
                StartCoroutine(MoveVertebraOutCoroutine(found));
            }
            else
            {
                Debug.LogWarning($"Vertebra '{targetName}' not found, skipping to next.");
                vertebraToRemove++;
            }
        }
        else
        {
            // Return current vertebra to original position
            StartCoroutine(MoveVertebraBackCoroutine(activeVertebra, activeVertebraLocalPos));
        }
    }

    private System.Collections.IEnumerator PullOutVertebraCoroutine(Transform vertebra, int index)
    {
        activeVertebra = vertebra;
        activeVertebraIndex = index;
        activeVertebraOriginalParent = vertebra.parent;
        activeVertebraSiblingIndex = vertebra.GetSiblingIndex();
        activeVertebraLocalPos = vertebra.localPosition;
        activeVertebraLocalRot = vertebra.localRotation;
        activeVertebraLocalScale = vertebra.localScale;

        Debug.Log($"Pull: saved parent={activeVertebraOriginalParent?.name}, siblingIndex={activeVertebraSiblingIndex}, localPos={activeVertebraLocalPos}");

        vertebra.SetParent(null);

        if (xrOrigin == null || xrOrigin.Camera == null)
            yield break;

        Vector3 cameraPos = xrOrigin.Camera.transform.position;
        Vector3 cameraForward = xrOrigin.Camera.transform.forward;
        Vector3 targetPos = cameraPos + cameraForward * pullDistance;

        Vector3 startPos = vertebra.position;
        Vector3 startScale = vertebra.localScale;
        Vector3 targetScale = startScale * pulledScaleMultiplier;

        float elapsed = 0f;
        while (elapsed < pullDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / pullDuration);
            vertebra.position = Vector3.Lerp(startPos, targetPos, t);
            vertebra.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        vertebra.position = targetPos;
        vertebra.localScale = targetScale;
        vertebraOut = true;
    }

    private System.Collections.IEnumerator MoveVertebraOutCoroutine(Transform vertebra)
    {
        if (vertebra == null)
            yield break;

        activeVertebra = vertebra;
        activeVertebraIndex = vertebraToRemove;
        activeVertebraLocalPos = activeVertebra.localPosition;

        Transform parentT = activeVertebra.parent;
        if (parentT == null)
            yield break;

        Vector3 worldStart = parentT.TransformPoint(activeVertebraLocalPos);
        Vector3 camPos = (xrOrigin != null && xrOrigin.Camera != null) ? xrOrigin.Camera.transform.position : Vector3.zero;
        Vector3 dirToCam = (xrOrigin != null && xrOrigin.Camera != null) ? (camPos - worldStart).normalized : parentT.forward;

        // Berechne Wirbelbreite über seine Bounds (= eine Wirbelbreite)
        float vertebraWidth = 0.05f; // Fallback
        Renderer rend = vertebra.GetComponent<Renderer>();
        if (rend != null)
        {
            Bounds bounds = rend.bounds;
            // Nutze die Ausdehnung in Richtung zur Kamera als Wirbelbreite
            vertebraWidth = Vector3.Dot(bounds.extents * 2f, dirToCam);
            vertebraWidth = Mathf.Abs(vertebraWidth);
        }

        Vector3 worldTarget = worldStart + dirToCam * vertebraWidth;

        // clamp to not cross too near to camera
        if (xrOrigin != null && xrOrigin.Camera != null)
        {
            float dist = Vector3.Distance(worldTarget, camPos);
            if (dist < minDistanceFromCamera)
                worldTarget = camPos + dirToCam * minDistanceFromCamera;
        }

        float elapsed = 0f;
        while (elapsed < pullDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / pullDuration));
            Vector3 worldPos = Vector3.Lerp(worldStart, worldTarget, t);
            activeVertebra.localPosition = parentT.InverseTransformPoint(worldPos);
            yield return null;
        }

        activeVertebra.localPosition = parentT.InverseTransformPoint(worldTarget);
        vertebraOut = true;
    }

    private System.Collections.IEnumerator MoveVertebraBackCoroutine(Transform vertebra, Vector3 originalLocal)
    {
        if (vertebra == null)
            yield break;

        Transform parentT = vertebra.parent;
        if (parentT == null)
            yield break;

        Vector3 startWorld = parentT.TransformPoint(vertebra.localPosition);
        Vector3 targetWorld = parentT.TransformPoint(originalLocal);

        float elapsed = 0f;
        while (elapsed < pullDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / pullDuration));
            Vector3 worldPos = Vector3.Lerp(startWorld, targetWorld, t);
            vertebra.localPosition = parentT.InverseTransformPoint(worldPos);
            yield return null;
        }

        vertebra.localPosition = originalLocal;
        Debug.Log($"Returned vertebra '{vertebra.name}' to original position.");
        vertebraOut = false;
        activeVertebra = null;
        activeVertebraIndex = -1;
        vertebraToRemove++;
    }

    private bool NamesMatch(string a, string b)
    {
        return NormalizeName(a) == NormalizeName(b);
    }

    private string NormalizeName(string s)
    {
        if (string.IsNullOrEmpty(s))
            return string.Empty;

        // Remove spaces and common separators
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (char.IsLetterOrDigit(c))
            {
                // If digit sequence, normalize number (remove leading zeros)
                if (char.IsDigit(c))
                {
                    int j = i;
                    while (j < s.Length && char.IsDigit(s[j])) j++;
                    string num = s.Substring(i, j - i);
                    // parse to int to remove leading zeros, but guard large numbers
                    if (num.Length > 0)
                    {
                        int parsed = 0;
                        if (int.TryParse(num, out parsed))
                        {
                            sb.Append(parsed.ToString());
                        }
                        else
                        {
                            // fallback: append digits as-is
                            sb.Append(num);
                        }
                    }
                    i = j;
                    continue;
                }
                else
                {
                    sb.Append(char.ToLowerInvariant(c));
                }
            }
            i++;
        }

        return sb.ToString();
    }

    private System.Collections.IEnumerator RetractVertebraCoroutine(Transform vertebra, int index)
    {
        if (vertebra == null || activeVertebraOriginalParent == null)
            yield break;

        Vector3 startPos = vertebra.position;
        Vector3 startScale = vertebra.localScale;
        Vector3 targetScale = activeVertebraLocalScale;
        Vector3 targetWorldPos = activeVertebraOriginalParent.TransformPoint(activeVertebraLocalPos);

        float elapsed = 0f;
        while (elapsed < pullDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / pullDuration);
            vertebra.position = Vector3.Lerp(startPos, targetWorldPos, t);
            vertebra.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        vertebra.SetParent(activeVertebraOriginalParent);
        if (activeVertebraSiblingIndex >= 0)
            vertebra.SetSiblingIndex(activeVertebraSiblingIndex);
        vertebra.localPosition = activeVertebraLocalPos;
        vertebra.localRotation = activeVertebraLocalRot;
        vertebra.localScale = activeVertebraLocalScale;

        Debug.Log($"Retract: restored parent={activeVertebraOriginalParent?.name}, siblingIndex={activeVertebraSiblingIndex}, localPos={activeVertebraLocalPos}");

        vertebraOut = false;
        activeVertebra = null;
        activeVertebraOriginalParent = null;
        activeVertebraSiblingIndex = -1;
        activeVertebraIndex = -1;

        // Nach Zurückschieben: weiter zum nächsten Eintrag in der gewünschten Reihenfolge
        orderIndex++;
        vertebraToRemove = orderIndex;
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
