using System;
using System.Globalization;
using UnityEngine;

namespace KerbalMemorial
{
    // A scene decoration, not an editor part, screen overlay, or flag replacement.
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public sealed class EditorAccidentSign : MonoBehaviour
    {
        private Renderer anchor;
        private GameObject board;
        private Mesh mesh;
        private Material material;
        private Texture2D texture;
        private string lastLine;
        private float nextSearch, nextRefresh, started;
        private bool warned;
        private int appliedSettingsRevision = -1;
        private EditorFacility facility = EditorFacility.None;
        private const string Prefix = "[KerbalMemorial] Accident sign: ";

        private void Start() { started = Time.realtimeSinceStartup; }

        private void LateUpdate()
        {
            if (!HighLogic.LoadedSceneIsEditor) return;
            float now = Time.realtimeSinceStartup;
            if (facility != EditorDriver.editorFacility)
            {
                facility = EditorDriver.editorFacility;
                RemoveBoard(); nextSearch = nextRefresh = 0;
                started = now; warned = false;
            }
            if (now >= nextSearch)
            {
                nextSearch = now + 1f;
                // Both editors (and all facility levels) use the stock background flag script.
                // Retry while the scenery is loading and if a building is replaced.
                if (anchor == null || !anchor.gameObject.activeInHierarchy || !anchor.enabled || board == null)
                {
                    RemoveBoard();
                    Renderer candidate = FindFlag();
                    if (candidate != null)
                    {
                        try { CreateBoard(candidate); }
                        catch (Exception e)
                        {
                            RemoveBoard();
                            if (!warned) { Debug.LogWarning(Prefix + e); warned = true; }
                        }
                    }
                    else if (!warned && now - started > 10f)
                    {
                        Debug.LogWarning(Prefix + "No active editor background flag found in " + facility);
                        warned = true;
                    }
                }
            }
            if (board != null && anchor != null && appliedSettingsRevision != SignSettings.Revision)
            {
                // Update only the transform: keep the mesh, material and counter texture.
                PositionBoard(anchor, -board.transform.forward);
            }
            if (board == null || now < nextRefresh) return;
            nextRefresh = now + 0.5f;
            var tracker = CasualtyTracker.Instance;
            string line = tracker != null && tracker.IsReady
                ? Ledger.SafetySignDays(tracker.Live.Records, CasualtyTracker.Now, KSPUtil.dateTimeFormatter.Day)
                : "--- DAYS";
            if (line != lastLine)
            {
                SafetySignArtwork.Paint(texture, line);
                lastLine = line;
            }
        }

        private static Renderer FindFlag()
        {
            Renderer best = null;
            float bestArea = 0f;
            foreach (var flag in FindObjectsOfType<EditorBackgroundFlag>())
            {
                if (flag == null || !flag.gameObject.activeInHierarchy) continue;
                Consider(flag.flagMeshRenderer, ref best, ref bestArea);
                Consider(flag.skinnedMeshRender, ref best, ref bestArea);
            }
            return best;
        }

        private static void Consider(Renderer renderer, ref Renderer best, ref float bestArea)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) return;
            Bounds b = renderer.bounds;
            float area = b.size.y * Mathf.Max(b.size.x, b.size.z);
            if (area > bestArea && b.size.y > 0.1f && Mathf.Max(b.size.x, b.size.z) > 0.1f)
            { best = renderer; bestArea = area; }
        }

        private static Vector3 FlagNormal(Renderer renderer)
        {
            // Use the thin local mesh dimension when it has a horizontal normal;
            // this handles flags mounted on a rotated wall. Animated cloth may
            // have a thicker bounding box, so fall back to world-space bounds.
            Bounds local;
            var skinned = renderer as SkinnedMeshRenderer;
            var filter = renderer.GetComponent<MeshFilter>();
            if (skinned != null && skinned.sharedMesh != null) local = skinned.sharedMesh.bounds;
            else if (filter != null && filter.sharedMesh != null) local = filter.sharedMesh.bounds;
            else return WorldNormal(renderer.bounds);
            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
            float thinnest = float.PositiveInfinity;
            Vector3 normal = WorldNormal(renderer.bounds);
            for (int i = 0; i < axes.Length; ++i)
            {
                Vector3 v = renderer.transform.TransformVector(axes[i]);
                if (v.sqrMagnitude < 1e-8f) continue;
                float thickness = local.size[i] * v.magnitude;
                Vector3 direction = v.normalized;
                if (Mathf.Abs(direction.y) > 0.35f || thickness >= thinnest) continue;
                thinnest = thickness;
                normal = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            }
            return normal;
        }

        private static Vector3 WorldNormal(Bounds b) => b.size.x <= b.size.z ? Vector3.right : Vector3.forward;

        private Vector3 InteriorPoint()
        {
            var driver = EditorDriver.fetch;
            if (driver != null)
            {
                Transform cameraTransform = null;
                if (facility == EditorFacility.VAB && driver.vabCamera != null)
                    cameraTransform = driver.vabCamera.GetPivot() ?? driver.vabCamera.GetCameraTransform();
                else if (facility == EditorFacility.SPH && driver.sphCamera != null)
                    cameraTransform = driver.sphCamera.GetPivot() ?? driver.sphCamera.GetCameraTransform();
                if (cameraTransform != null) return cameraTransform.position;
            }
            return Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        }

        private static float ProjectedExtent(Bounds bounds, Vector3 axis)
        {
            Vector3 e = bounds.extents;
            return Mathf.Abs(axis.x) * e.x + Mathf.Abs(axis.y) * e.y + Mathf.Abs(axis.z) * e.z;
        }

        // Probe several points just inside the flag wall. Pick the lowest of
        // the nearest upward-facing scenery hits to avoid raised platforms.
        // Never use a vessel part or trigger as the reference floor.
        private static bool TryFindFloor(Bounds bounds, Vector3 normal, Vector3 horizontal,
            float flagWidth, out float floorY)
        {
            floorY = float.PositiveInfinity;
            float front = ProjectedExtent(bounds, normal);
            float[] depths = { 0.5f, 1.5f, 3f };
            float lateral = Mathf.Clamp(flagWidth * .15f, .5f, 2f);
            float startY = bounds.min.y - .05f;
            foreach (float depth in depths)
            {
                for (int side = -1; side <= 1; ++side)
                {
                    Vector3 origin = bounds.center + normal * (front + depth)
                        + horizontal * (side * lateral);
                    origin.y = startY;
                    float nearest = float.PositiveInfinity;
                    float hitY = 0f;
                    foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, 250f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider == null || hit.normal.y < .85f || hit.distance >= nearest)
                            continue;
                        if (hit.collider.GetComponentInParent<Part>() != null) continue;
                        nearest = hit.distance;
                        hitY = hit.point.y;
                    }
                    if (!float.IsPositiveInfinity(nearest)) floorY = Mathf.Min(floorY, hitY);
                }
            }
            return !float.IsPositiveInfinity(floorY);
        }

        private void PositionBoard(Renderer flag, Vector3 normal)
        {
            Bounds bounds = flag.bounds;
            Vector3 horizontal = Vector3.Cross(Vector3.up, normal).normalized;
            float flagWidth = 2f * ProjectedExtent(bounds, horizontal);
            var layout = SignLayout.Load(facility);
            float width = Mathf.Max(0.1f, layout.WidthMeters > 0f ? layout.WidthMeters : flagWidth * layout.Width);
            float height = width * SafetySignArtwork.WorldAspect;
            Vector3 position = bounds.center;
            string heightSource;
            if (layout.HeightAboveFloor < 0f)
            {
                // Legacy placement immediately below the flag.
                position.y = bounds.min.y - flagWidth * layout.Gap - height * 0.5f;
                heightSource = "flag gap";
            }
            else
            {
                float floorY = layout.FloorY;
                if (!float.IsNaN(floorY)) heightSource = "configured floor Y";
                else if (TryFindFloor(bounds, normal, horizontal, flagWidth, out floorY))
                    heightSource = "detected floor Y";
                else
                {
                    floorY = 0f;
                    heightSource = "fallback floor Y";
                    Debug.LogWarning(Prefix + facility + " floor not detected; using world Y=0. Set floorY in AccidentSign.cfg if needed.");
                }
                position.y = floorY + layout.HeightAboveFloor + height * .5f;
                heightSource += "=" + floorY.ToString("F2", CultureInfo.InvariantCulture);
            }
            position += horizontal * (flagWidth * layout.HorizontalOffset);
            position += normal * (ProjectedExtent(bounds, normal) + Mathf.Max(0.025f, flagWidth * 0.003f) + flagWidth * layout.DepthOffset);

            board.transform.position = position;
            board.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);
            board.transform.localScale = new Vector3(width, height, 1f);
            appliedSettingsRevision = SignSettings.Revision;
            Debug.Log(Prefix + facility + " flag " + flag.name + ", position " + position + ", size " + width.ToString("F2") + " x " + height.ToString("F2") + ", " + heightSource);
        }

        private void CreateBoard(Renderer flag)
        {
            Vector3 normal = FlagNormal(flag);
            if (Vector3.Dot(InteriorPoint() - flag.bounds.center, normal) < 0) normal = -normal;
            Shader shader = Shader.Find("Unlit/Texture");
            if (shader == null) shader = Shader.Find("KSP/Diffuse");
            if (shader == null) throw new InvalidOperationException("No suitable sign shader available.");
            texture = new Texture2D(SafetySignArtwork.Width, SafetySignArtwork.Height, TextureFormat.RGBA32, true);
            texture.name = "KerbalMemorial safety sign texture";
            SafetySignArtwork.Initialize(texture);
            SafetySignArtwork.Paint(texture, "--- DAYS");
            material = new Material(shader) { name = "KerbalMemorial safety sign material", mainTexture = texture };

            // A flat, single-sided quad with a front normal of -Z. No Collider,
            // Part, editor input hooks or changes to the stock flag/material.
            mesh = new Mesh { name = "KerbalMemorial safety sign mesh" };
            mesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(-.5f,.5f,0), new Vector3(.5f,.5f,0), new Vector3(.5f,-.5f,0) };
            mesh.uv = new[] { new Vector2(0,0), new Vector2(0,1), new Vector2(1,1), new Vector2(1,0) };
            mesh.triangles = new[] { 0,1,2,0,2,3 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            board = new GameObject("KerbalMemorial Accident Sign");
            board.layer = flag.gameObject.layer;
            PositionBoard(flag, normal);
            board.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = board.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            anchor = flag;
            lastLine = null; nextRefresh = 0;
            warned = false;
        }

        private void RemoveBoard()
        {
            if (board != null) { board.SetActive(false); Destroy(board); }
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
            if (texture != null) Destroy(texture);
            board = null; mesh = null; material = null; texture = null; anchor = null; lastLine = null;
        }
        private void OnDestroy() { RemoveBoard(); }

        internal sealed class SignLayout
        {
            internal float Width = .45f, Gap = .05f, HorizontalOffset, DepthOffset;
            internal float WidthMeters = 0f, HeightAboveFloor = 2f, FloorY = float.NaN;
            internal static SignLayout Load(EditorFacility facility)
            {
                var result = new SignLayout();
                if (GameDatabase.Instance != null)
                foreach (ConfigNode root in GameDatabase.Instance.GetConfigNodes("KERBAL_MEMORIAL_SIGN"))
                {
                    ConfigNode node = root.GetNode(facility.ToString());
                    if (node == null) continue;
                    result.Width = Read(node, "widthMultiplier", result.Width, .1f, 3f);
                    result.WidthMeters = Read(node, "widthMeters", result.WidthMeters, 0f, 100f);
                    result.HeightAboveFloor = Read(node, "heightAboveFloor", result.HeightAboveFloor, -1f, 100f);
                    result.FloorY = Read(node, "floorY", result.FloorY, -10000f, 10000f);
                    result.Gap = Read(node, "gapMultiplier", result.Gap, 0f, 3f);
                    result.HorizontalOffset = Read(node, "horizontalOffset", result.HorizontalOffset, -3f, 3f);
                    result.DepthOffset = Read(node, "depthOffset", result.DepthOffset, -1f, 1f);
                }
                SignSettings.ApplyOverrides(facility, ref result.WidthMeters, ref result.HeightAboveFloor);
                return result;
            }
            private static float Read(ConfigNode node, string key, float fallback, float min, float max)
            {
                float value;
                return float.TryParse(node.GetValue(key), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                    && !float.IsNaN(value) && !float.IsInfinity(value) ? Mathf.Clamp(value, min, max) : fallback;
            }
        }
    }
}
