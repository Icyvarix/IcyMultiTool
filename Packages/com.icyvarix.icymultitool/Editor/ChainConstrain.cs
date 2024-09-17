using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using UnityEngine.Animations;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.Dynamics;
using static Icyvarix.Multitool.Common.TransformUtilities;
using static Icyvarix.Multitool.Common.Utility;
using static Icyvarix.Multitool.Common.GUIUtilities;

namespace Icyvarix.Multitool.Tools
{
    public class ChainConstrainTool : EditorWindow
    {
        // --------------------------------------------------------------
        // GUI Defines
        public string[] topologyEnforcementSettingStrings = new string[] { "Receiver is Subset", "Exact Match", "None" };
        public enum TopologyEnforcementSetting
        {
            ReceiverSubset,
            Exact,
            None
        }

        // --------------------------------------------------------------
        // User Input Variables
        private Transform receiverRoot;
        private Transform targetRoot;
        private DesiredConstraintType constraintType = 0;
        private DesiredMatchOption childMatchOption = 0;
        private ConstraintSpaceOption constraintSpaceOption = 0;
        private TopologyEnforcementSetting topologyEnforcementSetting = 0;
        private float targetSourceWeight = 1.0f;
        private float receiverSourceWeight = 0.0f;
        private List<Transform> ignoreTransforms = new List<Transform>();
        private ReorderableList reorderableIgnoreList;

        // --------------------------------------------------------------
        // GUI
        private bool showAdvancedSettings = false;
        private const float baseHeight = 420;
        private const float elementHeight = 23;

        private static string logoPath = "Packages/com.icyvarix.icymultitool/Resources/ChainConstrainLogo.png";
        private Texture noodleDragon;
        private static Texture2D windowIcon;
        // --------------------------------------------------------------

        [MenuItem("Tools/Icyvarix/Chain Constrain")]
        private static void Init()
        {
            var window = (ChainConstrainTool)EditorWindow.GetWindow(typeof(ChainConstrainTool));
            windowIcon = (Texture2D)AssetDatabase.LoadAssetAtPath(logoPath, typeof(Texture2D));
            window.titleContent = new GUIContent("Chain Constrain", windowIcon);
            window.Show();
        }

        void OnEnable()
        {
            noodleDragon = LoadNoodleTexture();
            reorderableIgnoreList = InitReorderableTransformList(ignoreTransforms, "Ignore Transforms", "Transforms to skip over when applying constraints.  Also skips their children.");
        }

        private void OnGUI()
        {
            this.minSize = new Vector2(340, baseHeight + (Mathf.Max((showAdvancedSettings ? 4 : 0) + ignoreTransforms.Count - 1, 0) * elementHeight));

            // Stick me in there first so all the stuff gets drawn over me.
            if (noodleDragon != null)
            {
                float imageWidth = position.width; // I deserve to be as wide as the window...but should probably actually just stick to the corner.  Maybe later.
                float imageHeight = imageWidth * 0.19f;

                Rect imageRect = new Rect(0, position.height - imageHeight, imageWidth, imageHeight);
                GUI.DrawTexture(imageRect, noodleDragon, ScaleMode.ScaleToFit);
            }

            GUILayout.Label("Applies the selected constraint to every bone in Receiver, constraining it to the corresponding bone in Target.", EditorStyles.wordWrappedLabel);

            GUILayout.Space(10);
            GUILayout.Label("Constraint Type", EditorStyles.boldLabel);
            constraintType = (DesiredConstraintType)GUILayout.Toolbar((int)constraintType, DesiredConstraintTypeStrings, GUILayout.ExpandWidth(true));

            GUILayout.Space(10);
            GUILayout.Label("Transform Targets", EditorStyles.boldLabel);
            receiverRoot = EditorGUILayout.ObjectField(new GUIContent("Receiver Root Bone", "The root bone of the bone chain to apply the constraints to."), receiverRoot, typeof(Transform), true) as Transform;
            targetRoot = EditorGUILayout.ObjectField(new GUIContent("Target Root Bone", "The root bone of the bone chain to constrain the receiver chain to."), targetRoot, typeof(Transform), true) as Transform;

            GUILayout.Space(10);
            GUILayout.Label("Options", EditorStyles.boldLabel);
            childMatchOption = (DesiredMatchOption)EditorGUILayout.Popup(new GUIContent("Child Matching", "Choose how to match cases of multiple children when applying constraints. 'By name' uses name similarity, 'By order' uses the order in the hierarchy."), (int)childMatchOption, DesiredMatchOptionStrings);
            constraintSpaceOption = (ConstraintSpaceOption)EditorGUILayout.Popup(new GUIContent("Implementation", "How the constraint is implemented.  Local space is relative to the parent, World is relative to the world origin."), (int)constraintSpaceOption, ConstraintSpaceOptionStrings);
            
            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Settings", true);
            if (showAdvancedSettings)
            {
                EditorGUI.indentLevel++;
                targetSourceWeight = EditorGUILayout.Slider(new GUIContent("Target Weight", "The weight of the target bone source in the constraint.  1 is full weight, 0 is no weight."), targetSourceWeight, 0, 1);
                receiverSourceWeight = EditorGUILayout.Slider(new GUIContent("Receiver Weight", "Creates if non-zero.  The weight of the receiver bone source in the constraint.  1 is full weight, 0 is no weight."), receiverSourceWeight, 0, 1);
                topologyEnforcementSetting = (TopologyEnforcementSetting)EditorGUILayout.Popup(new GUIContent("Topology Enforcement", "How to handle cases where the hierarchies don't match.  'Receiver Subset' means every bone in receiver is in target, 'Exact Match' requires every bone in both to have a match, 'None' will skip topology validation and just match the bones it can."), (int)topologyEnforcementSetting, topologyEnforcementSettingStrings);
                EditorGUI.indentLevel--;
            }

            GUILayout.Space(10);
            reorderableIgnoreList.DoLayoutList();

            GUILayout.Space(10);
            if (GUILayout.Button("Apply Constraints"))
            {
                if (receiverRoot == null || targetRoot == null)
                {
                    EditorUtility.DisplayDialog("Error", "Please assign both a receiver and a target bone.", "Fine!");
                    return;
                }

                if (!ValidateBoneHierarchy(receiverRoot, targetRoot))
                {
                    Debug.Log("Failed to validate bone hierarchy.  Constraints not applied.");
                }
                else
                {
                    Undo.SetCurrentGroupName("Chain Constrain");
                    int undoGroup = Undo.GetCurrentGroup();

                    ApplyConstraints(receiverRoot, targetRoot);

                    Undo.CollapseUndoOperations(undoGroup);
                    Debug.Log("Constraints applied successfully.");
                }
            }
        }

