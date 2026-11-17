using UnityEditor;
using UnityEngine;
using ChaseGame.Characters;
using ChaseGame.Infrastructure;

namespace ChaseGame.EditorTools
{
    /// <summary>
    /// Adds the first-person rig (an EyeAnchor child plus a disabled FirstPersonView) to both
    /// playable character prefabs. SpawnManager enables the view on whichever character the human
    /// ends up driving; everyone else keeps third-person behaviour and its AI brain.
    ///
    /// Re-runnable: reuses an existing EyeAnchor and FirstPersonView instead of duplicating them.
    /// Batch: Unity -batchmode -quit -projectPath . -executeMethod ChaseGame.EditorTools.FirstPersonRigSetup.Run
    /// </summary>
    public static class FirstPersonRigSetup
    {
        private const string ChaserPrefabPath = "Assets/Prefabs/ChaserCharacter.prefab";
        private const string RunnerPrefabPath = "Assets/Prefabs/RunnerCharacter.prefab";

        private const string EyeAnchorName = "EyeAnchor";

        // Eye heights come from the models' own geometry, not from the CharacterController (both
        // prefabs use a 1m capsule regardless of how tall the model renders):
        //   chicken   mesh spans y 0.003 .. 0.694  -> eyes around 0.55
        //   human NPC mesh spans y 0.005 .. 1.961  -> eyes around 1.72
        // Both are serialized Transforms, so they can be nudged in the Inspector afterwards.
        private const float ChaserEyeHeight = 0.55f;
        private const float RunnerEyeHeight = 1.72f;

        [MenuItem("ChaseGame/Setup First Person Rigs")]
        public static void Run()
        {
            // The chaser keeps its own visual: its view model is wings and legs, built to be seen
            // from inside. The runner has only a full-body model, so it hides itself instead.
            SetupPrefab(ChaserPrefabPath, ChaserEyeHeight, hideOwnVisual: false);
            SetupPrefab(RunnerPrefabPath, RunnerEyeHeight, hideOwnVisual: true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[FirstPersonRigSetup] Done.");
        }

        private static void SetupPrefab(string prefabPath, float eyeHeight, bool hideOwnVisual)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                throw new System.InvalidOperationException("Missing prefab: " + prefabPath);
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                var anchor = FindOrCreateAnchor(root, eyeHeight);

                var view = root.GetComponent<FirstPersonView>();
                if (view == null)
                {
                    view = root.AddComponent<FirstPersonView>();
                }

                view.Configure(anchor, hideOwnVisual);

                // Off on the prefab: only the human-driven character gets it switched on, by
                // SpawnManager. Left on, every AI character would fight for the camera.
                view.enabled = false;

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[FirstPersonRigSetup] {prefabPath}: eye height {eyeHeight}, hideOwnVisual {hideOwnVisual}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform FindOrCreateAnchor(GameObject root, float eyeHeight)
        {
            Transform anchor = null;

            for (int i = 0; i < root.transform.childCount; i++)
            {
                var child = root.transform.GetChild(i);
                if (child.name == EyeAnchorName)
                {
                    anchor = child;
                    break;
                }
            }

            if (anchor == null)
            {
                var go = new GameObject(EyeAnchorName);
                anchor = go.transform;
                anchor.SetParent(root.transform, false);
            }

            anchor.localPosition = new Vector3(0f, eyeHeight, 0f);
            anchor.localRotation = Quaternion.identity;
            anchor.localScale = Vector3.one;
            return anchor;
        }
    }
}
