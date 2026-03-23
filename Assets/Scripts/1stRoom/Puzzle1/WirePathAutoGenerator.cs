using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// WirePathAutoGenerator.cs — Unity 6000.3.9f1
/// Samples wire mesh, builds ordered path, lets you pick start AND end in Scene view.
/// </summary>
public class WirePathAutoGenerator : MonoBehaviour
{
    [Header("Wire Mesh")]
    public GameObject wireMeshObject;

    [Header("Sampling")]
    public float pointSpacing = 0.05f;
    public string containerName = "WirePathPoints";

    [Header("Start / End Markers (set via buttons below)")]
    public Vector3 manualStartPosition;
    public bool useManualStart = false;
    public Vector3 manualEndPosition;
    public bool useManualEnd = false;

    [Header("Debug")]
    public bool showGizmos = true;
    public Color gizmoColor = Color.cyan;

    [HideInInspector] public List<Transform> generatedPoints = new();

    // ─────────────────────────────────────────────────────────────

    public void GeneratePath()
    {
        if (wireMeshObject == null) { Debug.LogError("[WirePathGen] Assign Wire Mesh Object!"); return; }
        MeshFilter mf = wireMeshObject.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) { Debug.LogError("[WirePathGen] No mesh!"); return; }

        // Clean old container
        var old = transform.Find(containerName);
        if (old != null)
        {
#if UNITY_EDITOR
            DestroyImmediate(old.gameObject);
#else
            Destroy(old.gameObject);
#endif
        }

        // World-space vertices
        Mesh mesh = mf.sharedMesh;
        Matrix4x4 m = wireMeshObject.transform.localToWorldMatrix;
        List<Vector3> worldVerts = new();
        foreach (var v in mesh.vertices)
            worldVerts.Add(m.MultiplyPoint3x4(v));

        // Deduplicate
        worldVerts = Deduplicate(worldVerts, pointSpacing * 0.35f);

        // Pick start vertex
        Vector3 startV = useManualStart
            ? Closest(worldVerts, manualStartPosition)
            : LowestX(worldVerts);

        // Build ordered path from start
        List<Vector3> ordered = Chain(worldVerts, startV);

        // If end is set: find index of closest vertex to end marker and trim
        if (useManualEnd && ordered.Count > 1)
        {
            Vector3 endV = Closest(worldVerts, manualEndPosition);
            int endIdx = 0;
            float best = float.MaxValue;
            for (int i = 0; i < ordered.Count; i++)
            {
                float d = Vector3.Distance(ordered[i], endV);
                if (d < best) { best = d; endIdx = i; }
            }
            // Only trim if end idx is past halfway (prevents cutting the path too short)
            if (endIdx > ordered.Count / 4)
            {
                ordered = ordered.GetRange(0, endIdx + 1);
                Debug.Log($"[WirePathGen] Trimmed to {ordered.Count} pts at end marker.");
            }
            else
            {
                Debug.LogWarning("[WirePathGen] End marker seems too close to start — ignoring trim. Move the orange marker closer to the wire end.");
            }
        }

        // Resample at even spacing
        List<Vector3> sampled = Resample(ordered, pointSpacing);

        // Spawn points
        var container = new GameObject(containerName);
        container.transform.SetParent(transform, false);
        generatedPoints.Clear();
        for (int i = 0; i < sampled.Count; i++)
        {
            var pt = new GameObject($"P{i:000}");
            pt.transform.SetParent(container.transform, true);
            pt.transform.position = sampled[i];
            generatedPoints.Add(pt.transform);
        }

