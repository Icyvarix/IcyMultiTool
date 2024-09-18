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
using static Icyvarix.Multitool.Common.StringUtilities;

namespace Icyvarix.Multitool.Tools
{
    public class EncapsulateSelectedTool : EditorWindow
    {
        // --------------------------------------------------------------
        // GUI Defines
        public static string[] ReferenceRetargetOptionStrings = new string[] { "Retarget Meshes", "Retarget All" };
        public enum ReferenceRetargetOption
        {
            RetargetMeshes,
            RetargetAll
        }

        public static string[] ParentOrChildOptionStrings = new string[] { "Parent", "Child" };
        public enum ParentOrChildOption
        {
            Parent,
            Child
        }

        // --------------------------------------------------------------
        // User Input Variables
        private ReferenceRetargetOption referenceRetargetOption = 0;
        private ParentOrChildOption componentHolder = 0;
        private string newChildName = "$oldname";
        private string newParentName = "$oldname (E)";

        // --------------------------------------------------------------
        // GUI
        private const float baseHeight = 280;
        private const float elementHeight = 23;

        private static string logoPath = "Packages/com.icyvarix.icymultitool/Resources/EncapsulateLogo.png";
        private Texture noodleDragon;
        private static Texture2D windowIcon;
        // --------------------------------------------------------------

        [MenuItem("Tools/Icyvarix/Encapsulate Selected")]
        private static void Init()
        {
            var window = (EncapsulateSelectedTool)EditorWindow.GetWindow(typeof(EncapsulateSelectedTool));
            windowIcon = (Texture2D)AssetDatabase.LoadAssetAtPath(logoPath, typeof(Texture2D));
            window.titleContent = new GUIContent("Encapsulate Selected", windowIcon);
            window.Show();
        }

        void OnEnable()
        {
            noodleDragon = LoadNoodleTexture();
        }

        private void OnGUI()
        {
            this.minSize = new Vector2(340, baseHeight);

            // Stick me in there first so all the stuff gets drawn over me.
            if (noodleDragon != null)
            {
                float imageWidth = position.width; // I deserve to be as wide as the window...but should probably actually just stick to the corner.  Maybe later.
                float imageHeight = imageWidth * 0.19f;

                Rect imageRect = new Rect(0, position.height - imageHeight, imageWidth, imageHeight);
                GUI.DrawTexture(imageRect, noodleDragon, ScaleMode.ScaleToFit);
            }

            GUILayout.Label("Splits all selected objects into a parent/child pair.", EditorStyles.wordWrappedLabel);

            GUILayout.Space(10);
            GUILayout.Label(new GUIContent("Reference Target", "What references should target the child."), EditorStyles.boldLabel);
            referenceRetargetOption = (ReferenceRetargetOption)GUILayout.Toolbar((int)referenceRetargetOption, ReferenceRetargetOptionStrings, GUILayout.ExpandWidth(true));

            GUILayout.Space(10);
            GUILayout.Label(new GUIContent("Component Holder", "Who keeps the components."), EditorStyles.boldLabel);
            componentHolder = (ParentOrChildOption)GUILayout.Toolbar((int)componentHolder, ParentOrChildOptionStrings, GUILayout.ExpandWidth(true));

            GUILayout.Space(10);
            GUILayout.Label(new GUIContent("Object Names", "What to name the objects after the operation is finished. $oldname will be replaced with the old name.  $oldname[x:y] will be replaced with a substring of the old name."), EditorStyles.boldLabel);
            newParentName = EditorGUILayout.TextField(new GUIContent("Parent Name", "What to name the parent. $oldname will be replaced with the old name.  $oldname[x:y] will be replaced with a substring of the old name."), newParentName);
            newChildName = EditorGUILayout.TextField(new GUIContent("Child Name", "What to name the child.  $oldname will be replaced with the old name.  $oldname[x:y] will be replaced with a substring of the old name."), newChildName);

            GUILayout.Space(10);
            if (GUILayout.Button("Encapsulate Selected"))
            {
                var selected = Selection.gameObjects;
                if (selected.Length == 0)
                {
                    EditorUtility.DisplayDialog("No Selection", "Please select at least one object to encapsulate.", "Very well.");
                    return;
                }

                if (referenceRetargetOption == ReferenceRetargetOption.RetargetAll)
                {
                    for (int i = 0; i < selected.Length; i++)
                    {
                        if (PrefabUtility.IsPartOfPrefabInstance(selected[i]))
                        {
                            EditorUtility.DisplayDialog("Invalid Selection", "Cannot use Retarget All on objects that are part of a prefab.", "Okay good.");
                            return;
                        }
                    }
                }

                Undo.SetCurrentGroupName("Encapsulate Selected");
                int undoGroup = Undo.GetCurrentGroup();

                for (int i = 0; i < selected.Length; i++)
                {
                    EncapsulateObject(selected[i], referenceRetargetOption, componentHolder);
                }

                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log($"Encapsulated {selected.Length} objects.");
            }
        }

