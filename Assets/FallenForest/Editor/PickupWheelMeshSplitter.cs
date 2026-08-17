#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FallenForest.Cinematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FallenForest.EditorTools
{
    /// <summary>
    /// The supplied Pickup Afghanistan FBX is a merged mesh. This tool detects four disconnected
    /// wheel islands, extracts them into independent mesh assets and rebuilds Pickup_Final as a
    /// Rigidbody + four WheelCollider vehicle. No substitute vehicle geometry is generated.
    /// </summary>
    public static class PickupWheelMeshSplitter
    {
        private const string Root = "Assets/FallenForest";
        private const string ModelPath = Root + "/Art/Vehicles/Pickup/Source/Pickup Afghanistan.fbx";
        private const string GeneratedDir = Root + "/Generated/Pickup";
        private const string PrefabPath = FinalUserAssetPrefabBuilder.PickupPrefab;

        private sealed class ComponentInfo
        {
            public int root;
            public readonly List<int> vertices = new();
            public Bounds bounds;
            public float score;
        }

        private sealed class WheelVisual
        {
            public Transform visual;
            public WheelCollider collider;
            public Vector3 rootLocalCenter;
            public float radius;
            public float forwardProjection;
            public float rightProjection;
        }

        [MenuItem("Fallen Forest/Vehicles/Rebuild Physics Pickup From Merged FBX")]
        public static void BuildFromMenu()
        {
            BuildOrThrow();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static GameObject BuildIfAvailable()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath) == null)
            {
                Debug.LogWarning($"Fallen Forest: physics pickup waiting for exact source FBX: {ModelPath}");
                return null;
            }
            return BuildOrThrow();
        }

        public static GameObject BuildOrThrow()
        {
            EnsureReadableModel();
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null)
                throw new InvalidOperationException($"Pickup model is missing: {ModelPath}. Import the canonical archive first.");

            Directory.CreateDirectory(GeneratedDir);
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath) ?? Root + "/Prefabs");
            AssetDatabase.Refresh();

            GameObject root = new("Pickup_Final");
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 2350f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            GameObject visualRoot = PrefabUtility.InstantiatePrefab(source, root.transform) as GameObject;
            if (visualRoot == null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw new InvalidOperationException("Could not instantiate pickup FBX.");
            }
            visualRoot.name = "PickupVisual";

            MeshFilter sourceFilter = FindLargestMeshFilter(visualRoot);
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw new InvalidOperationException("Pickup FBX has no readable mesh.");
            }

            MeshRenderer sourceRenderer = sourceFilter.GetComponent<MeshRenderer>();
            if (sourceRenderer == null)
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw new InvalidOperationException("Pickup mesh has no MeshRenderer.");
            }

            Mesh sourceMesh = sourceFilter.sharedMesh;
            List<ComponentInfo> components = FindConnectedComponents(sourceMesh);
            List<ComponentInfo> wheels = PickFourWheelComponents(components, sourceMesh.bounds);
            if (wheels.Count != 4)
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw new InvalidOperationException($"Expected four wheel mesh islands, detected {wheels.Count}.");
            }

            HashSet<int> wheelRoots = new(wheels.Select(w => w.root));
            Mesh bodyMesh = BuildSubsetMesh(sourceMesh, components, c => !wheelRoots.Contains(c.root), Vector3.zero, "Pickup_Body_Split");
            SaveMesh(bodyMesh, GeneratedDir + "/Pickup_Body_Split.asset");
            sourceFilter.sharedMesh = bodyMesh;

            var wheelVisuals = new List<WheelVisual>(4);
            for (int i = 0; i < wheels.Count; i++)
            {
                ComponentInfo component = wheels[i];
                Vector3 localCenter = component.bounds.center;
                Mesh wheelMesh = BuildSubsetMesh(sourceMesh, components, c => c.root == component.root, localCenter, $"Pickup_Wheel_{i + 1}");
                SaveMesh(wheelMesh, GeneratedDir + $"/Pickup_Wheel_{i + 1}.asset");

                GameObject wheelObject = new($"WheelVisual_{i + 1:00}");
                wheelObject.transform.SetParent(sourceFilter.transform, false);
                wheelObject.transform.localPosition = localCenter;
                wheelObject.transform.localRotation = Quaternion.identity;
                wheelObject.transform.localScale = Vector3.one;

                MeshFilter wheelFilter = wheelObject.AddComponent<MeshFilter>();
                wheelFilter.sharedMesh = wheelMesh;
                MeshRenderer wheelRenderer = wheelObject.AddComponent<MeshRenderer>();
                wheelRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
                wheelRenderer.shadowCastingMode = ShadowCastingMode.On;
                wheelRenderer.receiveShadows = true;

                Vector3 rootLocalCenter = root.transform.InverseTransformPoint(sourceFilter.transform.TransformPoint(localCenter));
                float radius = EstimateWheelRadiusInRootSpace(component.bounds, sourceFilter.transform, root.transform);
                wheelVisuals.Add(new WheelVisual
                {
                    visual = wheelObject.transform,
                    rootLocalCenter = rootLocalCenter,
                    radius = radius
                });
            }

            AlignVehicleForward(root.transform, visualRoot.transform, wheelVisuals);
            RefreshRootLocalWheelCenters(root.transform, wheelVisuals);
            ClassifyWheelCorners(wheelVisuals);

            foreach (WheelVisual wheel in wheelVisuals)
            {
                GameObject colliderObject = new("WheelCollider");
                colliderObject.transform.SetParent(root.transform, false);
                colliderObject.transform.localPosition = wheel.rootLocalCenter;
                colliderObject.transform.localRotation = Quaternion.identity;
                wheel.collider = colliderObject.AddComponent<WheelCollider>();
                wheel.collider.radius = wheel.radius;
            }

            WheelVisual frontLeft = FindCorner(wheelVisuals, front: true, left: true);
            WheelVisual frontRight = FindCorner(wheelVisuals, front: true, left: false);
            WheelVisual rearLeft = FindCorner(wheelVisuals, front: false, left: true);
            WheelVisual rearRight = FindCorner(wheelVisuals, front: false, left: false);

            AddBodyCollider(root, visualRoot);
            Light[] headlights = CreateHeadlights(root.transform, wheelVisuals, true);
            Light[] tailLights = CreateHeadlights(root.transform, wheelVisuals, false);

            CinematicPickupVehicle vehicle = root.AddComponent<CinematicPickupVehicle>();
            SerializedObject so = new(vehicle);
            ConfigureWheel(so.FindProperty("frontLeft"), frontLeft, steer: true);
            ConfigureWheel(so.FindProperty("frontRight"), frontRight, steer: true);
            ConfigureWheel(so.FindProperty("rearLeft"), rearLeft, steer: false);
            ConfigureWheel(so.FindProperty("rearRight"), rearRight, steer: false);

            float averageRadius = wheelVisuals.Average(w => w.radius);
            SetFloat(so, "wheelRadius", Mathf.Clamp(averageRadius, .22f, .75f));
            SetFloat(so, "suspensionDistance", Mathf.Clamp(averageRadius * .55f, .16f, .36f));
            SetFloat(so, "spring", 36000f);
            SetFloat(so, "damper", 5200f);
            SetFloat(so, "motorTorque", 1750f);
            SetFloat(so, "brakeTorque", 5200f);
            SetFloat(so, "cruiseSpeed", 10.8f);
            SetFloat(so, "approachSpeed", 4.8f);
            SetObjectArray(so, "headlights", headlights);
            SetObjectArray(so, "tailLights", tailLights);
            so.ApplyModifiedPropertiesWithoutUndo();

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                AssetDatabase.DeleteAsset(PrefabPath);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            if (prefab == null)
                throw new InvalidOperationException("Failed to save Pickup_Final prefab.");

            Debug.Log($"Fallen Forest: exact pickup split into body + 4 independent wheels and saved to {PrefabPath}.");
            return prefab;
        }

        private static void EnsureReadableModel()
        {
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) return;
            bool changed = false;
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                changed = true;
            }
            if (importer.meshCompression != ModelImporterMeshCompression.Off)
            {
                importer.meshCompression = ModelImporterMeshCompression.Off;
                changed = true;
            }
            if (changed) importer.SaveAndReimport();
        }

        private static MeshFilter FindLargestMeshFilter(GameObject root)
        {
            MeshFilter best = null;
            int vertices = -1;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.sharedMesh.vertexCount <= vertices) continue;
                best = filter;
                vertices = filter.sharedMesh.vertexCount;
            }
            return best;
        }

        private static List<ComponentInfo> FindConnectedComponents(Mesh mesh)
        {
            int vertexCount = mesh.vertexCount;
            int[] parent = new int[vertexCount];
            for (int i = 0; i < vertexCount; i++) parent[i] = i;

            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                int[] triangles = mesh.GetTriangles(sub);
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    Union(parent, triangles[i], triangles[i + 1]);
                    Union(parent, triangles[i], triangles[i + 2]);
                }
            }

            Vector3[] vertices = mesh.vertices;
            Dictionary<int, ComponentInfo> groups = new();
            for (int i = 0; i < vertexCount; i++)
            {
                int componentRoot = Find(parent, i);
                if (!groups.TryGetValue(componentRoot, out ComponentInfo component))
                {
                    component = new ComponentInfo { root = componentRoot };
                    groups.Add(componentRoot, component);
                }
                component.vertices.Add(i);
            }

            foreach (ComponentInfo component in groups.Values)
            {
                Bounds bounds = new(vertices[component.vertices[0]], Vector3.zero);
                for (int i = 1; i < component.vertices.Count; i++) bounds.Encapsulate(vertices[component.vertices[i]]);
                component.bounds = bounds;
            }
            return groups.Values.ToList();
        }

        private static List<ComponentInfo> PickFourWheelComponents(List<ComponentInfo> components, Bounds whole)
        {
            float longSize = Mathf.Max(whole.size.x, whole.size.z);
            float bottomBand = whole.min.y + whole.size.y * .42f;

            foreach (ComponentInfo c in components)
            {
                Vector3 size = c.bounds.size;
                float max = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                float min = Mathf.Max(.0001f, Mathf.Min(size.x, Mathf.Min(size.y, size.z)));
                float compactness = min / Mathf.Max(.0001f, max);
                float fraction = max / Mathf.Max(.0001f, longSize);
                float targetSize = 1f - Mathf.Clamp01(Mathf.Abs(fraction - .16f) / .14f);
                float low = c.bounds.center.y <= bottomBand
                    ? 1f
                    : Mathf.Clamp01(1f - (c.bounds.center.y - bottomBand) / Mathf.Max(.001f, whole.size.y * .4f));
                float vertexScore = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(28f, 90f, c.vertices.Count)) *
                                    (1f - Mathf.Clamp01(Mathf.InverseLerp(260f, 520f, c.vertices.Count)));
                c.score = compactness * 3.2f + targetSize * 2.8f + low * 3.3f + vertexScore * 1.4f;
            }

            List<ComponentInfo> ranked = components
                .Where(c => c.vertices.Count >= 24)
                .OrderByDescending(c => c.score)
                .Take(14)
                .ToList();

            List<ComponentInfo> chosen = new();
            foreach (ComponentInfo candidate in ranked)
            {
                if (chosen.Count == 0)
                {
                    chosen.Add(candidate);
                    continue;
                }

                float minDistance = chosen.Min(c => Vector2.Distance(
                    new Vector2(c.bounds.center.x, c.bounds.center.z),
                    new Vector2(candidate.bounds.center.x, candidate.bounds.center.z)));
                if (minDistance < longSize * .16f) continue;
                chosen.Add(candidate);
                if (chosen.Count == 4) break;
            }

            if (chosen.Count != 4) chosen = ranked.Take(4).ToList();
            return chosen;
        }

        private static Mesh BuildSubsetMesh(
            Mesh source,
            List<ComponentInfo> components,
            Func<ComponentInfo, bool> include,
            Vector3 recenter,
            string name)
        {
            int[] vertexToRoot = new int[source.vertexCount];
            foreach (ComponentInfo component in components)
                foreach (int vertex in component.vertices)
                    vertexToRoot[vertex] = component.root;

            HashSet<int> includedRoots = new(components.Where(include).Select(c => c.root));
            List<int>[] oldTriangles = new List<int>[source.subMeshCount];
            HashSet<int> used = new();

            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                oldTriangles[sub] = new List<int>();
                int[] triangles = source.GetTriangles(sub);
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    int a = triangles[i];
                    int componentRoot = vertexToRoot[a];
                    if (!includedRoots.Contains(componentRoot)) continue;
                    oldTriangles[sub].Add(a);
                    oldTriangles[sub].Add(triangles[i + 1]);
                    oldTriangles[sub].Add(triangles[i + 2]);
                    used.Add(a);
                    used.Add(triangles[i + 1]);
                    used.Add(triangles[i + 2]);
                }
            }

            List<int> oldIndices = used.OrderBy(i => i).ToList();
            Dictionary<int, int> remap = new(oldIndices.Count);
            for (int i = 0; i < oldIndices.Count; i++) remap.Add(oldIndices[i], i);

            Vector3[] srcVertices = source.vertices;
            Vector3[] srcNormals = source.normals;
            Vector4[] srcTangents = source.tangents;
            Color[] srcColors = source.colors;
            Vector2[] srcUv = source.uv;
            Vector2[] srcUv2 = source.uv2;

            var vertices = new Vector3[oldIndices.Count];
            Vector3[] normals = srcNormals.Length == srcVertices.Length ? new Vector3[oldIndices.Count] : Array.Empty<Vector3>();
            Vector4[] tangents = srcTangents.Length == srcVertices.Length ? new Vector4[oldIndices.Count] : Array.Empty<Vector4>();
            Color[] colors = srcColors.Length == srcVertices.Length ? new Color[oldIndices.Count] : Array.Empty<Color>();
            Vector2[] uv = srcUv.Length == srcVertices.Length ? new Vector2[oldIndices.Count] : Array.Empty<Vector2>();
            Vector2[] uv2 = srcUv2.Length == srcVertices.Length ? new Vector2[oldIndices.Count] : Array.Empty<Vector2>();

            for (int i = 0; i < oldIndices.Count; i++)
            {
                int old = oldIndices[i];
                vertices[i] = srcVertices[old] - recenter;
                if (normals.Length > 0) normals[i] = srcNormals[old];
                if (tangents.Length > 0) tangents[i] = srcTangents[old];
                if (colors.Length > 0) colors[i] = srcColors[old];
                if (uv.Length > 0) uv[i] = srcUv[old];
                if (uv2.Length > 0) uv2[i] = srcUv2[old];
            }

            Mesh mesh = new()
            {
                name = name,
                indexFormat = oldIndices.Count > 65534 ? IndexFormat.UInt32 : IndexFormat.UInt16,
                vertices = vertices,
                subMeshCount = source.subMeshCount
            };
            if (normals.Length > 0) mesh.normals = normals;
            if (tangents.Length > 0) mesh.tangents = tangents;
            if (colors.Length > 0) mesh.colors = colors;
            if (uv.Length > 0) mesh.uv = uv;
            if (uv2.Length > 0) mesh.uv2 = uv2;

            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                List<int> remapped = new(oldTriangles[sub].Count);
                foreach (int old in oldTriangles[sub]) remapped.Add(remap[old]);
                mesh.SetTriangles(remapped, sub, false);
            }
            if (normals.Length == 0) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void SaveMesh(Mesh mesh, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
        }

        private static float EstimateWheelRadiusInRootSpace(Bounds component, Transform meshTransform, Transform root)
        {
            Vector3 center = root.InverseTransformPoint(meshTransform.TransformPoint(component.center));
            Vector3 ex = root.InverseTransformPoint(meshTransform.TransformPoint(component.center + Vector3.right * component.extents.x)) - center;
            Vector3 ey = root.InverseTransformPoint(meshTransform.TransformPoint(component.center + Vector3.up * component.extents.y)) - center;
            Vector3 ez = root.InverseTransformPoint(meshTransform.TransformPoint(component.center + Vector3.forward * component.extents.z)) - center;
            float[] extents = { ex.magnitude, ey.magnitude, ez.magnitude };
            Array.Sort(extents);
            return Mathf.Max(.12f, extents[1]);
        }

        private static void AlignVehicleForward(Transform root, Transform visualRoot, List<WheelVisual> wheels)
        {
            Vector2 mean = Vector2.zero;
            foreach (WheelVisual wheel in wheels) mean += new Vector2(wheel.rootLocalCenter.x, wheel.rootLocalCenter.z);
            mean /= wheels.Count;

            float xx = 0f, xz = 0f, zz = 0f;
            foreach (WheelVisual wheel in wheels)
            {
                Vector2 d = new(wheel.rootLocalCenter.x - mean.x, wheel.rootLocalCenter.z - mean.y);
                xx += d.x * d.x;
                xz += d.x * d.y;
                zz += d.y * d.y;
            }
            float angle = .5f * Mathf.Atan2(2f * xz, xx - zz);
            Vector2 axisA = new(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 axisB = new(-axisA.y, axisA.x);
            Vector2 forward = Variance(wheels, mean, axisA) >= Variance(wheels, mean, axisB) ? axisA : axisB;
            float yaw = Mathf.Atan2(forward.x, forward.y) * Mathf.Rad2Deg;
            visualRoot.localRotation = Quaternion.Euler(0f, -yaw, 0f) * visualRoot.localRotation;
        }

        private static float Variance(List<WheelVisual> wheels, Vector2 mean, Vector2 axis)
        {
            float sum = 0f;
            foreach (WheelVisual wheel in wheels)
            {
                Vector2 d = new(wheel.rootLocalCenter.x - mean.x, wheel.rootLocalCenter.z - mean.y);
                float p = Vector2.Dot(d, axis);
                sum += p * p;
            }
            return sum;
        }

        private static void RefreshRootLocalWheelCenters(Transform root, List<WheelVisual> wheels)
        {
            foreach (WheelVisual wheel in wheels) wheel.rootLocalCenter = root.InverseTransformPoint(wheel.visual.position);
        }

        private static void ClassifyWheelCorners(List<WheelVisual> wheels)
        {
            float meanZ = wheels.Average(w => w.rootLocalCenter.z);
            float meanX = wheels.Average(w => w.rootLocalCenter.x);
            foreach (WheelVisual wheel in wheels)
            {
                wheel.forwardProjection = wheel.rootLocalCenter.z - meanZ;
                wheel.rightProjection = wheel.rootLocalCenter.x - meanX;
            }
        }

        private static WheelVisual FindCorner(List<WheelVisual> wheels, bool front, bool left)
        {
            WheelVisual best = null;
            float bestScore = float.NegativeInfinity;
            foreach (WheelVisual wheel in wheels)
            {
                float forward = front ? wheel.forwardProjection : -wheel.forwardProjection;
                float side = left ? -wheel.rightProjection : wheel.rightProjection;
                float score = forward + side * .65f;
                if (score <= bestScore) continue;
                best = wheel;
                bestScore = score;
            }
            if (best == null) throw new InvalidOperationException("Could not classify pickup wheel corner.");
            return best;
        }

        private static void ConfigureWheel(SerializedProperty property, WheelVisual wheel, bool steer)
        {
            if (property == null || wheel == null) return;
            SerializedProperty visual = property.FindPropertyRelative("visual");
            SerializedProperty collider = property.FindPropertyRelative("collider");
            SerializedProperty steerProperty = property.FindPropertyRelative("steer");
            SerializedProperty driven = property.FindPropertyRelative("driven");
            if (visual != null) visual.objectReferenceValue = wheel.visual;
            if (collider != null) collider.objectReferenceValue = wheel.collider;
            if (steerProperty != null) steerProperty.boolValue = steer;
            if (driven != null) driven.boolValue = true;
        }

        private static void AddBodyCollider(GameObject root, GameObject visualRoot)
        {
            Renderer[] renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds world = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) world.Encapsulate(renderers[i].bounds);

            Vector3 center = root.transform.InverseTransformPoint(world.center);
            Vector3 size = new(
                world.size.x / Mathf.Max(.0001f, Mathf.Abs(root.transform.lossyScale.x)),
                world.size.y / Mathf.Max(.0001f, Mathf.Abs(root.transform.lossyScale.y)),
                world.size.z / Mathf.Max(.0001f, Mathf.Abs(root.transform.lossyScale.z)));
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = center + Vector3.up * size.y * .07f;
            box.size = new Vector3(size.x * .78f, size.y * .72f, size.z * .86f);
        }

        private static Light[] CreateHeadlights(Transform root, List<WheelVisual> wheels, bool front)
        {
            float frontZ = front ? wheels.Max(w => w.rootLocalCenter.z) : wheels.Min(w => w.rootLocalCenter.z);
            float leftX = wheels.Min(w => w.rootLocalCenter.x);
            float rightX = wheels.Max(w => w.rootLocalCenter.x);
            float y = wheels.Average(w => w.rootLocalCenter.y) + wheels.Average(w => w.radius) * .72f;
            float zOffset = wheels.Average(w => w.radius) * (front ? .52f : -.52f);
            float xInset = Mathf.Abs(rightX - leftX) * .18f;

            Light a = CreateVehicleLight(root, front ? "Headlight_L" : "TailLight_L", new Vector3(leftX + xInset, y, frontZ + zOffset), front);
            Light b = CreateVehicleLight(root, front ? "Headlight_R" : "TailLight_R", new Vector3(rightX - xInset, y, frontZ + zOffset), front);
            return new[] { a, b };
        }

        private static Light CreateVehicleLight(Transform root, string name, Vector3 localPosition, bool front)
        {
            GameObject go = new(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = front ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = front ? new Color(1f, .92f, .78f) : new Color(1f, .04f, .015f);
            light.intensity = front ? 7.5f : 1.2f;
            light.range = front ? 32f : 5f;
            light.spotAngle = front ? 52f : 70f;
            light.innerSpotAngle = front ? 28f : 45f;
            light.shadows = front ? LightShadows.Soft : LightShadows.None;
            light.enabled = false;
            return light;
        }

        private static void SetFloat(SerializedObject so, string name, float value)
        {
            SerializedProperty property = so.FindProperty(name);
            if (property != null) property.floatValue = value;
        }

        private static void SetObjectArray<T>(SerializedObject so, string name, T[] values) where T : UnityEngine.Object
        {
            SerializedProperty array = so.FindProperty(name);
            if (array == null) return;
            array.arraySize = values?.Length ?? 0;
            for (int i = 0; i < array.arraySize; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static int Find(int[] parent, int value)
        {
            while (parent[value] != value)
            {
                parent[value] = parent[parent[value]];
                value = parent[value];
            }
            return value;
        }

        private static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a);
            int rb = Find(parent, b);
            if (ra != rb) parent[rb] = ra;
        }
    }
}
#endif