        var puzzle = GetComponent<WireTracePuzzle>();
        if (puzzle != null)
        {
            puzzle.pathPoints = generatedPoints.ToArray();
#if UNITY_EDITOR
            EditorUtility.SetDirty(puzzle);
#endif
        }
        Debug.Log($"[WirePathGen] Done — {generatedPoints.Count} points.");
#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
#endif
    }

    public void ReversePath()
    {
        generatedPoints.Reverse();
        var puzzle = GetComponent<WireTracePuzzle>();
        if (puzzle != null)
        {
            puzzle.pathPoints = generatedPoints.ToArray();
#if UNITY_EDITOR
            EditorUtility.SetDirty(puzzle);
#endif
        }
#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
#endif
    }

    // ── Algorithms ───────────────────────────────────────────────

    List<Vector3> Deduplicate(List<Vector3> pts, float minD)
    {
        var r = new List<Vector3>();
        foreach (var p in pts)
        {
            bool dup = false;
            foreach (var q in r) if (Vector3.Distance(p, q) < minD) { dup = true; break; }
            if (!dup) r.Add(p);
        }
        return r;
    }

    Vector3 Closest(List<Vector3> pts, Vector3 target)
    {
        Vector3 best = pts[0]; float bd = float.MaxValue;
        foreach (var p in pts) { float d = Vector3.Distance(p, target); if (d < bd) { bd = d; best = p; } }
        return best;
    }

    Vector3 LowestX(List<Vector3> pts)
    {
        Vector3 best = pts[0];
        foreach (var p in pts) if (p.x < best.x) best = p;
        return best;
    }

    List<Vector3> Chain(List<Vector3> pts, Vector3 start)
    {
        var remaining = new List<Vector3>(pts);
        var ordered = new List<Vector3>();
        Vector3 cur = Closest(remaining, start);
        remaining.Remove(cur);
        ordered.Add(cur);
        Vector3 dir = Vector3.zero;

        // Two-pass approach:
        // Pass 1: try strict cap with direction bias (follows wire naturally)
        // Pass 2: if nothing found nearby, widen search (bridges small gaps)
        float strictCap = pointSpacing * 4f;
        float wideCap = pointSpacing * 20f;

        while (remaining.Count > 0)
        {
            Vector3 bestPt = Vector3.zero;
            bool found = false;

            // ── Pass 1: strict cap + direction bias ──
            float bestScore = float.MaxValue;
            foreach (var p in remaining)
            {
                float dist = Vector3.Distance(cur, p);
                if (dist > strictCap) continue;

                float penalty = 0f;
                if (dir != Vector3.zero)
                {
                    float dot = Vector3.Dot(dir, (p - cur).normalized);
                    if (dot < -0.6f) penalty = pointSpacing * 2f;
                }

                float score = dist + penalty;
                if (score < bestScore) { bestScore = score; bestPt = p; found = true; }
            }

            // ── Pass 2: widen search, pure distance, no direction bias ──
            if (!found)
            {
                float bestDist = float.MaxValue;
                foreach (var p in remaining)
                {
                    float dist = Vector3.Distance(cur, p);
                    if (dist < wideCap && dist < bestDist)
                    {
                        bestDist = dist;
                        bestPt = p;
                        found = true;
                    }
                }
            }

            if (!found) break;

            dir = (bestPt - cur).normalized;
            ordered.Add(bestPt);
            remaining.Remove(bestPt);
            cur = bestPt;
        }
        return ordered;
    }

    List<Vector3> Resample(List<Vector3> path, float spacing)
    {
        var result = new List<Vector3>();
        if (path.Count < 2) return path;
        result.Add(path[0]);
        float acc = 0f;
        for (int i = 1; i < path.Count; i++)
        {
            float seg = Vector3.Distance(path[i - 1], path[i]);
            acc += seg;
            while (acc >= spacing)
            {
                acc -= spacing;
                float t = 1f - acc / seg;
                result.Add(Vector3.Lerp(path[i - 1], path[i], t));
            }
        }
        result.Add(path[path.Count - 1]);
        return result;
    }

    // ── Gizmos ───────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        if (useManualStart)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(manualStartPosition, 0.06f);
        }
        if (useManualEnd)
        {
            Gizmos.color = new Color(1f, 0.4f, 0f);
            Gizmos.DrawWireSphere(manualEndPosition, 0.06f);
        }

        if (!showGizmos || generatedPoints == null || generatedPoints.Count == 0) return;

        Gizmos.color = gizmoColor;
        for (int i = 0; i < generatedPoints.Count; i++)
        {
            if (generatedPoints[i] == null) continue;
            Gizmos.DrawSphere(generatedPoints[i].position, 0.015f);
            if (i > 0 && generatedPoints[i - 1] != null)
                Gizmos.DrawLine(generatedPoints[i - 1].position, generatedPoints[i].position);
        }
        if (generatedPoints[0] != null)
        { Gizmos.color = Color.green; Gizmos.DrawSphere(generatedPoints[0].position, 0.04f); }
        if (generatedPoints[generatedPoints.Count - 1] != null)
        { Gizmos.color = Color.red; Gizmos.DrawSphere(generatedPoints[generatedPoints.Count - 1].position, 0.04f); }
    }
}


