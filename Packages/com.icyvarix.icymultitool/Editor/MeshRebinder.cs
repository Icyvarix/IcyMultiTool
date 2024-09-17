using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using UnityEngine.Animations;
using VRC.Dynamics;
using System.Linq;
using static Icyvarix.Multitool.Common.TransformUtilities;
using static Icyvarix.Multitool.Common.MeshUtilities;
using static Icyvarix.Multitool.Common.Utility;
using static Icyvarix.Multitool.Common.GUIUtilities;

namespace Icyvarix.Multitool.Tools
{
    public class MeshRebindTool : EditorWindow
    {
        // --------------------------------------------------------------
        // GUI Defines
        public static string[] TransformRepositionOptionStringsMeshRebinder = new string[] { "Mesh Bones", "Target Bones", "None" };

        // --------------------------------------------------------------
        // User Input Variables
        SkinnedMeshRenderer skinnedMeshRenderer;
        Transform targetTransform;
        DesiredBoneMatchOption boneMatchOption = 0;
        PostRebindOperations postRebindOperations = 0;
        TransformRepositionOption repositionBoneOption = 0;
        string targetBonePrefix;
        string meshBonePrefix;
        private List<Transform> additionalTargetTransforms = new List<Transform>();
        private ReorderableList reorderableTargetList;
        private List<Transform> ignoreTransforms = new List<Transform>();
        private ReorderableList reorderableIgnoreList;
        private List<SkinnedMeshRenderer> additionalSkinnedMeshRenderers = new List<SkinnedMeshRenderer>();
        private ReorderableList reorderableAdditionalMeshes;

        // --------------------------------------------------------------
        // GUI
        private bool showAdvancedSettings = false;
        private const float baseHeight = 380;
        private const float elementHeight = 23;

        private static string logoPath = "Packages/com.icyvarix.icymultitool/Resources/MeshRebindLogo.png";
        private Texture noodleDragon;
        private static Texture2D windowIcon;
        // --------------------------------------------------------------

        [MenuItem("Tools/Icyvarix/Mesh Rebind")]
        private static void Init()
        {
            var window = (MeshRebindTool)EditorWindow.GetWindow(typeof(MeshRebindTool));
            windowIcon = (Texture2D)AssetDatabase.LoadAssetAtPath(logoPath, typeof(Texture2D));
            window.titleContent = new GUIContent("Mesh Rebinder", windowIcon);
            window.Show();
        }

        void OnEnable()
        {
            noodleDragon = LoadNoodleTexture();

            reorderableTargetList = InitReorderableGUIList<Transform>(additionalTargetTransforms, "Additional Bind Targets", "Additional transforms to search for valid bones to rebind the mesh to.  Will include all children.");
            reorderableIgnoreList = InitReorderableGUIList<Transform>(ignoreTransforms, "Ignore Transforms", "Transforms to ignore for all operations (except cleanup).  Also ignores their children.");
            reorderableAdditionalMeshes = InitReorderableGUIList<SkinnedMeshRenderer>(additionalSkinnedMeshRenderers, "Additional Skinned Mesh Renderers", "Additional skinned mesh renderers to rebind.  Will treat all bones used by at least one mesh as one group.");
        }

