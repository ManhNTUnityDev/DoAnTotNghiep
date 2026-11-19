#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace ChaseGame.Editor
{
    /// <summary>
    /// Automatically configures physics colliders, NavMesh modifiers, invisible perimeter boundary
    /// walls, interior unreachable-island blockers, and bakes the NavMeshSurface for Map_Final-6.
    /// </summary>
    public static class MapCollisionSetup
    {
        private const string MapObjectName = "Map_Final-6";
        private const string BackgroundGroundPath = "Map_Background/Ground";
        private const string BoundaryRootName = "MapBoundary";
        private const string InteriorBlockerRootName = "InteriorNavMeshBlockers";
        private const string NavMeshAssetPath = "Assets/NavMeshData/Game_Map_NavMesh.asset";

        // Outer fence bounds of House-Fence.001 in Map_Final-6
        private const float FenceMinX = -31.05f;
        private const float FenceMaxX = 29.21f;
        private const float FenceMinZ = -25.24f;
        private const float FenceMaxZ = 25.36f;

        private static readonly HashSet<string> WalkableMeshNames = new HashSet<string>
        {
            "Ground",
            "Wooden_bridge",
            "Cube.086"
        };

        private static readonly HashSet<string> PassThroughMeshNames = new HashSet<string>
        {
            "Pond",
            "Pond-Leaves",
            "Pond-Leaves.001",
            "Plane",
            "Plane.007",
            "Plane.010",
            "Plane.017",
            "Plane.026",
            "Barn-Toilet",
            "Cube.026",
            "Cylinder.028",
            "Lake-Rock_low.002",
            "Exit_Sign",
            "Door-Lock-Bar"
        };

        private static readonly string[] PassThroughPrefixes =
        {
            "Grass_",
            "Grain",
            "Tomato",
            "Eggplant",
            "Pumpkin",
            "Plant.",
            "Ads ",
            "Pin",
            "game_low"
        };

        [MenuItem("Tools/ChaseGame/Setup Map Colliders & Bake NavMesh")]
        public static void RunFromMenu()
        {
            string summary = ConfigureMapAndBake();
            Debug.Log("[MapCollisionSetup] " + summary);
        }

        public static string ConfigureMapAndBake()
        {
            GameObject map = GameObject.Find(MapObjectName);
            if (map == null)
            {
                return $"Error: '{MapObjectName}' not found in the active scene.";
            }

            CleanupBackgroundNavMesh();
            RemoveGeneratedHelpers(map.transform);

            int walkableCount = 0;
            int obstacleCount = 0;
            int passThroughCount = 0;

            foreach (MeshFilter mf in map.GetComponentsInChildren<MeshFilter>(true))
            {
                GameObject go = mf.gameObject;
                string name = go.name;

                bool isPassThrough = IsPassThroughObject(name);
                bool isWalkable = !isPassThrough && WalkableMeshNames.Contains(name);

                Collider existingCol = go.GetComponent<Collider>();
                NavMeshModifier mod = go.GetComponent<NavMeshModifier>();

                if (isPassThrough)
                {
                    if (existingCol != null)
                    {
                        Object.DestroyImmediate(existingCol);
                    }

                    if (mod == null)
                    {
                        mod = go.AddComponent<NavMeshModifier>();
                    }

                    mod.ignoreFromBuild = true;
                    passThroughCount++;
                }
                else
                {
                    MeshCollider mc = go.GetComponent<MeshCollider>();
                    if (mc == null)
                    {
                        if (existingCol != null)
                        {
                            Object.DestroyImmediate(existingCol);
                        }

                        mc = go.AddComponent<MeshCollider>();
                    }

                    mc.sharedMesh = mf.sharedMesh;

                    if (mod == null)
                    {
                        mod = go.AddComponent<NavMeshModifier>();
                    }

                    mod.ignoreFromBuild = false;
                    mod.overrideArea = true;
                    mod.area = isWalkable ? 0 : 1; // 0 = Walkable, 1 = Not Walkable

                    if (isWalkable)
                    {
                        walkableCount++;
                    }
                    else
                    {
                        obstacleCount++;
                    }
                }
            }

            foreach (SkinnedMeshRenderer smr in map.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                NavMeshModifier mod = smr.gameObject.GetComponent<NavMeshModifier>();
                if (mod == null)
                {
                    mod = smr.gameObject.AddComponent<NavMeshModifier>();
                }

                mod.ignoreFromBuild = true;
            }

            BuildPerimeterBoundary(map.transform);
            SnapSpawnPointsToGround(map.transform);

            NavMeshSurface surface = ConfigureSurface(map);
            surface.BuildNavMesh();

            int blockerCount = BuildUnreachableInteriorBlockers(map.transform);
            if (blockerCount > 0)
            {
                surface.BuildNavMesh();
            }

            PersistNavMeshAsset(surface);

            EditorUtility.SetDirty(map);
            EditorSceneManager.MarkSceneDirty(map.scene);
            EditorSceneManager.SaveScene(map.scene);
            AssetDatabase.SaveAssets();

            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            return $"Configured '{MapObjectName}': Walkable={walkableCount}, Obstacles={obstacleCount}, " +
                   $"PassThrough={passThroughCount}, InteriorBlockers={blockerCount}. " +
                   $"Baked NavMesh: verts={tri.vertices.Length}, tris={tri.indices.Length / 3}.";
        }

        private static bool IsPassThroughObject(string name)
        {
            if (PassThroughMeshNames.Contains(name))
            {
                return true;
            }

            for (int i = 0; i < PassThroughPrefixes.Length; i++)
            {
                if (name.StartsWith(PassThroughPrefixes[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static void CleanupBackgroundNavMesh()
        {
            GameObject bgGround = GameObject.Find(BackgroundGroundPath);
            if (bgGround == null)
            {
                return;
            }

            NavMeshSurface oldSurface = bgGround.GetComponent<NavMeshSurface>();
            if (oldSurface != null)
            {
                oldSurface.RemoveData();
                Object.DestroyImmediate(oldSurface);
            }

            MeshCollider oldCollider = bgGround.GetComponent<MeshCollider>();
            if (oldCollider != null)
            {
                Object.DestroyImmediate(oldCollider);
            }

            EditorUtility.SetDirty(bgGround);
        }

        private static void RemoveGeneratedHelpers(Transform mapTransform)
        {
            Transform oldBoundary = mapTransform.Find(BoundaryRootName);
            if (oldBoundary != null)
            {
                Object.DestroyImmediate(oldBoundary.gameObject);
            }

            Transform oldBlockers = mapTransform.Find(InteriorBlockerRootName);
            if (oldBlockers != null)
            {
                Object.DestroyImmediate(oldBlockers.gameObject);
            }
        }

        private static void BuildPerimeterBoundary(Transform mapTransform)
        {
            GameObject boundaryRoot = new GameObject(BoundaryRootName);
            boundaryRoot.transform.SetParent(mapTransform, false);

            float centerX = (FenceMinX + FenceMaxX) * 0.5f;
            float centerZ = (FenceMinZ + FenceMaxZ) * 0.5f;
            float sizeX = FenceMaxX - FenceMinX;
            float sizeZ = FenceMaxZ - FenceMinZ;
            const float wallHeight = 8f;
            const float wallThickness = 0.6f;
            float wallCenterY = wallHeight * 0.5f - 1.5f;

            CreateBoundaryWall(
                boundaryRoot.transform,
                "Wall_West",
                new Vector3(FenceMinX, wallCenterY, centerZ),
                new Vector3(wallThickness, wallHeight, sizeZ + wallThickness * 2f));

            CreateBoundaryWall(
                boundaryRoot.transform,
                "Wall_East",
                new Vector3(FenceMaxX, wallCenterY, centerZ),
                new Vector3(wallThickness, wallHeight, sizeZ + wallThickness * 2f));

            CreateBoundaryWall(
                boundaryRoot.transform,
                "Wall_South",
                new Vector3(centerX, wallCenterY, FenceMinZ),
                new Vector3(sizeX + wallThickness * 2f, wallHeight, wallThickness));

            CreateBoundaryWall(
                boundaryRoot.transform,
                "Wall_North",
                new Vector3(centerX, wallCenterY, FenceMaxZ),
                new Vector3(sizeX + wallThickness * 2f, wallHeight, wallThickness));
        }

        private static void CreateBoundaryWall(Transform parent, string name, Vector3 worldPos, Vector3 size)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(parent, false);
            wall.transform.position = worldPos;

            BoxCollider bc = wall.AddComponent<BoxCollider>();
            bc.size = size;

            NavMeshModifier mod = wall.AddComponent<NavMeshModifier>();
            mod.ignoreFromBuild = false;
            mod.overrideArea = true;
            mod.area = 1; // Not Walkable
        }

        private static void SnapSpawnPointsToGround(Transform mapTransform)
        {
            // Ensure RunnerSpawn_3 does not overlap the boulder Lake-Rock_2_low.001 at (6, y, 8)
            GameObject runnerSpawn3 = GameObject.Find("RunnerSpawn_3");
            if (runnerSpawn3 != null && Mathf.Abs(runnerSpawn3.transform.position.x - 6f) < 0.2f)
            {
                runnerSpawn3.transform.position = new Vector3(4.80f, runnerSpawn3.transform.position.y, 8.00f);
                EditorUtility.SetDirty(runnerSpawn3.transform);
            }

            Transform groundTransform = mapTransform.Find("Ground");
            Collider groundCol = groundTransform != null ? groundTransform.GetComponent<Collider>() : null;
            if (groundCol == null)
            {
                return;
            }

            string[] spawnNames =
            {
                "ChaserSpawn_0", "ChaserSpawn_1", "ChaserSpawn_2", "ChaserSpawn_3",
                "RunnerSpawn_0", "RunnerSpawn_1", "RunnerSpawn_2", "RunnerSpawn_3"
            };

            for (int i = 0; i < spawnNames.Length; i++)
            {
                GameObject sp = GameObject.Find(spawnNames[i]);
                if (sp == null)
                {
                    continue;
                }

                Vector3 pos = sp.transform.position;
                Ray ray = new Ray(new Vector3(pos.x, 10f, pos.z), Vector3.down);
                if (groundCol.Raycast(ray, out RaycastHit hit, 30f))
                {
                    sp.transform.position = new Vector3(pos.x, hit.point.y + 0.02f, pos.z);
                    EditorUtility.SetDirty(sp.transform);
                }
            }
        }

        private static NavMeshSurface ConfigureSurface(GameObject map)
        {
            NavMeshSurface surface = map.GetComponent<NavMeshSurface>();
            if (surface == null)
            {
                surface = map.AddComponent<NavMeshSurface>();
            }

            float centerX = (FenceMinX + FenceMaxX) * 0.5f;
            float centerZ = (FenceMinZ + FenceMaxZ) * 0.5f;
            float sizeX = FenceMaxX - FenceMinX;
            float sizeZ = FenceMaxZ - FenceMinZ;

            surface.collectObjects = CollectObjects.Volume;
            surface.center = new Vector3(centerX, 2f, centerZ);
            surface.size = new Vector3(sizeX + 0.2f, 10f, sizeZ + 0.2f);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.defaultArea = 0;

            return surface;
        }

        private static int BuildUnreachableInteriorBlockers(Transform mapTransform)
        {
            GameObject sp0 = GameObject.Find("ChaserSpawn_0");
            Vector3 referencePos = sp0 != null ? sp0.transform.position : Vector3.zero;
            if (!NavMesh.SamplePosition(referencePos, out NavMeshHit originHit, 3f, NavMesh.AllAreas))
            {
                return 0;
            }

            Vector3 origin = originHit.position;
            NavMeshTriangulation tri = NavMesh.CalculateTriangulation();
            Dictionary<(int, int), Bounds> unreachableCells = new Dictionary<(int, int), Bounds>();
            NavMeshPath path = new NavMeshPath();

            for (int i = 0; i < tri.indices.Length; i += 3)
            {
                Vector3 a = tri.vertices[tri.indices[i]];
                Vector3 b = tri.vertices[tri.indices[i + 1]];
                Vector3 c = tri.vertices[tri.indices[i + 2]];
                Vector3 centroid = (a + b + c) / 3f;

                bool reachable = NavMesh.CalculatePath(origin, centroid, NavMesh.AllAreas, path) &&
                                 path.status == NavMeshPathStatus.PathComplete;
                if (!reachable)
                {
                    var key = (Mathf.FloorToInt(centroid.x / 3f), Mathf.FloorToInt(centroid.z / 3f));
                    if (!unreachableCells.TryGetValue(key, out Bounds bounds))
                    {
                        bounds = new Bounds(a, Vector3.zero);
                    }

                    bounds.Encapsulate(a);
                    bounds.Encapsulate(b);
                    bounds.Encapsulate(c);
                    unreachableCells[key] = bounds;
                }
            }

            if (unreachableCells.Count == 0)
            {
                return 0;
            }

            GameObject blockerRoot = new GameObject(InteriorBlockerRootName);
            blockerRoot.transform.SetParent(mapTransform, false);

            int idx = 0;
            foreach (KeyValuePair<(int, int), Bounds> kv in unreachableCells)
            {
                Bounds b = kv.Value;
                GameObject vgo = new GameObject($"Blocker_{idx++}");
                vgo.transform.SetParent(blockerRoot.transform, false);
                vgo.transform.position = b.center;

                NavMeshModifierVolume vol = vgo.AddComponent<NavMeshModifierVolume>();
                vol.center = Vector3.zero;
                vol.size = new Vector3(b.size.x + 0.1f, 3.0f, b.size.z + 0.1f);
                vol.area = 1; // Not Walkable
            }

            return idx;
        }

        private static void PersistNavMeshAsset(NavMeshSurface surface)
        {
            if (surface == null || surface.navMeshData == null)
            {
                return;
            }

            NavMeshData existingAsset = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshAssetPath);
            if (existingAsset != null)
            {
                surface.RemoveData();
                EditorUtility.CopySerialized(surface.navMeshData, existingAsset);
                surface.navMeshData = existingAsset;
                surface.AddData();
                EditorUtility.SetDirty(existingAsset);
            }
            else
            {
                AssetDatabase.CreateAsset(surface.navMeshData, NavMeshAssetPath);
            }

            EditorUtility.SetDirty(surface);
        }
    }
}
#endif
