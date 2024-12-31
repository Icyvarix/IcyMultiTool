using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using UnityEngine.Animations;
using static Icyvarix.Multitool.Common.TransformUtilities;
using static Icyvarix.Multitool.Common.Utility;
using static Icyvarix.Multitool.Common.GUIUtilities;
using static Icyvarix.Multitool.Common.AnimationUtilities;

namespace Icyvarix.Multitool.Tools
{
    public class AnimationEffectRetarget : EditorWindow
    {
        // --------------------------------------------------------------
        // User Input Variables
        private DesiredAnimationEffectMode effectMode = 0; // How to apply effects
        Transform sourceObject; // Object to copy effects from
        Transform rootObject; // Object all animations are relative to
        private List<Transform> targetTransforms = new List<Transform>(); // List of transforms to apply effects to
        private ReorderableList reorderableTargetList;

        private List<AnimationClip> targetAnimations = new List<AnimationClip>(); // List of animations to process
        private ReorderableList reorderableAnimationList;

        string targetPathRegex = ""; // Alternative way to match source object

        // --------------------------------------------------------------
        // GUI
        private Vector2 scrollPosition;
        private bool showAdvancedSettings = false;
        private const float baseHeight = 400;
        private const float elementHeight = 23;

        private static string logoPath = "Packages/com.icyvarix.icymultitool/Resources/AnimationTrianglesLogo.png";
        private Texture noodleDragon;
        private static Texture2D windowIcon;
        // --------------------------------------------------------------

        [MenuItem("Tools/Icyvarix/Retarget Animation Effects")]
        private static void Init()
        {
            var window = (AnimationEffectRetarget)EditorWindow.GetWindow(typeof(AnimationEffectRetarget));
            windowIcon = (Texture2D)AssetDatabase.LoadAssetAtPath(logoPath, typeof(Texture2D));
            window.titleContent = new GUIContent("Retarget Animation Effects", windowIcon);
            window.Show();
        }

        void OnEnable()
        {
            noodleDragon = LoadNoodleTexture();

            reorderableTargetList = InitReorderableGUIList<Transform>(targetTransforms, "Target Objects", "Objects to copy/transfer effects to.");
            reorderableAnimationList = InitReorderableGUIList<AnimationClip>(targetAnimations, "Target Animations", "List of animations to operate on.");
        }

        private void OnGUI()
        {
            float advancedSettingsHeight = 24;
            float animationHeight = Mathf.Min(80 + Mathf.Max(targetAnimations.Count - 1, 0) * elementHeight, 300);
            float minHeight = baseHeight + animationHeight + Mathf.Max(targetTransforms.Count - 1, 0) * elementHeight + (showAdvancedSettings ? advancedSettingsHeight : 0);

            this.minSize = new Vector2(340, minHeight);

            // Stick me in there first so all the stuff gets drawn over me.
            if (noodleDragon != null)
            {
                float imageWidth = position.width; // I deserve to be as wide as the window...but should probably actually just stick to the corner.  Maybe later.
                float imageHeight = imageWidth * 0.19f;

                Rect imageRect = new Rect(0, position.height - imageHeight, imageWidth, imageHeight);
                GUI.DrawTexture(imageRect, noodleDragon, ScaleMode.ScaleToFit);
            }

            GUILayout.Label("In all targeted animations, replicate effects on source object to all target objects.", EditorStyles.wordWrappedLabel);

            GUILayout.Space(10);

            GUILayout.Label(new GUIContent("Operation Type", "Copy = Copy effects from source object to target objects, keeping source object's effects\nTransfer = Same as copy but remove source object's effects\nSubstitute = Same as Transfer but create a new animation instead of modifying the original"), EditorStyles.boldLabel);
            effectMode = (DesiredAnimationEffectMode)GUILayout.Toolbar((int)effectMode, desiredAnimationEffectModeStrings, GUILayout.ExpandWidth(true));

            GUILayout.Space(10);

            rootObject = EditorGUILayout.ObjectField(new GUIContent("Root Object", "The object all animations are relative to."), rootObject, typeof(Transform), true) as Transform;
            sourceObject = EditorGUILayout.ObjectField(new GUIContent("Source Object", "The object to copy effects from."), sourceObject, typeof(Transform), true) as Transform;

            GUILayout.Space(10);

            reorderableTargetList.DoLayoutList();

            GUILayout.Space(10);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(animationHeight));
            reorderableAnimationList.DoLayoutList();
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Add All Animations in Folder"))
            {
                AddAllAnimationsInFolder(EditorUtility.OpenFolderPanel("Select Folder", "", ""), targetAnimations);
            }

            GUILayout.Space(10);

            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Settings", true);
            if (showAdvancedSettings)
            {
                EditorGUI.indentLevel++;
                targetPathRegex = EditorGUILayout.TextField(new GUIContent("Source Path Regex", "Regular Expression for matching paths for Source Objects.\nOnly has an effect if Source Object is unspecified\nWill throw an error if multiple matches are found in one animation."), targetPathRegex);
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(10);
            if (GUILayout.Button("Retarget Animations"))
            {
                List<AnimationClip> cleanedAnimationList = new List<AnimationClip>( targetAnimations );
                cleanedAnimationList.RemoveAll(t => t == null);

                if (cleanedAnimationList.Count == 0)
                {
                    EditorUtility.DisplayDialog("Error", "At least one animation is required.", "Fine");
                    return;
                }

                List<Transform> cleanedTargetTransforms = new List<Transform>( targetTransforms );
                cleanedTargetTransforms.RemoveAll(t => t == null);

                if (cleanedTargetTransforms.Count == 0)
                {
                    EditorUtility.DisplayDialog("Error", "At least one target object is required.", "Fine");
                    return;
                }

                if (sourceObject == null && string.IsNullOrEmpty(targetPathRegex))
                {
                    EditorUtility.DisplayDialog("Error", "Either a source object or a source path regex is required.", "Fine");
                    return;
                }

                string sourceRegex = targetPathRegex;

                if (sourceObject != null)
                {
                    // Get the transform path of the source object
                    string sourcePath = AnimationUtility.CalculateTransformPath(sourceObject.transform, rootObject ? rootObject.transform : null);

                    // Add start/end line characters to regex, since this is an exact path match.
                    sourceRegex = $"^{sourcePath}$";
                }

                // Print source path
                Debug.Log($"Source Object Search String: {sourceRegex}");

                RetargetAnimations(sourceRegex, rootObject, cleanedTargetTransforms, cleanedAnimationList, effectMode);
            }
        }
    }
}