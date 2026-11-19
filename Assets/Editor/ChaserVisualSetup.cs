using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ChaseGame.Characters;

namespace ChaseGame.EditorTools
{
    /// <summary>
    /// Builds the Chaser's animation assets from the clips already in the project.
    ///
    /// Both chicken rigs (Rig_Chick_Prop, the first-person view model with only wings and legs,
    /// and Main_Chic_Rig, the full-body world model) share an identical bone hierarchy, and all
    /// eight clips under Assets/Models/Player store their curves as string paths against that
    /// hierarchy - so one controller drives both models and no new animation is needed.
    ///
    /// Re-runnable: every step overwrites its own output.
    /// Batch: Unity -batchmode -quit -projectPath . -executeMethod ChaseGame.EditorTools.ChaserVisualSetup.Run
    /// </summary>
    public static class ChaserVisualSetup
    {
        private const string MainRigPath = "Assets/Models/Chicken/Main/Main_Chic_Rig.fbx";
        private const string FppRigPath = "Assets/Models/Player/Rig_Chick_Prop.fbx";

        private const string IdleClipPath = "Assets/Models/Player/Anim_FPP/Armature_Chic_FPP_Idle.anim";
        private const string WalkClipPath = "Assets/Models/Player/Anim_FPP/Armature_Chic_FPP_Walk.anim";
        private const string AttackClipPath = "Assets/Models/Player/Anim_FPP/Armature_Chic_FPP_Attack.anim";

        private const string ControllerPath = "Assets/Models/Chicken/Chic.controller";
        private const string FppVisualPrefabPath = "Assets/Prefabs/Visuals/ChaserVisual_FPP.prefab";
        private const string WorldVisualPrefabPath = "Assets/Prefabs/Visuals/ChaserVisual_World.prefab";
        private const string ChaserCharacterPath = "Assets/Prefabs/ChaserCharacter.prefab";

        // Speed is normalized 0..1 by CharacterAnimationController. Chaser base speed is 5 m/s
        // (ChaserStats) and top speed 8 m/s (SpeedBoost multiplier 1.6), so plain walking sits at
        // 5/8 = 0.625 and boosted running at 1.0.
        private const float WalkThreshold = 0.625f;
        private const float RunTimeScale = 1.6f;

        private const string SpeedParam = "Speed";
        private const string OnAbilityParam = "OnAbility";
        private const string AbilityIdParam = "AbilityID";

        // Gun is component 0 in GetComponents<IAbility>() order on ChaserCharacter.
        private const int GunAbilityId = 0;

        // First-person view model shoulder overrides (see 2026-11-18-chaser-wing-attachment-design.md):
        // the shared clips omit shoulder.L/R localPosition and localRotation curves so the world
        // model keeps the FBX bind pose (wings attached to mid-torso), while the FPP prefab holds
        // the viewmodel offset and upward rotation that frame the wings inside the FPP camera.
        private const string ShoulderLeftPath = "Armature/Bone/spine/spine.001/spine.002/shoulder.L";
        private const string ShoulderRightPath = "Armature/Bone/spine/spine.001/spine.002/shoulder.R";
        private static readonly Vector3 FppShoulderLeftPos = new Vector3(-0.1025f, 0.1457f, 0.2785f);
        private static readonly Vector3 FppShoulderRightPos = new Vector3(0.1025f, 0.1457f, 0.2785f);
        private static readonly Quaternion FppShoulderLeftRot = new Quaternion(-0.2167198f, 0.9318815f, 0.05396045f, -0.28588f);
        private static readonly Quaternion FppShoulderRightRot = new Quaternion(0.2167198f, 0.9318815f, 0.05396045f, 0.28588f);

        [MenuItem("ChaseGame/Rebuild Chaser Visuals")]
        public static void Run()
        {
            EnsureMainRigAvatar();
            var controller = BuildController();
            var fpp = BuildVisualPrefab(FppRigPath, FppVisualPrefabPath, controller, applyFppShoulderOffset: true);
            var world = BuildVisualPrefab(MainRigPath, WorldVisualPrefabPath, controller, applyFppShoulderOffset: false);
            RewireChaserCharacter(fpp, world);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ChaserVisualSetup] Done.");
        }