#if UNITY_EDITOR
[CustomEditor(typeof(WirePathAutoGenerator))]
public class WirePathAutoGeneratorEditor : Editor
{
    private bool _pickingStart = false;
    private bool _pickingEnd = false;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var gen = (WirePathAutoGenerator)target;
        EditorGUILayout.Space(10);

        // ── Pick Start ──
        GUI.backgroundColor = _pickingStart ? Color.yellow : new Color(0.4f, 0.8f, 1f);
        if (GUILayout.Button(_pickingStart ? "🖱  Click START on wire in Scene..." : "🎯  Pick Start Point", GUILayout.Height(30)))
        { _pickingStart = !_pickingStart; if (_pickingStart) _pickingEnd = false; }
        GUI.backgroundColor = Color.white;

        // ── Pick End ──
        GUI.backgroundColor = _pickingEnd ? Color.yellow : new Color(1f, 0.65f, 0.2f);
        if (GUILayout.Button(_pickingEnd ? "🖱  Click END on wire in Scene..." : "🎯  Pick End Point", GUILayout.Height(30)))
        { _pickingEnd = !_pickingEnd; if (_pickingEnd) _pickingStart = false; }
        GUI.backgroundColor = Color.white;

        if (_pickingStart) EditorGUILayout.HelpBox("Click the wire START in Scene view. Yellow sphere = picked position.", MessageType.Warning);
        if (_pickingEnd) EditorGUILayout.HelpBox("Click the wire END in Scene view. Orange sphere = picked position.", MessageType.Warning);

        EditorGUILayout.Space(6);

        // ── Generate ──
        GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
        if (GUILayout.Button("▶  Generate Path Points", GUILayout.Height(36)))
        { gen.GeneratePath(); _pickingStart = _pickingEnd = false; }
        GUI.backgroundColor = Color.white;

        if (gen.generatedPoints != null && gen.generatedPoints.Count > 0)
        {
            EditorGUILayout.HelpBox($"✓ {gen.generatedPoints.Count} points generated.", MessageType.Info);
            GUI.backgroundColor = new Color(0.9f, 0.6f, 0.2f);
            if (GUILayout.Button("⇄  Reverse Path Direction", GUILayout.Height(28)))
                gen.ReversePath();
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox("1. Pick Start (yellow) → click wire start in Scene\n2. Pick End (orange) → click wire end in Scene\n3. Generate\nGreen = start, Red = end.", MessageType.None);
    }

    void OnSceneGUI()
    {
        if (!_pickingStart && !_pickingEnd) return;

        var gen = (WirePathAutoGenerator)target;
        Event e = Event.current;

        if (e.type == EventType.Layout)
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Vector3 pos;

            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
                pos = hit.point;
            else
            {
                float t = ray.direction.y != 0f ? -ray.origin.y / ray.direction.y : 5f;
                pos = ray.GetPoint(Mathf.Clamp(t, 0.1f, 500f));
            }

            if (_pickingStart)
            {
                gen.manualStartPosition = pos;
                gen.useManualStart = true;
                _pickingStart = false;
                Debug.Log($"[WirePathGen] Start set to {pos}");
            }
            else
            {
                gen.manualEndPosition = pos;
                gen.useManualEnd = true;
                _pickingEnd = false;
                Debug.Log($"[WirePathGen] End set to {pos}");
            }

            EditorUtility.SetDirty(gen);
            e.Use();
            Repaint();
        }

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
        { _pickingStart = _pickingEnd = false; e.Use(); }
    }
}
#endif