        // Splits the object into two, with the child being the encapsulated object.
        // Our end-goal is a hierarchy that is almost identical to the original, but with the child object being the encapsulated object.
        // Sometimes this will require moving the original object and making the new object the parent, sometimes not.
        private void EncapsulateObject(GameObject target, ReferenceRetargetOption referenceRetarget, ParentOrChildOption componentTarget)
        {
            if (target == null)
            {
                return;
            }
            // Determine parent/child names.
            string childName = CalculateNewNameFromFormatString(newChildName, target.name);
            string parentName = CalculateNewNameFromFormatString(newParentName, target.name);

            // For readability and maintainability, we'll keep track of whether the child is new or not.
            // It could be inferred from the referenceRetarget option, but it's easier to read this way.
            bool childIsNew = true;
            GameObject newObject = null;

            // If referenceRetarget is set to RetargetMeshes, all mesh references will be retargeted to the child, and nothing else.
            // We do this by simply finding all skinnedmeshrenderers that have target as one of their bones, and rebinding that bone to the child.
            if (referenceRetarget == ReferenceRetargetOption.RetargetMeshes)
            {
                // First we need to create our new object we're splitting into
                GameObject child = new GameObject(childName);
                Undo.RegisterCreatedObjectUndo(child, "Encapsulate Selected");
                childIsNew = true;
                newObject = child;

                // Set the child parent to the target through the undo system
                Undo.SetTransformParent(child.transform, target.transform, "Encapsulate Selected");

                // Zero out all the child transforms
                child.transform.localPosition = Vector3.zero;
                child.transform.localRotation = Quaternion.identity;
                child.transform.localScale = Vector3.one;

                // Finds all SkinnedMeshRenderers in the current scene, including inactive ones.
                SkinnedMeshRenderer[] skinnedMeshRenderers = FindObjectsOfType<SkinnedMeshRenderer>(true);

                Debug.Log($"Found {skinnedMeshRenderers.Length} SkinnedMeshRenderers in the scene.");
                foreach (var renderer in skinnedMeshRenderers)
                {
                    // If the renderer has the target as a bone, we need to rebind it to the child.
                    if (renderer.bones.Contains(target.transform))
                    {
                        Debug.Log($"Rebinding: {renderer.gameObject.name}");
                        // Record for undo system
                        Undo.RecordObject(renderer, "Encapsulate Selected");

                        Transform[] newBones = new Transform[renderer.bones.Length];
                        for (int i = 0; i < renderer.bones.Length; i++)
                        {
                            if (renderer.bones[i] == target.transform)
                            {
                                newBones[i] = child.transform;
                            }
                            else
                            {
                                newBones[i] = renderer.bones[i];
                            }
                        }
                        renderer.bones = newBones;
                    }
                }

                // Record target's state
                Undo.RecordObject(target, "Encapsulate Selected");

                // Finally we set the parent's name
                target.name = parentName;
            }
            else if (referenceRetarget == ReferenceRetargetOption.RetargetAll)
            {
                // For this case, we actually want to make the new object the parent and the current object the child.
                // We'll need to grab all the current object's children and assign them to the new object to keep the hierarchy the same.
                // That's actually all we need to do.  We'll figure out who gets the components in the next step.

                // First we need to create our new object we're splitting into
                // It takes the parent's name since the parent will get the new name.
                GameObject replacer = new GameObject(parentName);
                Undo.RegisterCreatedObjectUndo(replacer, "Encapsulate Selected");
                childIsNew = false;
                newObject = replacer;

                // Find target's position in its parents child order
                int targetIndex = target.transform.GetSiblingIndex();

                // Set the child parent to the target's parent through the undo system
                Undo.SetTransformParent(replacer.transform, target.transform.parent, "Encapsulate Selected");

                // Match the transforms of the target object
                replacer.transform.localPosition = target.transform.localPosition;
                replacer.transform.localRotation = target.transform.localRotation;
                replacer.transform.localScale = target.transform.localScale;

                // Now we need to move all the children of the target to the new object
                List<Transform> children = new List<Transform>();
                for (int i = 0; i < target.transform.childCount; i++)
                {
                    children.Add(target.transform.GetChild(i));
                }

                foreach (Transform child in children)
                {
                    Undo.SetTransformParent(child, replacer.transform, "Encapsulate Selected");
                }

                // Move the target to the new object
                Undo.SetTransformParent(target.transform, replacer.transform, "Encapsulate Selected");

                // Record target's state
                Undo.RecordObject(target, "Encapsulate Selected");

                // Zero out the target's transforms
                target.transform.localPosition = Vector3.zero;
                target.transform.localRotation = Quaternion.identity;
                target.transform.localScale = Vector3.one;

                // Finally we set its name
                target.name = childName;

                // And set the newObject's position in the hierarchy to match the old target's position
                replacer.transform.SetSiblingIndex(targetIndex);
            }
            else
            {
                RaiseCritialError("[Logic Failure] Invalid referenceRetarget option.");
            }

            // Now we need to figure out who gets the components.
            bool shouldTransfer = false;

            if (componentTarget == ParentOrChildOption.Parent)
            {
                if (!childIsNew)
                {
                    shouldTransfer = true;
                }
            }
            else if (componentTarget == ParentOrChildOption.Child)
            {
                if (childIsNew)
                {
                    shouldTransfer = true;
                }
            }

            // If we should transfer components, do so.
            if (shouldTransfer)
            {
                // Iterate over each component on the source GameObject
                foreach (Component sourceComponent in target.GetComponents<Component>())
                {
                    if (sourceComponent is Transform) continue;

                    // Use Undo.AddComponent to add the component to the target GameObject
                    Component newComponent = Undo.AddComponent(newObject, sourceComponent.GetType());

                    // Copy serialized data from the source component to the new component
                    EditorUtility.CopySerialized(sourceComponent, newComponent);
                }

                // Remove components from original object
                foreach (Component sourceComponent in target.GetComponents<Component>())
                {
                    if (sourceComponent is Transform) continue;
                    Undo.DestroyObjectImmediate(sourceComponent);
                }
            }
        }
    }
}