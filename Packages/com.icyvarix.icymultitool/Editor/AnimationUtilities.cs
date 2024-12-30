using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Icyvarix.Multitool.Common.Utility;

namespace Icyvarix.Multitool.Common
{   
    public class AnimationUtilities
    {
        public static string[] desiredAnimationEffectModeStrings = new string[] { "Copy", "Transfer", "Substitute" };
        public enum DesiredAnimationEffectMode 
        { 
            Copy, // Copy effects from source object to target objects, keeping source object's effects
            Transfer, // Transfer effects from source object to target objects, removing source object's effects
            Substitute // Create new set of animations where the effects on source object apply to target objects instead
        }

        public class AnimationRetargetException : System.Exception
        {
            public AnimationRetargetException(string message) : base(message) { }
        }

        public static void RaiseAnimationRetargetError(string message)
        {
            // Present a popup to the user with the error and then throw an exception
            EditorUtility.DisplayDialog("Retargeting Error", message, "Unfortunate");

            throw new AnimationRetargetException(message);
        }

        public static bool CheckAnimationMultipleRegexMatch(string regex, AnimationClip animation)
        {
            var bindings = AnimationUtility.GetCurveBindings(animation);

            // Get all bindings that affect the source object
            var sourceBindings = bindings.Where(b => System.Text.RegularExpressions.Regex.IsMatch(b.path, regex)).ToList();

            if (sourceBindings.Count == 0)
            {
                // No bindings means no conflicts
                return true;
            }

            // Ensure all sourceBindings have the same binding path, throw an error if not
            string sourcePath = sourceBindings[0].path;
            foreach (var binding in sourceBindings)
            {
                if (binding.path != sourcePath)
                {
                    Debug.LogWarning($"Source bindings have different paths for {animation.name}: {sourcePath} and {binding.path}");
                    return false;
                }
            }

            return true;
        }

        public static void RetargetAnimation(string sourceRegex, Transform rootObject, List<Transform> targetTransforms, AnimationClip animation, bool removeSourceBindings)
        {
            if (animation == null)
            {
                RaiseCritialError("No animation clip provided to animation retarget function.");
            }

            Debug.Log($"Processing animation: {animation.name}");
            var bindings = AnimationUtility.GetCurveBindings(animation);

            // Get all bindings that affect the source object
            var sourceBindings = bindings.Where(b => System.Text.RegularExpressions.Regex.IsMatch(b.path, sourceRegex)).ToList();

            if (sourceBindings.Count == 0)
            {
                Debug.LogWarning($"No bindings found for source object in animation: {animation.name}");
                return;
            }

            // Ensure all sourceBindings have the same binding path, throw an error if not
            string sourcePath = sourceBindings[0].path;
            foreach (var binding in sourceBindings)
            {
                if (binding.path != sourcePath)
                {
                    RaiseCritialError($"Source bindings have different paths for {animation.name}: {sourcePath} and {binding.path}");
                }
            }

            foreach (var binding in sourceBindings)
            {
                // Get the curve for the source object's binding
                var curve = AnimationUtility.GetEditorCurve(animation, binding);

                foreach (var targetTransform in targetTransforms)
                {
                    if (targetTransform == null)
                        continue;

                    // Get the transform path for the copy object
                    string copyPath = AnimationUtility.CalculateTransformPath(targetTransform, rootObject);

                    // Create a copy object's binding
                    var modifiedBinding = binding;
                    modifiedBinding.path = copyPath;

                    // Set the curve for the copy object's binding
                    AnimationUtility.SetEditorCurve(animation, modifiedBinding, curve);

                    Debug.Log($"Copied changes from {sourceRegex} to {targetTransform.gameObject.name} for curve: {binding.propertyName}");
                }
            }

            if (removeSourceBindings)
            {
                // Remove the source bindings from the animation
                foreach (var binding in sourceBindings)
                {
                    AnimationUtility.SetEditorCurve(animation, binding, null);
                }

                Debug.Log($"Removed source bindings from animation: {animation.name}");
            }

            // Mark the animation clip as dirty to ensure changes are saved
            EditorUtility.SetDirty(animation);
        }

        public static void RetargetAnimations(string sourceRegex, Transform rootObject, List<Transform> targetTransforms, List<AnimationClip> targetAnimations, DesiredAnimationEffectMode effectMode)
        {
            foreach (var animation in targetAnimations)
            {
                if (!CheckAnimationMultipleRegexMatch(sourceRegex, animation))
                {
                    RaiseAnimationRetargetError($"Multiple distinct path matches found for source object in animation: {animation.name}");
                }
            }

            foreach (var targetAnimation in targetAnimations)
            {
                if (effectMode == DesiredAnimationEffectMode.Copy)
                {
                    RetargetAnimation(sourceRegex, rootObject, targetTransforms, targetAnimation, false);
                }
                else if (effectMode == DesiredAnimationEffectMode.Transfer)
                {
                    RetargetAnimation(sourceRegex, rootObject, targetTransforms, targetAnimation, true);
                }
                else if (effectMode == DesiredAnimationEffectMode.Substitute)
                {
                    // Create a new animation
                    AnimationClip newAnimation = new AnimationClip();
                    string firstTargetTransformName = targetTransforms[0].name;
                    newAnimation.name = $"{targetAnimation.name}_{firstTargetTransformName}";

                    // Copy all curves from the source animation to the new animation
                    var bindings = AnimationUtility.GetCurveBindings(targetAnimation);
                    foreach (var binding in bindings)
                    {
                        var curve = AnimationUtility.GetEditorCurve(targetAnimation, binding);
                        AnimationUtility.SetEditorCurve(newAnimation, binding, curve);
                    }

                    // Apply the effects to the new animation
                    RetargetAnimation(sourceRegex, rootObject, targetTransforms, newAnimation, true);

                    // Save the new animation in folder of the original animation + /Retargeted
                    string assetPath = AssetDatabase.GetAssetPath(targetAnimation);
                    string assetFolder = System.IO.Path.GetDirectoryName(assetPath);

                    // Make Retargeted folder if it doesn't exist
                    if (!AssetDatabase.IsValidFolder($"{assetFolder}/Retargeted"))
                    {
                        AssetDatabase.CreateFolder(assetFolder, "Retargeted");
                    }

                    string newAssetPath = $"{assetFolder}/Retargeted/{newAnimation.name}.anim";
                    AssetDatabase.CreateAsset(newAnimation, newAssetPath);
                }
            }

            Debug.Log($"Retargeted {targetAnimations.Count} animations successfully.");

            // Save all modified assets
            AssetDatabase.SaveAssets();
        }
    }
}