        // Special exception for errors hit during validation, called ValidateException
        public class ValidateException : System.Exception
        {
            public ValidateException(string message) : base(message) { }
        }

        public static void RaiseValidateError(string message)
        {
            // Present a popup to the user with the error and then throw an exception
            EditorUtility.DisplayDialog("Validation Error", message, "Unfortunate");

            throw new ValidateException(message);
        }

        // Recursive function to validate the bone hierarchy.  Throws an exception if the hierarchies don't match.
        private bool ValidateBoneHierarchy(Transform receiver, Transform target)
        {
            try
            {
                if (topologyEnforcementSetting == TopologyEnforcementSetting.Exact)
                {
                    ValidateExactBoneHierarchy(receiver, target);
                }
                else if (topologyEnforcementSetting == TopologyEnforcementSetting.ReceiverSubset)
                {
                    ValidateReceiverSubsetBoneHierarchy(receiver, target);
                }
                else
                {
                    // Skip validation.
                }

                return true;
            }
            catch ( ValidateException ) { return false; }
        }

        private void ValidateExactBoneHierarchy(Transform receiver, Transform target)
        {
            // Count how many receiver children were in the ignoreTransforms list
            int ignoredRecvChildren = 0;
            int ignoredTargetChildren = 0;

            // Check if any of the receiver children are in the ignore list
            for (int i = 0; i < receiver.childCount; i++)
            {
                Transform receiver_child = receiver.GetChild(i);

                if (ignoreTransforms.Contains(receiver_child))
                {
                    ignoredRecvChildren++;
                }
            }

            // Check if any of the target children are in the ignore list
            for (int i = 0; i < target.childCount; i++)
            {
                Transform target_child = target.GetChild(i);

                if (ignoreTransforms.Contains(target_child))
                {
                    ignoredTargetChildren++;
                }
            }

            if (receiver.childCount - ignoredRecvChildren != target.childCount - ignoredTargetChildren)
            {
                RaiseValidateError("Receiver and target child counts do not match for " + receiver.name + " and " + target.name);
            }

            // Create the dictionary mapping the receiver children to the target children
            Dictionary<Transform, Transform> childMap = MapTransformChildren(receiver, target, childMatchOption, ignoreTransforms);

            if (childMap.Count != receiver.childCount - ignoredRecvChildren)
            {
                RaiseValidateError("Failed to match all bones in " + receiver.name + " and " + target.name);
            }

            // Now we can iterate through the child map and recursively check the hierarchy
            foreach (var pair in childMap)
            {
                ValidateExactBoneHierarchy(pair.Key, pair.Value);
            }
        }

        private void ValidateReceiverSubsetBoneHierarchy(Transform receiver, Transform target)
        {
            // First create the dictionary mapping the receiver children to the target children
            Dictionary<Transform, Transform> childMap = MapTransformChildren(receiver, target, childMatchOption, ignoreTransforms);

            // Count how many receiver children were in the ignoreTransforms list
            int ignoredChildren = 0;

            // Check if any of the receiver children are in the ignore list
            for (int i = 0; i < receiver.childCount; i++)
            {
                Transform receiver_child = receiver.GetChild(i);

                if (ignoreTransforms.Contains(receiver_child))
                {
                    ignoredChildren++;
                }
            }

            int expectedMatchCount = receiver.childCount - ignoredChildren;

            if (childMap.Count != expectedMatchCount)
                RaiseValidateError("Failed to match all receiver bones in " + receiver.name + " and " + target.name + ".  Only matched " + childMap.Count + " out of " + expectedMatchCount + " bones.");

            // Now we can iterate through the child map and recursively check the hierarchy
            foreach (var pair in childMap)
            {
                ValidateReceiverSubsetBoneHierarchy(pair.Key, pair.Value);
            }
        }

