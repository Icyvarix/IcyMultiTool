using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using UnityEngine.Animations;
using VRC.Dynamics;
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
        DesiredBoneMatchOption boneMatchOption = 0;
        PostRebindOperations postRebindOperations = 0;
        TransformRepositionOption repositionBoneOption = 0;
        string targetBonePrefix;
        string meshBonePrefix;
        private List<Transform> targetTransforms = new List<Transform>();
        private ReorderableList reorderableTargetList;
        private List<Transform> ignoreTransforms = new List<Transform>();
        private ReorderableList reorderableIgnoreList;

        // --------------------------------------------------------------
        // GUI
        private bool showAdvancedSettings = false;
        private const float baseHeight = 450;
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

            reorderableTargetList = InitReorderableTransformList(targetTransforms, "Target Transforms", "Transforms to search for valid bones to rebind the mesh to.  Will include all children.");
            reorderableIgnoreList = InitReorderableTransformList(ignoreTransforms, "Ignore Transforms", "Transforms to ignore for all operations (except cleanup).  Also ignores their children.");
        }

        private void OnGUI()
        {
            this.minSize = new Vector2(340, baseHeight + (((showAdvancedSettings ? 4 : 0) + Mathf.Max(ignoreTransforms.Count - 1, 0) + Mathf.Max(targetTransforms.Count - 1, 0)) * elementHeight));

            // Stick me in there first so all the stuff gets drawn over me.
            if (noodleDragon != null)
            {
                float imageWidth = position.width; // I deserve to be as wide as the window...but should probably actually just stick to the corner.  Maybe later.
                float imageHeight = imageWidth * 0.19f;

                Rect imageRect = new Rect(0, position.height - imageHeight, imageWidth, imageHeight);
                GUI.DrawTexture(imageRect, noodleDragon, ScaleMode.ScaleToFit);
            }

            GUILayout.Label("Rebinds skinned mesh renderer bones.\nThe skinned mesh renderer will follow the new target bones instead.", EditorStyles.wordWrappedLabel);

            GUILayout.Space(10);
            reorderableTargetList.DoLayoutList();

            GUILayout.Space(10);
            skinnedMeshRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(new GUIContent("Skinned Mesh Renderer", "The skinned mesh renderer to rebind."), skinnedMeshRenderer, typeof(SkinnedMeshRenderer), true);

            GUILayout.Space(10);
            GUILayout.Label("Options", EditorStyles.boldLabel);
            boneMatchOption = (DesiredBoneMatchOption)EditorGUILayout.Popup(new GUIContent("Bone Matching", "How to identify which bones should rebind to which."), (int)boneMatchOption, DesiredBoneMatchOptionStrings);
            postRebindOperations = (PostRebindOperations)EditorGUILayout.Popup(new GUIContent("Post Rebind Ops", "What to do after rebinding the mesh.  Cleanup deletes the old bones, and reparenting will reparent all children of rebound bones."), (int)postRebindOperations, PostRebindOperationsStrings);
            repositionBoneOption = (TransformRepositionOption)EditorGUILayout.Popup(new GUIContent("Reposition Bones", "How to reposition the bones before rebinding.  Mesh Bones will move the mesh bones to the target bones, Target Bones will move the target bones to the mesh bones, and None will leave them where they are."), (int)repositionBoneOption, TransformRepositionOptionStringsMeshRebinder);

            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Settings", true);
            if (showAdvancedSettings)
            {
                EditorGUI.indentLevel++;
                targetBonePrefix = EditorGUILayout.TextField(new GUIContent("Required Target Bone Prefix", "All target bones that do not start with this string will be silently ignored for rebinding.\nIt is also ignored for matching."), targetBonePrefix);
                meshBonePrefix = EditorGUILayout.TextField(new GUIContent("Required Mesh Bone Prefix", "All mesh bones that do not start with this string will be silently ignored for rebinding.\nIt is also ignored for matching."), meshBonePrefix);
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(10);
            reorderableIgnoreList.DoLayoutList();

            GUILayout.Space(10);
            if (GUILayout.Button("Rebind Mesh"))
            {
                if (skinnedMeshRenderer == null)
                {
                    EditorUtility.DisplayDialog("Error", "Skinned Mesh Renderer is required.", "Fine");
                    return;
                }

                if (targetTransforms.Count == 0)
                {
                    EditorUtility.DisplayDialog("Error", "At least one target transform is required.", "Okay good");
                    return;
                }

                Dictionary<Transform, Transform> rebindMap = GenerateAndValidateRebindMap(skinnedMeshRenderer, targetTransforms, boneMatchOption, postRebindOperations, ignoreTransforms, targetBonePrefix, meshBonePrefix);

                if (rebindMap != null)
                {
                    RebindAndAdjustBones(skinnedMeshRenderer, rebindMap, postRebindOperations, repositionBoneOption, ignoreTransforms);

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