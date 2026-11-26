using System;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;

using static KerbalMemorial.RegisterToolbar;

namespace KerbalMemorial
{
    // A scene decoration, not an editor part, screen overlay, or flag replacement.
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    public sealed class EditorAccidentSign : MonoBehaviour
    {
        private Renderer anchor;
        private GameObject board;
        private GameObject face, stand, backing;
        private readonly GameObject[] posts = new GameObject[2];
        private readonly GameObject[] feet = new GameObject[2];
        private Mesh mesh, postMesh, backingMesh;
        private Material standMaterial;
        private Material material;
        private Texture2D texture;
        private string lastLine;
        private float nextSearch, nextRefresh, started;
        private bool warned;
        private int appliedSettingsRevision = -1;
        private EditorFacility facility = EditorFacility.None;
        private const string Prefix = "[KerbalMemorial] Accident sign: ";

        private void Start()
        {
            started = Time.realtimeSinceStartup;
        }

        private void LateUpdate()
        {
            if (!HighLogic.LoadedSceneIsEditor)
                return;
            float now = Time.realtimeSinceStartup;
            if (facility != EditorDriver.editorFacility)
            {
                facility = EditorDriver.editorFacility;
                RemoveBoard();
                nextSearch = nextRefresh = 0;
                started = now;
                warned = false;
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
                        try
                        {
                            CreateBoard(candidate);
                        }
                        catch (Exception e)
                        {
                            RemoveBoard();
                            if (!warned)
                            {
                                Log.Warning(Prefix + e);
                                warned = true;
                            }
                        }
                    }
                    else if (!warned && now - started > 10f)
                    {
                        Log.Warning(Prefix + "No active editor background flag found in " + facility);
                        warned = true;
                    }
                }
            }
            if (board != null && anchor != null && appliedSettingsRevision != SignSettings.Revision)
            {
                // Update placement and mounting without recreating the counter texture.
                try
                {
                    PositionBoard(anchor, -board.transform.forward);
                }
                catch (Exception e)
                {
                    RemoveBoard();
                    if (!warned)
                    {
                        Log.Warning(Prefix + e);
                        warned = true;
                    }
                }
            }
            if (board == null || now < nextRefresh)
                return;
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
                if (flag == null || !flag.gameObject.activeInHierarchy)
                    continue;
                Consider(flag.flagMeshRenderer, ref best, ref bestArea);
                Consider(flag.skinnedMeshRender, ref best, ref bestArea);
            }
            return best;
        }

        private static void Consider(Renderer renderer, ref Renderer best, ref float bestArea)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                return;
            Bounds b = renderer.bounds;
            float area = b.size.y * Mathf.Max(b.size.x, b.size.z);
            if (area > bestArea && b.size.y > 0.1f && Mathf.Max(b.size.x, b.size.z) > 0.1f)
            {
                best = renderer;
                bestArea = area;
            }
        }

        private static Vector3 FlagNormal(Renderer renderer)
        {
            // Use the thin local mesh dimension when it has a horizontal normal;
            // this handles flags mounted on a rotated wall. Animated cloth may
            // have a thicker bounding box, so fall back to world-space bounds.
            Bounds local;
            var skinned = renderer as SkinnedMeshRenderer;
            var filter = renderer.GetComponent<MeshFilter>();
            if (skinned != null && skinned.sharedMesh != null)
                local = skinned.sharedMesh.bounds;
            else if (filter != null && filter.sharedMesh != null)
                local = filter.sharedMesh.bounds;
            else
                return WorldNormal(renderer.bounds);
            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
            float thinnest = float.PositiveInfinity;
            Vector3 normal = WorldNormal(renderer.bounds);
            for (int i = 0; i < axes.Length; ++i)
            {
                Vector3 v = renderer.transform.TransformVector(axes[i]);
                if (v.sqrMagnitude < 1e-8f)
                    continue;
                float thickness = local.size[i] * v.magnitude;
                Vector3 direction = v.normalized;
                if (Mathf.Abs(direction.y) > 0.35f || thickness >= thinnest)
                    continue;
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
                if (cameraTransform != null)
                    return cameraTransform.position;
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
                    if (TryFloorAt(origin, out float hitY))
                        floorY = Mathf.Min(floorY, hitY);
                }
            }
            return !float.IsPositiveInfinity(floorY);
        }

        private static bool TryFloorAt(Vector3 origin, out float floorY)
        {
            float nearest = float.PositiveInfinity;
            floorY = 0f;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, Vector3.down, 250f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == null || hit.normal.y < .85f || hit.distance >= nearest)
                    continue;
                if (hit.collider.GetComponentInParent<Part>() != null)
                    continue;
                nearest = hit.distance;
                floorY = hit.point.y;
            }
            return !float.IsPositiveInfinity(nearest);
        }

        private void PositionBoard(Renderer flag, Vector3 normal)
        {
            Bounds bounds = flag.bounds;
            Vector3 horizontal = Vector3.Cross(Vector3.up, normal).normalized;
            float flagWidth = 2f * ProjectedExtent(bounds, horizontal);
            var layout = SignLayout.Load(facility);
            float width = Mathf.Max(0.1f, layout.WidthMeters > 0f ? layout.WidthMeters : flagWidth * layout.Width);
            float height = width * SafetySignArtwork.WorldAspect;
            float depth = Mathf.Max(0.025f, flagWidth * 0.003f) + flagWidth * layout.DepthOffset;
            float thickness = Mathf.Clamp(width * .035f, .025f, .18f);
            float postDepth = thickness + Mathf.Clamp(width * .075f, .035f, .4f) * .25f;
            if (layout.PostMounted)
                depth = Mathf.Max(.4f + postDepth, depth + Mathf.Max(.6f, width * .25f));
            Vector3 position = bounds.center + horizontal * (flagWidth * layout.HorizontalOffset)
                + normal * (ProjectedExtent(bounds, normal) + depth);
            float floorY = layout.FloorY;
            float[] footY = { float.NaN, float.NaN };
            string heightSource = "configured floor Y";
            if (float.IsNaN(floorY) && layout.PostMounted)
            {
                // Probe beneath each actual support, not beneath the flag wall.
                for (int i = 0; i < 2; ++i)
                {
                    Vector3 origin = position - normal * postDepth + horizontal * ((i == 0 ? 1f : -1f) * width * .28f);
                    origin.y = bounds.min.y - .05f;
                    if (TryFloorAt(origin, out float found))
                        footY[i] = found;
                }
                if (!float.IsNaN(footY[0]) || !float.IsNaN(footY[1]))
                {
                    floorY = float.IsNaN(footY[0]) ? footY[1] : float.IsNaN(footY[1]) ? footY[0] : Mathf.Max(footY[0], footY[1]);
                    heightSource = "detected support floor Y";
                }
            }
            if (float.IsNaN(floorY) && (layout.PostMounted || layout.HeightAboveFloor >= 0f))
            {
                if (TryFindFloor(bounds, normal, horizontal, flagWidth, out floorY))
                    heightSource = "detected floor Y";
                else
                {
                    floorY = 0f;
                    heightSource = "fallback floor Y";
                    Log.Warning(Prefix + facility + " floor not detected; using world Y=0. Set floorY in AccidentSign.cfg if needed.");
                }
            }
            if (layout.HeightAboveFloor < 0f)
            {
                position.y = bounds.min.y - flagWidth * layout.Gap - height * .5f;
                heightSource = "flag gap";
            }
            else
            {
                position.y = floorY + layout.HeightAboveFloor + height * .5f;
                heightSource += "=" + floorY.ToString("F2", CultureInfo.InvariantCulture);
            }
            // A legacy flag-gap position may be too low for posts at a raised floor.
            if (layout.PostMounted)
                position.y = Mathf.Max(position.y, floorY + height * .5f + .01f);

            board.transform.position = position;
            board.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);
            face.transform.localScale = new Vector3(width, height, 1f);
            if (layout.PostMounted)
            {
                EnsureStand();
                stand.SetActive(true);
                SetGeometry(backing, new Vector3(0f, 0f, thickness * .5f + .002f), new Vector3(width * .98f, height * .98f, thickness));
                float diameter = Mathf.Clamp(width * .075f, .035f, .4f);
                for (int i = 0; i < 2; ++i)
                {
                    float ground = float.IsNaN(footY[i]) ? floorY : footY[i];
                    // Extend VAB supports only; SPH uses the original floor reference.
                    float bottom = ground - position.y - (facility == EditorFacility.VAB ? 1.75f : 0f);
                    float top = -height * .28f;
                    float length = Mathf.Max(.01f, top - bottom);
                    float x = (i == 0 ? -1f : 1f) * width * .28f;
                    float footHeight = Mathf.Min(diameter * .35f, length);
                    SetGeometry(posts[i], new Vector3(x, bottom + length * .5f, postDepth), new Vector3(diameter, length, diameter));
                    SetGeometry(feet[i], new Vector3(x, bottom + footHeight * .5f, postDepth), new Vector3(diameter * 1.6f, footHeight, diameter * 1.6f));
                }
            }
            else if (stand != null)
                stand.SetActive(false);
            appliedSettingsRevision = SignSettings.Revision;
            Log.Info(Prefix + facility + " flag " + flag.name + ", position " + position + ", size " + width.ToString("F2") + " x " + height.ToString("F2") + ", " + heightSource + ", mounting " + (layout.PostMounted ? "posts" : "wall"));
        }

        private static void SetGeometry(GameObject item, Vector3 position, Vector3 scale)
        {
            item.transform.localPosition = position;
            item.transform.localScale = scale;
        }

        private GameObject Geometry(string name, Transform parent, Mesh geometry, Material surface)
        {
            var item = new GameObject(name);
            item.layer = board.layer;
            item.transform.SetParent(parent, false);
            item.AddComponent<MeshFilter>().sharedMesh = geometry;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = surface;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return item;
        }

        private void EnsureStand()
        {
            if (stand != null)
                return;
            Shader shader = Shader.Find("KSP/Diffuse");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                throw new InvalidOperationException("No suitable support shader available.");
            standMaterial = new Material(shader);
            standMaterial.name = "KerbalMemorial steel supports";
            standMaterial.color = new Color(.32f, .33f, .36f, 1f);
            postMesh = MakeCylinder();
            backingMesh = MakeBacking();
            stand = new GameObject("KerbalMemorial freestanding mount");
            stand.layer = board.layer;
            stand.transform.SetParent(board.transform, false);
            backing = Geometry("Sign backing", stand.transform, backingMesh, standMaterial);
            for (int i = 0; i < 2; ++i)
            {
                posts[i] = Geometry("Support post " + (i + 1), stand.transform, postMesh, standMaterial);
                feet[i] = Geometry("Support foot " + (i + 1), stand.transform, postMesh, standMaterial);
            }
        }

        // Mesh-only scenery: no colliders, parts or editor input hooks.
        internal static Mesh MakeCylinder()
        {
            const int sides = 24;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            for (int i = 0; i < sides; ++i)
            {
                float angle = i * 2f * Mathf.PI / sides;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices.Add(radial * .5f - Vector3.up * .5f);
                vertices.Add(radial * .5f + Vector3.up * .5f);
                normals.Add(radial);
                normals.Add(radial);
                int a = i * 2;
                int b = ((i + 1) % sides) * 2;
                triangles.AddRange(new[] { a, a + 1, b + 1, a, b + 1, b });
            }
            for (int cap = 0; cap < 2; ++cap)
            {
                float y = cap == 0 ? -.5f : .5f;
                Vector3 normal = cap == 0 ? Vector3.down : Vector3.up;
                int center = vertices.Count;
                vertices.Add(new Vector3(0f, y, 0f));
                normals.Add(normal);
                for (int i = 0; i < sides; ++i)
                {
                    Vector3 point = vertices[i * 2];
                    point.y = y;
                    vertices.Add(point);
                    normals.Add(normal);
                    int a = center + 1 + i;
                    int b = center + 1 + (i + 1) % sides;
                    triangles.AddRange(cap == 0 ? new[] { center, a, b } : new[] { center, b, a });
                }
            }
            var result = new Mesh { name = "KerbalMemorial support cylinder" };
            result.vertices = vertices.ToArray();
            result.normals = normals.ToArray();
            result.triangles = triangles.ToArray();
            result.RecalculateBounds();
            return result;
        }

        internal static Mesh MakeBacking()
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (Vector3 normal in normals)
            {
                Vector3 tangent = Vector3.Cross(Mathf.Abs(normal.y) > .5f ? Vector3.forward : Vector3.up, normal);
                Vector3 bitangent = Vector3.Cross(normal, tangent);
                int first = vertices.Count;
                vertices.Add((normal - tangent - bitangent) * .5f);
                vertices.Add((normal + tangent - bitangent) * .5f);
                vertices.Add((normal + tangent + bitangent) * .5f);
                vertices.Add((normal - tangent + bitangent) * .5f);
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
            var result = new Mesh { name = "KerbalMemorial sign backing" };
            result.vertices = vertices.ToArray();
            result.triangles = triangles.ToArray();
            result.RecalculateNormals();
            result.RecalculateBounds();
            return result;
        }

        private void CreateBoard(Renderer flag)
        {
            Vector3 normal = FlagNormal(flag);
            if (Vector3.Dot(InteriorPoint() - flag.bounds.center, normal) < 0)
                normal = -normal;
            Shader shader = Shader.Find("Unlit/Texture");
            if (shader == null)
                shader = Shader.Find("KSP/Diffuse");
            if (shader == null)
                throw new InvalidOperationException("No suitable sign shader available.");
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
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            board = new GameObject("KerbalMemorial Accident Sign");
            board.layer = flag.gameObject.layer;
            face = Geometry("Safety sign face", board.transform, mesh, material);
            PositionBoard(flag, normal);
            anchor = flag;
            lastLine = null;
            nextRefresh = 0;
            warned = false;
        }

        private void RemoveBoard()
        {
            if (board != null)
            {
                board.SetActive(false);
                Destroy(board);
            }
            if (mesh != null)
                Destroy(mesh);
            if (postMesh != null)
                Destroy(postMesh);
            if (backingMesh != null)
                Destroy(backingMesh);
            if (standMaterial != null)
                Destroy(standMaterial);
            face = null;
            stand = null;
            backing = null;
            postMesh = null;
            backingMesh = null;
            standMaterial = null;
            Array.Clear(posts, 0, posts.Length);
            Array.Clear(feet, 0, feet.Length);
            if (material != null)
                Destroy(material);
            if (texture != null)
                Destroy(texture);
            board = null;
            mesh = null;
            material = null;
            texture = null;
            anchor = null;
            lastLine = null;
        }
        private void OnDestroy()
        {
            RemoveBoard();
        }

        internal sealed class SignLayout
        {
            internal float Width = .45f, Gap = .05f, HorizontalOffset, DepthOffset;
            internal float WidthMeters = 0f, HeightAboveFloor = 2f, FloorY = float.NaN;
            internal bool PostMounted = true;
            internal static SignLayout Load(EditorFacility facility)
            {
                var result = new SignLayout();
                if (GameDatabase.Instance != null)
                    foreach (ConfigNode root in GameDatabase.Instance.GetConfigNodes("KERBAL_MEMORIAL_SIGN"))
                    {
                        ConfigNode node = root.GetNode(facility.ToString());
                        if (node == null)
                            continue;
                        if (bool.TryParse(node.GetValue("postMounted"), out bool posts))
                            result.PostMounted = posts;
                        result.Width = Read(node, "widthMultiplier", result.Width, .1f, 3f);
                        result.WidthMeters = Read(node, "widthMeters", result.WidthMeters, 0f, 100f);
                        result.HeightAboveFloor = Read(node, "heightAboveFloor", result.HeightAboveFloor, -1f, 100f);
                        result.FloorY = Read(node, "floorY", result.FloorY, -10000f, 10000f);
                        result.Gap = Read(node, "gapMultiplier", result.Gap, 0f, 3f);
                        result.HorizontalOffset = Read(node, "horizontalOffset", result.HorizontalOffset, -3f, 3f);
                        result.DepthOffset = Read(node, "depthOffset", result.DepthOffset, -1f, 1f);
                    }
                SignSettings.ApplyOverrides(facility, ref result.WidthMeters, ref result.HeightAboveFloor, ref result.PostMounted);
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
