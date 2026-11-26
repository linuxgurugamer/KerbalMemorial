using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ClickThroughFix;
using UnityEngine;

using static KerbalMemorial.RegisterToolbar;

namespace KerbalMemorial
{
    // Scene-only scenery. Parent to the loaded facility so floating-origin shifts
    // and PQS visibility changes are inherited instead of saving world coordinates.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public sealed class FlightAccidentSigns : MonoBehaviour
    {
        private const string Prefix = "[KerbalMemorial] Outdoor sign: ";
        private Site[] sites = new Site[0];
        internal static FlightAccidentSigns Instance { get; private set; }
        private const float OffsetLimit = 1000f;
        private const string OverrideNode = "KERBAL_MEMORIAL_OUTDOOR_SIGN_SETTINGS";
        private Rect settingsRect = new Rect(60f, 90f, 580f, 480f);
        private Vector2 settingsScroll;
        private bool settingsOpen, settingsDirty;
        private bool showPlacementControls;
        private bool showPlacementControlsForLayout;
        private string settingsMessage = "";
        private static string SettingsPath => Path.Combine(KSPUtil.ApplicationRootPath,
            "GameData", "KerbalMemorial", "PluginData", "OutdoorSignSettings.cfg");
        private Texture2D texture;
        private Material faceMaterial, steelMaterial;
        private Mesh boardMesh, postMesh;
        private string lastLine;
        private float nextSearch, nextRefresh;

        private void LateUpdate()
        {
            if (!HighLogic.LoadedSceneIsFlight)
                return;
            float now = Time.realtimeSinceStartup;
            CelestialBody body = FlightGlobals.currentMainBody;
            if (body == null || body != FlightGlobals.GetHomeBody())
            {
                foreach (Site site in sites)
                    site.Clear();
                return;
            }
            foreach (Site site in sites)
            {
                if (site.Board != null && (site.Building == null || !site.Building.gameObject.activeInHierarchy || site.Building.IsDestroyed))
                    site.Clear();
                if (site.PlacementChanged)
                {
                    site.PlacementChanged = false;
                    // Keep the known facility for live adjustments. Disable the
                    // old geometry before measuring the building or raycasting.
                    DestructibleBuilding building = site.Building;
                    site.Clear();
                    if (site.Enabled && building != null && building.gameObject.activeInHierarchy && !building.IsDestroyed)
                    {
                        try
                        {
                            TryCreate(site, body, building);
                        }
                        catch (Exception e)
                        {
                            site.Clear();
                            Log.Warning(Prefix + site.Name + ": adjustment failed: " + e.Message);
                        }
                    }
                    if (site.Board == null)
                        nextSearch = 0f;
                }
            }
            if (now >= nextSearch && FlightGlobals.ActiveVessel != null && FlightGlobals.ActiveVessel.altitude < 10000d)
            {
                nextSearch = now + 3f;
                foreach (Site site in sites)
                {
                    if (site.Enabled && site.Board == null)
                    {
                        try
                        {
                            TryCreate(site, body);
                        }
                        catch (Exception e)
                        {
                            site.Clear();
                            if (!site.Warned)
                            {
                                Log.Warning(Prefix + site.Name + ": " + e);
                                site.Warned = true;
                            }
                        }
                    }
                }
            }
            if (texture == null || now < nextRefresh)
                return;
            nextRefresh = now + .5f;
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

        private void Start()
        {
            Instance = this;
            if (GameDatabase.Instance == null)
                return;

            var configuredSites = new List<Site>();
            foreach (ConfigNode root in GameDatabase.Instance.GetConfigNodes("KERBAL_MEMORIAL_OUTDOOR_SIGN"))
            {
                foreach (ConfigNode node in root.nodes)
                {
                    if (node == null || string.IsNullOrWhiteSpace(node.name))
                        continue;

                    var site = new Site(node.name);
                    site.Load(node);
                    configuredSites.Add(site);
                }
            }
            sites = configuredSites.ToArray();
            LoadOverrides();
            Log.Info(Prefix + "Loaded " + sites.Length + " configured sites");
        }

        // Stock DestructibleBuilding IDs/names vary with facility level. Accept
        // main building identifiers only; never select tanks, towers or debris.
        private static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            var result = new System.Text.StringBuilder();
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c))
                    result.Append(char.ToLowerInvariant(c));
            }
            return result.ToString();
        }

        private static int NameScore(string text, string name)
        {
            string key = Normalize(text);
            if (key.StartsWith("ksc", StringComparison.Ordinal))
                key = key.Substring(3);
            string shortName = Normalize(name);
            if (string.IsNullOrEmpty(shortName))
                return 0;
            string longName = shortName;
            if (shortName == "vab")
                longName = "vehicleassemblybuilding";
            else if (shortName == "sph")
                longName = "spaceplanehangar";
            if (key.StartsWith(shortName + "mainbuilding", StringComparison.Ordinal) ||
                (longName != shortName && key.StartsWith(longName, StringComparison.Ordinal)))
                return 3;
            if (key == shortName || key == shortName + "building")
                return 2;
            if (key.StartsWith(shortName + "level", StringComparison.Ordinal) ||
                key.StartsWith(shortName + "lvl", StringComparison.Ordinal) ||
                (key.StartsWith(shortName, StringComparison.Ordinal) && key.Length > shortName.Length && char.IsDigit(key[shortName.Length])))
                return 1;
            return 0;
        }

        private static int BuildingScore(DestructibleBuilding building, string name)
        {
            int score = NameScore(building.name, name);
            if (!string.IsNullOrEmpty(building.id))
            {
                string[] pieces = building.id.Split('/');
                score = Mathf.Max(score, NameScore(pieces[pieces.Length - 1], name));
            }
            return score;
        }

        private static DestructibleBuilding FindBuilding(Site site, out Bounds bounds, out int layer)
        {
            DestructibleBuilding best = null;
            bounds = new Bounds();
            layer = 0;
            int bestScore = 0;
            float bestVolume = 0f;
            foreach (var building in FindObjectsOfType<DestructibleBuilding>())
            {
                if (building == null || !building.gameObject.activeInHierarchy || building.IsDestroyed)
                    continue;

                if (building != null)
                    Log.Info(Prefix + site.Name + ", building=" + building.name + ", id=" + building.id + ", score=" + BuildingScore(building, site.Name));

                int score = BuildingScore(building, site.Name);
                if (!string.IsNullOrEmpty(site.BuildingId))
                    score = string.Equals(building.id, site.BuildingId, StringComparison.OrdinalIgnoreCase) ? 10 : 0;
                if (score == 0 || score < bestScore)
                    continue;
                if (building.GetComponentInParent<PQSCity>() == null)
                    continue;
                if (!GetBounds(building, out Bounds candidate, out int candidateLayer))
                    continue;
                float volume = candidate.size.x * candidate.size.y * candidate.size.z;
                if (score == bestScore && volume <= bestVolume)
                    continue;
                best = building;
                bestScore = score;
                bestVolume = volume;
                bounds = candidate;
                layer = candidateLayer;
            }
            if (best == null)
                Log.Warning(Prefix + site.Name + ": no matching building found");
            return best;
        }

        private static System.Collections.Generic.List<Renderer> BuildingRenderers(DestructibleBuilding building)
        {
            var result = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer renderer in building.GetComponentsInChildren<Renderer>())
            {
                if (renderer.GetComponentInParent<DestructibleBuilding>() == building)
                    AddRenderer(result, renderer);
            }
            // Some stock facility levels reference the visible shell via a
            // collapsible object instead of nesting it below the damage script.
            if (building.CollapsibleObjects != null)
            {
                foreach (var item in building.CollapsibleObjects)
                {
                    if (item == null || item.collapseObject == null)
                        continue;
                    foreach (Renderer renderer in item.collapseObject.GetComponentsInChildren<Renderer>())
                        AddRenderer(result, renderer);
                }
            }
            return result;
        }

        private static void AddRenderer(System.Collections.Generic.List<Renderer> result, Renderer renderer)
        {
            if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer) || !renderer.enabled || !renderer.gameObject.activeInHierarchy || result.Contains(renderer))
                return;
            result.Add(renderer);
        }

        private static bool GetBounds(DestructibleBuilding building, out Bounds bounds, out int layer)
        {
            bounds = new Bounds();
            layer = building.gameObject.layer;
            bool found = false;
            foreach (Renderer renderer in BuildingRenderers(building))
            {
                if (!found)
                {
                    bounds = renderer.bounds;
                    layer = renderer.gameObject.layer;
                    found = true;
                }
                else
                    bounds.Encapsulate(renderer.bounds);
            }
            return found && bounds.size.sqrMagnitude > 25f;
        }

        private static Bounds OrientedBounds(DestructibleBuilding building, Vector3 reference, Quaternion rotation)
        {
            // Project local mesh bounding boxes once into the sign's axes. Using
            // world AABBs twice would substantially overestimate diagonal SPH bounds.
            Quaternion inverse = Quaternion.Inverse(rotation);
            Bounds result = new Bounds();
            bool found = false;
            foreach (Renderer renderer in BuildingRenderers(building))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                var skinned = renderer as SkinnedMeshRenderer;
                bool local = filter != null && filter.sharedMesh != null || skinned != null && skinned.sharedMesh != null;
                Bounds bounds = local ? (skinned != null ? skinned.localBounds : filter.sharedMesh.bounds) : renderer.bounds;
                for (int i = 0; i < 8; ++i)
                {
                    Vector3 corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                    Vector3 point = inverse * ((local ? renderer.transform.TransformPoint(corner) : corner) - reference);
                    if (!found)
                    {
                        result = new Bounds(point, Vector3.zero);
                        found = true;
                    }
                    else
                        result.Encapsulate(point);
                }
            }
            return result;
        }

        private static bool BelongsToBuilding(Collider collider, DestructibleBuilding building)
        {
            if (collider == null || collider.isTrigger)
                return false;
            if (collider.GetComponentInParent<DestructibleBuilding>() == building)
                return true;
            if (building.CollapsibleObjects != null)
            {
                foreach (var item in building.CollapsibleObjects)
                {
                    if (item != null && item.collapseObject != null &&
                        collider.transform.IsChildOf(item.collapseObject.transform))
                        return true;
                }
            }
            return false;
        }

        // Sample the actual near-ground frontage at the intended lateral position.
        // A roof, antenna or other protrusion can extend far beyond the wall.
        private static bool FindFacade(DestructibleBuilding building, Bounds frontage,
            Vector3 reference, Quaternion rotation, float side, out float frontZ, out int collisionLayer)
        {
            frontZ = frontage.min.z;
            collisionLayer = building.gameObject.layer;
            Vector3 forward = rotation * Vector3.forward;
            float startZ = frontage.min.z - 5f;
            float distance = frontage.size.z + 10f;
            Vector3 up = rotation * Vector3.up;
            float floorY = frontage.min.y;
            Vector3 groundProbe = reference + rotation * new Vector3(side, frontage.max.y + 10f, startZ);
            if (Ground(groundProbe, up, frontage.size.y + 110f, out Vector3 groundPoint, out int groundLayer))
                floorY = Vector3.Dot(groundPoint - reference, up);
            float[] heights = { 2f, 4f, 1f, 6f, 8f };
            foreach (float height in heights)
            {
                float y = Mathf.Min(frontage.max.y - .1f, floorY + height);
                Vector3 origin = reference + rotation * new Vector3(side, y, startZ);
                float nearest = float.PositiveInfinity;
                foreach (RaycastHit hit in Physics.RaycastAll(origin, forward, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (!BelongsToBuilding(hit.collider, building) || hit.distance >= nearest ||
                        Vector3.Dot(hit.normal, -forward) < .35f)
                        continue;
                    nearest = hit.distance;
                    collisionLayer = hit.collider.gameObject.layer;
                }
                if (!float.IsPositiveInfinity(nearest))
                {
                    frontZ = startZ + nearest;
                    return true;
                }
            }
            return false;
        }

        // Use the physical ground's layer as a fallback if the facade has no
        // matching collider. Keep the visible meshes on the original render layer.
        private static bool Ground(Vector3 origin, Vector3 up, float distance, out Vector3 point, out int layer)
        {
            point = Vector3.zero;
            layer = 0;
            float nearest = float.PositiveInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, -up, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == null || hit.distance >= nearest || Vector3.Dot(hit.normal, up) < .75f)
                    continue;
                if (hit.collider.GetComponentInParent<Part>() != null || hit.collider.GetComponentInParent<DestructibleBuilding>() != null)
                    continue;
                nearest = hit.distance;
                point = hit.point;
                layer = hit.collider.gameObject.layer;
            }
            return !float.IsPositiveInfinity(nearest);
        }

        private void TryCreate(Site site, CelestialBody body, DestructibleBuilding knownBuilding = null)
        {
            Bounds bounds;
            int layer;
            DestructibleBuilding building = knownBuilding;
            if (building == null)
                building = FindBuilding(site, out bounds, out layer);
            else if (!GetBounds(building, out bounds, out layer))
                return;
            if (building == null)
                return;
            // Avoid creating decorations at distant, unloaded KSC scenery.
            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null || (vessel.transform.position - bounds.center).sqrMagnitude > 5000f * 5000f)
                return;
            Vector3 up = (bounds.center - body.transform.position).normalized;
            double lat = body.GetLatitude(bounds.center);
            double lon = body.GetLongitude(bounds.center);
            double alt = body.GetAltitude(bounds.center);
            Vector3 east = Vector3.ProjectOnPlane((Vector3)(body.GetWorldSurfacePosition(lat, lon + .01, alt) - body.GetWorldSurfacePosition(lat, lon, alt)), up).normalized;
            if (east.sqrMagnitude < .5f)
                return;
            Vector3 north = Vector3.ProjectOnPlane((Vector3)(body.GetWorldSurfacePosition(lat + .01, lon, alt) - body.GetWorldSurfacePosition(lat, lon, alt)), up).normalized;
            if (north.sqrMagnitude < .5f)
                return;
            // Bearing clockwise from geographic north, as configured per site.
            float angle = site.Bearing * Mathf.Deg2Rad;
            Vector3 normal = (north * Mathf.Cos(angle) + east * Mathf.Sin(angle)).normalized;
            Quaternion frontageRotation = Quaternion.LookRotation(-normal, up);
            Vector3 frontageRight = frontageRotation * Vector3.right;
            float facingAngle = site.RotationDegrees * Mathf.Deg2Rad;
            Vector3 signNormal = (north * Mathf.Cos(facingAngle) + east * Mathf.Sin(facingAngle)).normalized;
            Quaternion rotation = Quaternion.LookRotation(-signNormal, up);
            Vector3 right = rotation * Vector3.right;
            float width = site.Width;
            float height = width * SafetySignArtwork.WorldAspect;
            float thickness = Mathf.Clamp(width * .035f, .025f, .18f);
            float diameter = Mathf.Clamp(width * .075f, .035f, .4f);
            float postDepth = thickness + diameter * .25f;
            // Place at the right-hand edge of the facade, within the building
            // frontage rather than beyond the corner. Positive lateral is an
            // inset from that edge; negative values move toward the outside.
            Bounds frontage = OrientedBounds(building, bounds.center, frontageRotation);
            float side = frontage.max.x - width * .5f - site.LateralOffset;
            float facadeSide = Mathf.Clamp(side, frontage.min.x + .1f, frontage.max.x - .1f);
            bool foundFacade = FindFacade(building, frontage, bounds.center, frontageRotation, facadeSide,
                out float frontZ, out int collisionLayer);
            // Measure clearance from the rear of the sign/posts, not its face.
            float frontDistance = site.FrontOffset + postDepth + diameter * .8f;
            Vector3 center = bounds.center + frontageRight * side + normal * (-frontZ + frontDistance) + up * frontage.center.y;
            center += east * site.XOffset + north * site.YOffset;
            Vector3[] feet = new Vector3[2];
            for (int i = 0; i < 2; ++i)
            {
                float x = (i == 0 ? -1f : 1f) * width * .28f;
                Vector3 origin = center + right * x - signNormal * postDepth + up * (frontage.extents.y + 10f);
                if (!Ground(origin, up, 2f * frontage.extents.y + 100f, out feet[i], out int groundLayer))
                    return;
                if (!foundFacade)
                    collisionLayer = groundLayer;
            }
            float floor = Mathf.Max(Vector3.Dot(feet[0] - center, up), Vector3.Dot(feet[1] - center, up));
            center += up * (floor + site.Clearance + height * .5f);
            EnsureArtwork();
            site.Building = building;
            site.Board = new GameObject("KerbalMemorial " + site.Name + " outdoor accident sign");
            site.Board.layer = layer;
            site.Board.transform.position = center;
            site.Board.transform.rotation = rotation;
            // One closed board mesh: only the outward front face carries
            // artwork. The back and four edges use solid steel.
            GameObject backing = Geometry("Textured sign board", site.Board.transform, boardMesh, faceMaterial,
                new Vector3(0f, 0f, thickness * .5f + .002f), new Vector3(width, height, thickness));
            var boardRenderer = backing.GetComponent<MeshRenderer>();
            boardRenderer.sharedMaterials = new[] { faceMaterial, steelMaterial };
            // Explicit renderer-local texture binding also survives a shared
            // material's texture being changed elsewhere.
            var properties = new MaterialPropertyBlock();
            properties.SetTexture("_MainTex", texture);
            properties.SetVector("_MainTex_ST", new Vector4(1f, 1f, 0f, 0f));
            boardRenderer.SetPropertyBlock(properties, 0);
            AddSolidCollider(backing, collisionLayer);
            if (site.PostMounted)
            {
                for (int i = 0; i < 2; ++i)
                {
                    float x = (i == 0 ? -1f : 1f) * width * .28f;
                    float bottom = Vector3.Dot(feet[i] - center, up);
                    float top = -height * .28f;
                    float length = Mathf.Max(.01f, top - bottom);
                    float footHeight = Mathf.Min(diameter * .35f, length);
                    GameObject post = Geometry("Support post " + (i + 1), site.Board.transform, postMesh, steelMaterial,
                        new Vector3(x, bottom + length * .5f, postDepth), new Vector3(diameter, length, diameter));
                    AddSolidCollider(post, collisionLayer);
                    GameObject foot = Geometry("Support foot " + (i + 1), site.Board.transform, postMesh, steelMaterial,
                        new Vector3(x, bottom + footHeight * .5f, postDepth), new Vector3(diameter * 1.6f, footHeight, diameter * 1.6f));
                    AddSolidCollider(foot, collisionLayer);
                }
            }
            // Preserve world size when parenting; inherit facility relocation,
            // not the vessel's moving coordinate frame. Solid colliders inherit it too.
            site.Board.transform.SetParent(building.transform, true);
            site.Warned = false;
            Log.Info(Prefix + site.Name + ": board mesh=" + boardMesh.name +
                "; submeshes=" + boardMesh.subMeshCount + "; material slots=" + boardRenderer.sharedMaterials.Length +
                "; artwork=" + faceMaterial.mainTexture + "; shader=" + faceMaterial.shader.name +
                "; renderer enabled=" + boardRenderer.enabled + "; scale=" + backing.transform.lossyScale);
            Log.Info(Prefix + site.Name + " placed at " + center + " on " + building.id +
                "; frontage=" + (foundFacade ? "wall collider" : "bounds fallback") +
                "; render layer=" + layer + "; collision layer=" + collisionLayer);
        }

        private static GameObject Geometry(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale)
        {
            var item = new GameObject(name);
            item.layer = parent.gameObject.layer;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = item.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return item;
        }

        private static void AddSolidCollider(GameObject item, int layer)
        {
            // Match stock scenery's physics layer without changing visibility.
            var solid = new GameObject("Solid collider");
            solid.layer = layer;
            solid.transform.SetParent(item.transform, false);
            var collider = solid.AddComponent<BoxCollider>();
            collider.size = Vector3.one;
            collider.isTrigger = false;
        }

        private void EnsureArtwork()
        {
            if (faceMaterial != null && steelMaterial != null && boardMesh != null && postMesh != null && texture != null)
                return;
            ReleaseArtwork();
            Shader faceShader = Shader.Find("Unlit/Texture");
            if (faceShader == null || !faceShader.isSupported)
                faceShader = Shader.Find("KSP/Diffuse");
            Shader steelShader = Shader.Find("KSP/Diffuse");
            if (steelShader == null || !steelShader.isSupported)
                steelShader = Shader.Find("Standard");
            if (steelShader == null || !steelShader.isSupported)
                steelShader = Shader.Find("Unlit/Color");
            if (faceShader == null || !faceShader.isSupported || steelShader == null || !steelShader.isSupported)
                throw new InvalidOperationException("No suitable outdoor sign shaders.");
            texture = new Texture2D(SafetySignArtwork.Width, SafetySignArtwork.Height, TextureFormat.RGBA32, true);
            texture.name = "KerbalMemorial outdoor safety sign texture";
            SafetySignArtwork.Initialize(texture);
            SafetySignArtwork.Paint(texture, "--- DAYS");
            faceMaterial = new Material(faceShader);
            faceMaterial.name = "KerbalMemorial outdoor artwork";
            faceMaterial.mainTexture = texture;
            if (faceMaterial.HasProperty("_MainTex"))
            {
                faceMaterial.SetTexture("_MainTex", texture);
                faceMaterial.SetTextureScale("_MainTex", Vector2.one);
                faceMaterial.SetTextureOffset("_MainTex", Vector2.zero);
            }
            if (faceMaterial.HasProperty("_Color"))
                faceMaterial.color = Color.white;
            steelMaterial = new Material(steelShader);
            steelMaterial.color = new Color(.32f, .33f, .36f, 1f);
            boardMesh = MakeTexturedBoard();
            postMesh = EditorAccidentSign.MakeCylinder();
            lastLine = null;
            Log.Info(Prefix + "artwork initialized: " + texture.width + "x" + texture.height +
                "; shader=" + faceShader.name + "; format=" + texture.format + "; mipmaps=" + texture.mipmapCount +
                "; sample colors=" + texture.GetPixel(128, 128) + ", " + texture.GetPixel(1024, 1024) + ", " + texture.GetPixel(1536, 1536));
        }

        // A unit cube with separate vertices on each face. The front is local
        // -Z (outward bearing); material slot 0 carries its artwork only.
        // Slot 1 contains the plain back and four steel edges.
        private static Mesh MakeTexturedBoard()
        {
            var vertices = new System.Collections.Generic.List<Vector3>();
            var normals = new System.Collections.Generic.List<Vector3>();
            var uv = new System.Collections.Generic.List<Vector2>();
            var artwork = new System.Collections.Generic.List<int>();
            var edges = new System.Collections.Generic.List<int>();
            Vector3[] directions = { Vector3.back, Vector3.forward, Vector3.right, Vector3.left, Vector3.up, Vector3.down };
            foreach (Vector3 normal in directions)
            {
                Vector3 vertical = Mathf.Abs(normal.y) > .5f ? Vector3.forward : Vector3.up;
                Vector3 horizontal = Vector3.Cross(normal, vertical);
                int first = vertices.Count;
                vertices.Add((normal - horizontal - vertical) * .5f);
                vertices.Add((normal - horizontal + vertical) * .5f);
                vertices.Add((normal + horizontal + vertical) * .5f);
                vertices.Add((normal + horizontal - vertical) * .5f);
                for (int i = 0; i < 4; ++i)
                    normals.Add(normal);
                uv.Add(new Vector2(0f, 0f));
                uv.Add(new Vector2(0f, 1f));
                uv.Add(new Vector2(1f, 1f));
                uv.Add(new Vector2(1f, 0f));
                var triangles = normal.z < -.5f ? artwork : edges;
                triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
            var result = new Mesh { name = "KerbalMemorial textured outdoor board" };
            result.vertices = vertices.ToArray();
            result.normals = normals.ToArray();
            result.uv = uv.ToArray();
            result.subMeshCount = 2;
            result.SetTriangles(artwork.ToArray(), 0);
            result.SetTriangles(edges.ToArray(), 1);
            result.RecalculateBounds();
            return result;
        }

        private void LoadOverrides()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return;
                ConfigNode file = ConfigNode.Load(SettingsPath);
                ConfigNode root = file == null ? null : file.GetNode(OverrideNode);
                if (root == null)
                    throw new IOException("Invalid outdoor sign settings file.");
                foreach (Site site in sites)
                    site.ApplyOverrides(root.GetNode(site.Name));
            }
            catch (Exception e)
            {
                settingsMessage = "Load failed: " + e.Message;
                Log.Warning(Prefix + settingsMessage);
            }
        }

        private bool SaveOverrides()
        {
            if (!settingsDirty)
                return true;
            try
            {
                var file = new ConfigNode();
                var root = file.AddNode(OverrideNode);
                foreach (Site site in sites)
                {
                    var node = root.AddNode(site.Name);
                    node.AddValue("buildingId", site.BuildingId ?? "");
                    node.AddValue("xOffsetMeters", FormatNumber(site.XOffset));
                    node.AddValue("yOffsetMeters", FormatNumber(site.YOffset));
                    node.AddValue("rotationDegrees", FormatNumber(site.RotationDegrees));
                    node.AddValue("widthMeters", FormatNumber(site.Width));
                    node.AddValue("heightAboveGround", FormatNumber(site.Clearance));
                    node.AddValue("postMounted", site.PostMounted ? "true" : "false");
                }
                string path = SettingsPath;
                string temp = path + ".tmp";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                file.Save(temp);
                if (File.Exists(path))
                    File.Replace(temp, path, path + ".bak");
                else
                    File.Move(temp, path);
                settingsDirty = false;
                settingsMessage = "Saved outdoor sign settings.";
                return true;
            }
            catch (Exception e)
            {
                settingsMessage = "Save failed: " + e.Message;
                Log.Warning(Prefix + settingsMessage);
                return false;
            }
        }

        private static string FormatNumber(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        internal void OpenSettings()
        {
            if (settingsOpen)
                return;
            foreach (Site site in sites)
                site.ResetText();
            showPlacementControls = false;
            showPlacementControlsForLayout = false;
            settingsOpen = true;
        }

        internal void DrawSettingsWindow(int id)
        {
            if (!settingsOpen || !HighLogic.LoadedSceneIsFlight)
                return;
            settingsRect.width = Mathf.Min(580f, Screen.width);
            settingsRect.height = Mathf.Min(480f, Screen.height);
            settingsRect.x = Mathf.Clamp(settingsRect.x, 0f, Screen.width - settingsRect.width);
            settingsRect.y = Mathf.Clamp(settingsRect.y, 0f, Screen.height - settingsRect.height);
            settingsRect = ClickThruBlocker.GUILayoutWindow(id, settingsRect, DrawSettings,
                "Outdoor Accident Sign Settings", GUILayout.Width(settingsRect.width), GUILayout.Height(settingsRect.height));
        }

        private void DrawSettings(int id)
        {
            // Keep the conditional IMGUI layout consistent across input and repaint events.
            if (Event.current.type == EventType.Layout)
            {
                showPlacementControlsForLayout = showPlacementControls;
            }
            showPlacementControls = GUILayout.Toggle(showPlacementControls, "Show position and rotation");
            if (showPlacementControlsForLayout)
            {
                GUILayout.Label("X: east (+) / west (-). Y: north (+) / south (-). Angle: clockwise from north.");
                GUILayout.Label("Wall removes the supports; use the offsets to align the board with a wall.");
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Site", GUILayout.Width(150f));
                    GUILayout.Label("X (m)", GUILayout.Width(122f));
                    GUILayout.Label("Y (m)", GUILayout.Width(122f));
                    GUILayout.Label("Degrees", GUILayout.Width(122f));
                }
            }
            settingsScroll = GUILayout.BeginScrollView(settingsScroll, GUILayout.ExpandHeight(true));
            foreach (Site site in sites)
            {
                bool changed = false;
                using (new GUILayout.HorizontalScope())
                {
                    string status = !site.Enabled ? " (off)" : site.Board == null ? " (unloaded)" : "";
                    GUILayout.Label(site.Name + status, GUILayout.Width(150f));
                    if (showPlacementControlsForLayout)
                    {
                        changed |= NumberField(ref site.XOffset, ref site.XText, -OffsetLimit, OffsetLimit, 1f);
                        changed |= NumberField(ref site.YOffset, ref site.YText, -OffsetLimit, OffsetLimit, 1f);
                        changed |= NumberField(ref site.RotationDegrees, ref site.RotationText, 0f, 360f, 5f);
                    }
                }
                changed |= DimensionSlider("Width", ref site.Width, 1f, 5f);
                changed |= DimensionSlider("Height above ground", ref site.Clearance, .1f, 10f);
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Mounting", GUILayout.Width(150f));
                    bool posts = GUILayout.SelectionGrid(site.PostMounted ? 1 : 0, new[] { "Wall", "Posts" }, 2) == 1;
                    if (posts != site.PostMounted)
                    {
                        site.PostMounted = posts;
                        changed = true;
                    }
                }
                if (changed)
                {
                    site.PlacementChanged = true;
                    settingsDirty = true;
                    settingsMessage = "";
                }
                GUILayout.Space(10f);
            }
            GUILayout.EndScrollView();
            GUILayout.Label(string.IsNullOrEmpty(settingsMessage) ? "Changes apply live. Save or Close to keep them." : settingsMessage);
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Save", GUILayout.Width(95f)))
                    SaveOverrides();
                if (GUILayout.Button("Close", GUILayout.Width(95f)) && SaveOverrides())
                    settingsOpen = false;
                GUILayout.FlexibleSpace();
            }
            GUI.DragWindow();
        }

        // Do not overwrite larger/smaller dimensions loaded from a config just
        // because the narrower settings slider is being displayed.
        private static bool DimensionSlider(string label, ref float value, float min, float max)
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label(label, GUILayout.Width(150f));
                float displayed = Mathf.Clamp(value, min, max);
                float selected = GUILayout.HorizontalSlider(displayed, min, max, GUILayout.ExpandWidth(true));
                bool changed = selected != displayed;
                if (changed)
                {
                    float next = Mathf.Clamp(Mathf.Round(selected * 10f) / 10f, min, max);
                    changed = next != value;
                    value = next;
                }
                GUILayout.Label(SignSettings.Format(value) + " m", GUILayout.Width(64f));
                return changed;
            }
        }

        // Keep incomplete numeric edits (e.g. a lone minus sign) in the field
        // without moving the sign until the value parses and is in range.
        private static bool NumberField(ref float value, ref string text, float min, float max, float step)
        {
            float next = value;
            using (new GUILayout.HorizontalScope(GUILayout.Width(122f)))
            {
                bool decrement = GUILayout.Button("-", GUILayout.Width(23f));
                text = GUILayout.TextField(text ?? FormatNumber(value), 16, GUILayout.Width(56f));
                bool increment = GUILayout.Button("+", GUILayout.Width(23f));
                if (decrement || increment)
                {
                    next = Mathf.Clamp(value + (increment ? step : -step), min, max);
                    text = FormatNumber(next);
                }
                else if (SignSettings.TryValue(text, min, max, out float parsed))
                    next = parsed;
            }
            if (next == value)
                return false;
            value = next;
            return true;
        }

        private void ReleaseArtwork()
        {
            if (faceMaterial != null)
                Destroy(faceMaterial);
            if (steelMaterial != null)
                Destroy(steelMaterial);
            if (boardMesh != null)
                Destroy(boardMesh);
            if (postMesh != null)
                Destroy(postMesh);
            if (texture != null)
                Destroy(texture);
            faceMaterial = null;
            steelMaterial = null;
            boardMesh = null;
            postMesh = null;
            texture = null;
            lastLine = null;
        }

        private void OnDestroy()
        {
            if (settingsDirty)
                SaveOverrides();
            if (Instance == this)
                Instance = null;
            foreach (Site site in sites)
                site.Clear();
            ReleaseArtwork();
        }

        private sealed class Site
        {
            internal readonly string Name;
            internal float XOffset, YOffset, RotationDegrees;
            internal string XText, YText, RotationText;
            internal bool PlacementChanged;
            internal float Bearing = 90f, Width = 4f, Clearance = 2f, FrontOffset = 1f, LateralOffset = 1f;
            internal bool Enabled = true, Warned;
            internal bool PostMounted = true;
            internal string BuildingId;
            internal GameObject Board;
            internal DestructibleBuilding Building;

            internal Site(string name)
            {
                Name = name;
            }

            internal void Clear()
            {
                if (Board != null)
                {
                    Board.SetActive(false);
                    UnityEngine.Object.Destroy(Board);
                }
                Board = null;
                Building = null;
            }

            internal void Load(ConfigNode node)
            {
                if (bool.TryParse(node.GetValue("enabled"), out bool enabled))
                    Enabled = enabled;
                BuildingId = node.GetValue("buildingId");
                if (bool.TryParse(node.GetValue("postMounted"), out bool posts))
                    PostMounted = posts;
                Bearing = Read(node, "frontBearing", Bearing, 0f, 360f);
                Width = Read(node, "widthMeters", Width, .5f, 30f);
                Clearance = Read(node, "heightAboveGround", Clearance, .1f, 20f);
                FrontOffset = Read(node, "frontOffsetMeters", FrontOffset, .1f, 100f);
                LateralOffset = Read(node, "lateralOffsetMeters", LateralOffset, -100f, 100f);
                XOffset = Read(node, "xOffsetMeters", 0f, -OffsetLimit, OffsetLimit);
                YOffset = Read(node, "yOffsetMeters", 0f, -OffsetLimit, OffsetLimit);
                RotationDegrees = Read(node, "rotationDegrees", Bearing, 0f, 360f);
            }

            internal void ApplyOverrides(ConfigNode node)
            {
                if (node == null)
                    return;
                if (!string.Equals(node.GetValue("buildingId") ?? "", BuildingId ?? "", StringComparison.OrdinalIgnoreCase))
                    return;
                XOffset = Read(node, "xOffsetMeters", XOffset, -OffsetLimit, OffsetLimit);
                YOffset = Read(node, "yOffsetMeters", YOffset, -OffsetLimit, OffsetLimit);
                RotationDegrees = Read(node, "rotationDegrees", RotationDegrees, 0f, 360f);
                Width = Read(node, "widthMeters", Width, .5f, 30f);
                Clearance = Read(node, "heightAboveGround", Clearance, .1f, 20f);
                if (bool.TryParse(node.GetValue("postMounted"), out bool posts))
                    PostMounted = posts;
            }

            internal void ResetText()
            {
                XText = FormatNumber(XOffset);
                YText = FormatNumber(YOffset);
                RotationText = FormatNumber(RotationDegrees);
            }

            private static float Read(ConfigNode node, string key, float fallback, float min, float max)
            {
                if (float.TryParse(node.GetValue(key), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && !float.IsNaN(value) && !float.IsInfinity(value))
                    return Mathf.Clamp(value, min, max);
                return fallback;
            }
        }
    }
}
