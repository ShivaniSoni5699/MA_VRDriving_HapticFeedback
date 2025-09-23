using System.Collections.Generic;
using UnityEngine;

public class CheckpointManagerOne : MonoBehaviour
{
    [Header("Car")]
    public string carRootTag = "Player";           // tag on CAR ROOT (e.g., AMG_GT with Rigidbody)

    [Header("Arrows")]
    public Transform arrowsRoot;                   // drag your "Arrows" Transform (or it will try to find by name)
    public string cpPrefix = "CP_";
    public string arrowPrefix = "Arrow_";

    readonly List<(GameObject go, Collider col)> cps = new();

    void Awake()
    {
        // ensure parent has a kinematic RB so children are one compound body (safer for physics callbacks)
        var rb = GetComponent<Rigidbody>();
        if (!rb) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true; rb.useGravity = false;

        // cache & prepare all child CP colliders, and add relays
        cps.Clear();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t == transform) continue;
            var col = t.GetComponent<Collider>();
            if (!col) continue;

            col.isTrigger = true;
            cps.Add((t.gameObject, col));

            var relay = t.GetComponent<ChildTriggerRelay>();
            if (!relay) relay = t.gameObject.AddComponent<ChildTriggerRelay>();
            relay.manager = this;
        }

        if (!arrowsRoot)
        {
            var go = GameObject.Find("Arrows");
            if (go) arrowsRoot = go.transform;
        }
    }

    // Called by each child relay when something enters its trigger
    public void OnChildTriggered(GameObject cpGO, Collider other)
    {
        // compare tag on the car ROOT (attached rigidbody root if present)
        var root = other.attachedRigidbody ? other.attachedRigidbody.transform : other.transform.root;
        if (!root.CompareTag(carRootTag)) return;

        // hide CP
        if (cpGO && cpGO.activeInHierarchy) cpGO.SetActive(false);

        // hide matching arrow
        HideMatchingArrow(cpGO ? cpGO.name : null);
    }

    void HideMatchingArrow(string cpName)
    {
        if (!arrowsRoot || string.IsNullOrEmpty(cpName)) return;

        string suf = ExtractSuffix(cpName, cpPrefix);   // CP_07 -> "07"
        string[] candidates = { arrowPrefix + Pad2(suf), arrowPrefix + suf };

        foreach (var cand in candidates)
        {
            var t = arrowsRoot.Find(cand);
            if (!t)
            {
                foreach (Transform c in arrowsRoot) { if (c.name == cand) { t = c; break; } }
            }
            if (t) { t.gameObject.SetActive(false); return; }
        }
    }

    static string ExtractSuffix(string full, string prefix)
    {
        if (!string.IsNullOrEmpty(prefix) && full.StartsWith(prefix))
            return full.Substring(prefix.Length);
        int i = full.LastIndexOf('_');
        return (i >= 0 && i < full.Length - 1) ? full.Substring(i + 1) : full;
    }
    static string Pad2(string s) => int.TryParse(s, out var n) ? n.ToString("00") : s;
}

// tiny helper added to each CP child automatically
public class ChildTriggerRelay : MonoBehaviour
{
    [HideInInspector] public CheckpointManagerOne manager;

    void OnTriggerEnter(Collider other)
    {
        if (manager) manager.OnChildTriggered(gameObject, other);
    }
}