        private void OnGUI()
        {
            float advancedSettingsHeight = (9 + Mathf.Max(additionalTargetTransforms.Count - 1, 0) + Mathf.Max(additionalSkinnedMeshRenderers.Count - 1, 0)) * elementHeight;

            this.minSize = new Vector2(340, baseHeight + Mathf.Max(ignoreTransforms.Count - 1, 0) * elementHeight + (showAdvancedSettings ? advancedSettingsHeight : 0));

            // Stick me in there first so all the stuff gets drawn over me.
            if (noodleDragon != null)
            {
                float imageWidth = position.width; // I deserve to be as wide as the window...but should probably actually just stick to the corner.  Maybe later.
                float imageHeight = imageWidth * 0.19f;

                Rect imageRect = new Rect(0, position.height - imageHeight, imageWidth, imageHeight);
                GUI.DrawTexture(imageRect, noodleDragon, ScaleMode.ScaleToFit);
            }

            GUILayout.Label("Changes the bones the skinned mesh renderer follows.", EditorStyles.wordWrappedLabel);

            GUILayout.Space(10);
            GUILayout.Label("Targets", EditorStyles.boldLabel);
            skinnedMeshRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(new GUIContent("Skinned Mesh Renderer", "The skinned mesh renderer to rebind.  Current skinned mesh renderer bones are determined automatically and do not need to be specified."), skinnedMeshRenderer, typeof(SkinnedMeshRenderer), true);
            targetTransform = (Transform)EditorGUILayout.ObjectField(new GUIContent("Bind Target", "Transform to search for valid bones to rebind the mesh to.  Will include all children."), targetTransform, typeof(Transform), true);

            GUILayout.Space(10);
            GUILayout.Label("Options", EditorStyles.boldLabel);
            boneMatchOption = (DesiredBoneMatchOption)EditorGUILayout.Popup(new GUIContent("Bone Matching", "How to identify which bones should rebind to which."), (int)boneMatchOption, DesiredBoneMatchOptionStrings);
            postRebindOperations = (PostRebindOperations)EditorGUILayout.Popup(new GUIContent("Post Rebind Ops", "What to do after rebinding the mesh.  Cleanup deletes the old bones, and reparenting will reparent all children of rebound bones."), (int)postRebindOperations, PostRebindOperationsStrings);
            repositionBoneOption = (TransformRepositionOption)EditorGUILayout.Popup(new GUIContent("Reposition Bones", "How to reposition the bones before rebinding.  Mesh Bones will move the mesh bones to the target bones, Target Bones will move the target bones to the mesh bones, and None will leave them where they are."), (int)repositionBoneOption, TransformRepositionOptionStringsMeshRebinder);

            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Settings", true);
            if (showAdvancedSettings)
            {
                EditorGUI.indentLevel++;
                targetBonePrefix = EditorGUILayout.TextField(new GUIContent("Target Bone Prefix", "All target bones that do not start with this string will be silently ignored for rebinding.\nIt is also ignored for matching."), targetBonePrefix);
                meshBonePrefix = EditorGUILayout.TextField(new GUIContent("Mesh Bone Prefix", "All mesh bones that do not start with this string will be silently ignored for rebinding.\nIt is also ignored for matching."), meshBonePrefix);
                EditorGUI.indentLevel--;

                // Indent doesn't seem to affect the reorderable lists.
                GUILayout.Space(10);
                GUILayout.BeginHorizontal();
                GUILayout.Space(20);
                GUILayout.BeginVertical();
                reorderableAdditionalMeshes.DoLayoutList();

                GUILayout.Space(10);
                reorderableTargetList.DoLayoutList();
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);
            reorderableIgnoreList.DoLayoutList();

            GUILayout.Space(10);
            if (GUILayout.Button("Rebind Mesh"))
            {
                // Consolidate the additional lists, they're just a user interface abstraction to make usage more intuitive.
                List<Transform> targetTransforms = new List<Transform> { targetTransform };
                targetTransforms.AddRange(additionalTargetTransforms);

                List<SkinnedMeshRenderer> skinnedMeshRenderers = new List<SkinnedMeshRenderer> { skinnedMeshRenderer };
                skinnedMeshRenderers.AddRange(additionalSkinnedMeshRenderers);

                // Remove null entries from all lists
                targetTransforms.RemoveAll(t => t == null);
                skinnedMeshRenderers.RemoveAll(s => s == null);

                List<Transform> cleanedIgnoreTransforms = new List<Transform>(ignoreTransforms);
                cleanedIgnoreTransforms.RemoveAll(t => t == null);

                if (skinnedMeshRenderers.Count == 0)
                {
                    EditorUtility.DisplayDialog("Error", "At least one skinned Mesh Renderer is required.", "Fine");
                    return;
                }

                if (targetTransforms.Count == 0)
                {
                    EditorUtility.DisplayDialog("Error", "At least one target transform is required.", "Okay good");
                    return;
                }

                // Yell at the user if the same mesh renderer appears twice
                if (skinnedMeshRenderers.Count != skinnedMeshRenderers.Distinct().Count())
                {
                    EditorUtility.DisplayDialog("Error", "The same skinned mesh renderer appears multiple times! This is not required nor desired.", "I'm sorry");
                    return;
                }

                Dictionary<Transform, Transform> rebindMap = GenerateAndValidateRebindMap(skinnedMeshRenderers, targetTransforms, boneMatchOption, postRebindOperations, cleanedIgnoreTransforms, targetBonePrefix, meshBonePrefix);

                if (rebindMap != null)
                {
                    RebindAndAdjustBones(skinnedMeshRenderers, rebindMap, postRebindOperations, repositionBoneOption, cleanedIgnoreTransforms);

                    // Print a message saying how many bones we just rebound.
                    Debug.Log($"Rebound {rebindMap.Count} mesh bones successfully.");
                }
                else
                {
                    Debug.Log("Failed to map transform lists.  Mesh not rebound.");
                }
            }
        }
    }
}