        // Recursive function to apply constraints to the bone hierarchy.
        private void ApplyConstraints(Transform receiver, Transform target)
        {
            if ( ignoreTransforms.Contains(receiver) || ignoreTransforms.Contains(target) )
            {
                RaiseCritialError("[Logic Failure] Visited ignore transform!  This should never happen!  Validator is busted, tell Icy.");
            }

            // First create the dictionary mapping the receiver children to the target children
            Dictionary<Transform, Transform> childMap = MapTransformChildren(receiver, target, childMatchOption, ignoreTransforms);

            foreach (var pair in childMap)
            {
                ApplyConstraints(pair.Key, pair.Value);
            }

            // Record our state before modifying it.
            Undo.RecordObject(receiver, "Apply Individual Constraint");

            // Actually constrain the matching bones.
            if (constraintSpaceOption == ConstraintSpaceOption.UnityWorld)
            {
                ApplyUnityConstraint(constraintType, receiver, target);
            }
            else
            {
                ApplyVRCConstraint(constraintType, receiver, target, constraintSpaceOption == ConstraintSpaceOption.Local);
            }
        }

        // Applies the desired unity constraint type to the receiver bone, constraining it to the target bone.
        private void ApplyUnityConstraint(DesiredConstraintType type, Transform receiver, Transform target)
        {
            switch (type)
            {
                case DesiredConstraintType.Parent:
                    ParentConstraint parentConstraint = Undo.AddComponent<ParentConstraint>(receiver.gameObject);
                    parentConstraint.AddSource(new ConstraintSource { weight = 1, sourceTransform = target });
                    parentConstraint.constraintActive = true;
                    parentConstraint.locked = true;
                    break;
                case DesiredConstraintType.Rotation:
                    RotationConstraint rotationConstraint = Undo.AddComponent<RotationConstraint>(receiver.gameObject);
                    rotationConstraint.AddSource(new ConstraintSource { weight = 1, sourceTransform = target });
                    rotationConstraint.constraintActive = true;
                    rotationConstraint.locked = true;
                    break;
                case DesiredConstraintType.Position:
                    PositionConstraint positionConstraint = Undo.AddComponent<PositionConstraint>(receiver.gameObject);
                    positionConstraint.AddSource(new ConstraintSource { weight = 1, sourceTransform = target });
                    positionConstraint.constraintActive = true;
                    positionConstraint.locked = true;
                    break;
                case DesiredConstraintType.Scale:
                    ScaleConstraint scaleConstraint = Undo.AddComponent<ScaleConstraint>(receiver.gameObject);
                    scaleConstraint.AddSource(new ConstraintSource { weight = 1, sourceTransform = target });
                    scaleConstraint.constraintActive = true;
                    scaleConstraint.locked = true;
                    break;
            }
        }

        // Applies the desired VRChat constraint type to the receiver bone, constraining it to the target bone.
        private void ApplyVRCConstraint(DesiredConstraintType type, Transform receiver, Transform target, bool useLocalSpace)
        {
            VRCConstraintBase newConstraint; // Yay these actually have a common baseclass.

            switch (type)
            {
                case DesiredConstraintType.Parent:
                    newConstraint = Undo.AddComponent<VRCParentConstraint>(receiver.gameObject);
                    break;
                case DesiredConstraintType.Rotation:
                    newConstraint = Undo.AddComponent<VRCRotationConstraint>(receiver.gameObject);
                    break;
                case DesiredConstraintType.Position:
                    newConstraint = Undo.AddComponent<VRCPositionConstraint>(receiver.gameObject);
                    break;
                case DesiredConstraintType.Scale:
                    newConstraint = Undo.AddComponent<VRCScaleConstraint>(receiver.gameObject);
                    break;
                default:
                    RaiseCritialError("Invalid constraint type " + type);
                    newConstraint = Undo.AddComponent<VRCParentConstraint>(receiver.gameObject);
                    break;
            }

            newConstraint.Sources.SetLength(receiverSourceWeight > 0 ? 2 : 1);

            // Set target source
            VRCConstraintSource targetSource = newConstraint.Sources[0];
            targetSource.Weight = targetSourceWeight;
            targetSource.SourceTransform = target;
            newConstraint.Sources[0] = targetSource; // "Add" seems to trigger the "set defaults" behavior on deseralization so we have to use this strat instead.

            // Set receiver source
            if (receiverSourceWeight > 0)
            {
                VRCConstraintSource receiverSource = newConstraint.Sources[1];

                receiverSource.Weight = receiverSourceWeight;
                receiverSource.SourceTransform = receiver;

                newConstraint.Sources[1] = receiverSource;
            }

            newConstraint.IsActive = true;
            newConstraint.Locked = true;
            newConstraint.SolveInLocalSpace = useLocalSpace;
        }
    }
}