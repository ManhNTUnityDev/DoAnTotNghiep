using UnityEngine;
using UnityEngine.AI;
using ChaseGame.Characters;

namespace ChaseGame.Match
{
    // A jail cage dropped where a runner was caught. Rescue was dropped from the design,
    // so the cage only has to hold its occupant for the rest of the match, keep other
    // characters from walking through it, and mark the spot on the map.
    //
    // There is no cage model in the project yet, so BuildPrimitive() assembles one out of
    // cubes — the same placeholder idea Gun uses for the egg. Assign a real prefab on
    // SpawnSettings.cagePrefab later and nothing else has to change.
    public sealed class Cage : MonoBehaviour
    {
        public const float Width = 1.6f;
        public const float Height = 2f;
        private const float BarThickness = 0.08f;
        private const float RailThickness = 0.06f;

        public Character Occupant { get; private set; }
        public Vector3 Position => transform.position;

        public static Cage Spawn(Cage prefab, Vector3 position, Quaternion rotation)
            => prefab != null
                ? Instantiate(prefab, position, rotation)
                : BuildPrimitive(position, rotation);

        public static Cage BuildPrimitive(Vector3 position, Quaternion rotation)
        {
            var root = new GameObject("Cage");
            root.transform.SetPositionAndRotation(position, rotation);
            var cage = root.AddComponent<Cage>();

            Vector3 center = new Vector3(0f, Height * 0.5f, 0f);
            Vector3 size = new Vector3(Width, Height, Width);

            var box = root.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;

            // Avoidance only: NPC agents steer around the cage without re-baking the
            // NavMesh at runtime (carving would invalidate the baked data mid-match).
            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = center;
            obstacle.size = size;
            obstacle.carving = false;

            Material bars = CreateBarMaterial();
            float half = Width * 0.5f - BarThickness * 0.5f;

            // Four corner posts.
            AddBar(root.transform, bars, new Vector3(half, Height * 0.5f, half), new Vector3(BarThickness, Height, BarThickness));
            AddBar(root.transform, bars, new Vector3(half, Height * 0.5f, -half), new Vector3(BarThickness, Height, BarThickness));
            AddBar(root.transform, bars, new Vector3(-half, Height * 0.5f, half), new Vector3(BarThickness, Height, BarThickness));
            AddBar(root.transform, bars, new Vector3(-half, Height * 0.5f, -half), new Vector3(BarThickness, Height, BarThickness));

            // Two rails per side, low enough to read as a cage but open enough to see through.
            foreach (float railHeight in new[] { Height * 0.32f, Height * 0.74f })
            {
                AddBar(root.transform, bars, new Vector3(0f, railHeight, half), new Vector3(Width, RailThickness, RailThickness));
                AddBar(root.transform, bars, new Vector3(0f, railHeight, -half), new Vector3(Width, RailThickness, RailThickness));
                AddBar(root.transform, bars, new Vector3(half, railHeight, 0f), new Vector3(RailThickness, RailThickness, Width));
                AddBar(root.transform, bars, new Vector3(-half, railHeight, 0f), new Vector3(RailThickness, RailThickness, Width));
            }

            // Roof, so the occupant reads as locked in rather than standing in a fence.
            AddBar(root.transform, bars, new Vector3(0f, Height, 0f), new Vector3(Width, RailThickness, Width));

            return cage;
        }

        // Records who is locked inside and stops the cage from shoving its own occupant.
        public void Bind(Character occupant)
        {
            Occupant = occupant;
            if (occupant == null)
            {
                return;
            }

            var cageCollider = GetComponent<Collider>();
            if (cageCollider == null)
            {
                return;
            }

            foreach (var occupantCollider in occupant.GetComponentsInChildren<Collider>())
            {
                if (occupantCollider != null && occupantCollider.enabled)
                {
                    Physics.IgnoreCollision(cageCollider, occupantCollider, true);
                }
            }
        }

        private static void AddBar(Transform parent, Material material, Vector3 localPosition, Vector3 localScale)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "Bar";

            // The root BoxCollider does the blocking; per-bar colliders would only add cost.
            var barCollider = bar.GetComponent<Collider>();
            if (barCollider != null)
            {
                Destroy(barCollider);
            }

            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = localPosition;
            bar.transform.localScale = localScale;

            if (material != null)
            {
                var renderer = bar.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = material;
                }
            }
        }

        // Dark metal bars. Returns null in a test environment with no usable shader,
        // in which case the bars keep the primitive's default material.
        private static Material CreateBarMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader) { name = "CageBars" };
            material.color = new Color(0.18f, 0.18f, 0.2f);
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.8f);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.45f);
            }

            return material;
        }
    }
}