        // Main_Chic_Rig ships with avatarSetup = NoAvatar. The clips bind by path either way
        // because the hierarchy matches, but the Animator needs an Avatar to behave as a proper
        // Generic rig instead of relying on implicit fallback behaviour.
        private static void EnsureMainRigAvatar()
        {
            var importer = AssetImporter.GetAtPath(MainRigPath) as ModelImporter;
            if (importer == null)
            {
                throw new System.InvalidOperationException("Not a model importer: " + MainRigPath);
            }

            bool dirty = false;

            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                dirty = true;
            }

            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                dirty = true;
            }

            if (dirty)
            {
                importer.SaveAndReimport();
                Debug.Log("[ChaserVisualSetup] Created Avatar for " + MainRigPath);
            }
        }

        private static AnimatorController BuildController()
        {
            var idle = Load<AnimationClip>(IdleClipPath);
            var walk = Load<AnimationClip>(WalkClipPath);
            var attack = Load<AnimationClip>(AttackClipPath);

            EnsureAssetFolder(ControllerPath);
            AssetDatabase.DeleteAsset(ControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            // Same parameter set the Runner's NPC.controller uses, so one shared
            // CharacterAnimationController drives both roles. IsJailed is deliberately absent:
            // JailService only ever jails Runners, and CharacterAnimationController guards the
            // parameter with HasBoolParameter.
            controller.AddParameter(SpeedParam, AnimatorControllerParameterType.Float);
            controller.AddParameter(OnAbilityParam, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(AbilityIdParam, AnimatorControllerParameterType.Int);

            var locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendParameter = SpeedParam;
            tree.blendType = BlendTreeType.Simple1D;
            tree.useAutomaticThresholds = false;

            // No real Run clip exists yet; the run slot reuses Walk at 1.6x playback. Swapping in a
            // proper Run clip later is a one-line change to this child.
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, WalkThreshold);
            tree.AddChild(walk, 1f);

            var children = tree.children;
            children[0].timeScale = 1f;
            children[1].timeScale = 1f;
            children[2].timeScale = RunTimeScale;
            tree.children = children;

            // The ability pose lives on the BASE layer behind an Any State transition rather than
            // on its own override layer: an override layer at weight 1 whose default state has no
            // motion writes defaults over the whole body and freezes the chicken in its rest pose.
            var machine = controller.layers[0].stateMachine;
            machine.defaultState = locomotion;

            var attackState = machine.AddState("Attack");
            attackState.motion = attack;

            var toAttack = machine.AddAnyStateTransition(attackState);
            toAttack.hasExitTime = false;
            toAttack.duration = 0.05f;
            toAttack.canTransitionToSelf = false;
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, OnAbilityParam);
            toAttack.AddCondition(AnimatorConditionMode.Equals, GunAbilityId, AbilityIdParam);

            var backToLocomotion = attackState.AddTransition(locomotion);
            backToLocomotion.hasExitTime = true;
            backToLocomotion.exitTime = 0.9f;
            backToLocomotion.duration = 0.1f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("[ChaserVisualSetup] Built " + ControllerPath);
            return controller;
        }

        private static GameObject BuildVisualPrefab(
            string rigPath,
            string outPath,
            AnimatorController controller,
            bool applyFppShoulderOffset)
        {
            var rig = Load<GameObject>(rigPath);
            EnsureAssetFolder(outPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(rig);

            try
            {
                instance.name = System.IO.Path.GetFileNameWithoutExtension(outPath);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;

                if (applyFppShoulderOffset)
                {
                    var shoulderL = instance.transform.Find(ShoulderLeftPath);
                    var shoulderR = instance.transform.Find(ShoulderRightPath);
                    if (shoulderL != null)
                    {
                        shoulderL.localPosition = FppShoulderLeftPos;
                        shoulderL.localRotation = Quaternion.Normalize(FppShoulderLeftRot);
                    }
                    if (shoulderR != null)
                    {
                        shoulderR.localPosition = FppShoulderRightPos;
                        shoulderR.localRotation = Quaternion.Normalize(FppShoulderRightRot);
                    }
                }

                var animator = instance.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = instance.AddComponent<Animator>();
                }

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                // The clips carry no root motion (the Armature position curves are constant), and
                // the body is driven by CharacterController, so culling transforms is safe.
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                if (instance.GetComponent<CharacterAnimationController>() == null)
                {
                    instance.AddComponent<CharacterAnimationController>();
                }

                var saved = PrefabUtility.SaveAsPrefabAsset(instance, outPath);
                Debug.Log("[ChaserVisualSetup] Built " + outPath);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static void RewireChaserCharacter(GameObject fppVisual, GameObject worldVisual)
        {
            var root = PrefabUtility.LoadPrefabContents(ChaserCharacterPath);

            try
            {
                // Idempotent: drop the single legacy "Visual" child and anything a previous run of
                // this script left behind, but leave any other child alone.
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    var child = root.transform.GetChild(i).gameObject;
                    if (child.name == "Visual" ||
                        child.name == "Visual_FirstPerson" ||
                        child.name == "Visual_World")
                    {
                        Object.DestroyImmediate(child);
                    }
                }

                var fpp = AddVisualChild(root, fppVisual, "Visual_FirstPerson");
                var world = AddVisualChild(root, worldVisual, "Visual_World");

                var selector = root.GetComponent<CharacterVisualSelector>();
                if (selector == null)
                {
                    selector = root.AddComponent<CharacterVisualSelector>();
                }

                selector.Configure(fpp, world);

                PrefabUtility.SaveAsPrefabAsset(root, ChaserCharacterPath);
                Debug.Log("[ChaserVisualSetup] Rewired " + ChaserCharacterPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static GameObject AddVisualChild(GameObject root, GameObject visualPrefab, string childName)
        {
            var child = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, root.transform);
            child.name = childName;
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child;
        }

        // A folder created outside Unity (mkdir) has no .meta and gets pruned as an empty folder,
        // so SaveAsPrefabAsset would fail with "Given path does not exist". Create the folders
        // through the AssetDatabase instead, one segment at a time, so the script needs nothing
        // prepared for it.
        private static void EnsureAssetFolder(string assetPath)
        {
            string folder = System.IO.Path.GetDirectoryName(assetPath);
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] parts = folder.Split('/');
            string current = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(current, parts[i]);
                    if (string.IsNullOrEmpty(guid))
                    {
                        throw new System.InvalidOperationException("Could not create folder: " + next);
                    }

                    Debug.Log("[ChaserVisualSetup] Created folder " + next);
                }

                current = next;
            }
        }

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new System.InvalidOperationException("Missing asset: " + path);
            }

            return asset;
        }
    }
}
