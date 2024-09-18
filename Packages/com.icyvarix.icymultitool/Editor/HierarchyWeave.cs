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
    public class HierarchyWeaveTool : EditorWindow
    {
        // --------------------------------------------------------------
        // User Input Variables
        private Transform parentHierarchyRoot;
        private Transform childHierarchyRoot;
        DesiredBoneMatchOption boneMatchOption = DesiredBoneMatchOption.ByExactName;

        private string parentPrefix = "";
        private string parentSuffix = "";
        private string childPrefix = "";
        private string childSuffix = "";

        // --------------------------------------------------------------
        // GUI
        private const float baseHeight = 370;
        private const float elementHeight = 23;

        private static string logoPath = "Packages/com.icyvarix.icymultitool/Resources/HierarchyWeaveLogo.png";
        private Texture noodleDragon;
        private static Texture2D windowIcon;
        // --------------------------------------------------------------

        [MenuItem("Tools/Icyvarix/Hierarchy Join")]
        private static void Init()
        {
            var window = (HierarchyWeaveTool)EditorWindow.GetWindow(typeof(HierarchyWeaveTool));
            windowIcon = (Texture2D)AssetDatabase.LoadAssetAtPath(logoPath, typeof(Texture2D));
            window.titleContent = new GUIContent("Hierarchy Join", windowIcon);
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

            GUILayout.Label("Parents every element in Child Hierarchy to the corresponding element in Parent Hierarchy.", EditorStyles.wordWrappedLabel);

            GUILayout.Space(10);
            EditorGUILayout.LabelField(new GUIContent("Parent Hierarchy Root", "Root of the parent hierarchy.  All children of this object will be considered as a target for reparenting."), EditorStyles.boldLabel);
            parentHierarchyRoot = (Transform)EditorGUILayout.ObjectField(parentHierarchyRoot, typeof(Transform), true);

            // Child Hierarchy Root Transform reference
            EditorGUILayout.LabelField(new GUIContent("Child Hierarchy Root", "Root of the child hierarchy.  All children of this object will be considered for reparenting."), EditorStyles.boldLabel);
            childHierarchyRoot = (Transform)EditorGUILayout.ObjectField(childHierarchyRoot, typeof(Transform), true);

            

            // Parent Name Format
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(new GUIContent("Parent Name Format", "Prefix and suffix that each parent object must have to be considered a target for reparenting.  Leave blank to match all objects."), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            // Adjust label width to give more space to the input fields
            EditorGUIUtility.labelWidth = 50;

            parentPrefix = EditorGUILayout.TextField("Prefix", parentPrefix, GUILayout.Width((EditorGUIUtility.currentViewWidth - 20) / 2));
            parentSuffix = EditorGUILayout.TextField("Suffix", parentSuffix, GUILayout.Width((EditorGUIUtility.currentViewWidth - 20) / 2));

            EditorGUILayout.EndHorizontal();

            // Child Name Format
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(new GUIContent("Child Name Format", "Prefix and suffix that each child object must have to be considered for reparenting.  Leave blank to match all objects."), EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            childPrefix = EditorGUILayout.TextField("Prefix", childPrefix, GUILayout.Width((EditorGUIUtility.currentViewWidth - 20) / 2));
            childSuffix = EditorGUILayout.TextField("Suffix", childSuffix, GUILayout.Width((EditorGUIUtility.currentViewWidth - 20) / 2));

            EditorGUILayout.EndHorizontal();

            // Reset label width after use
            EditorGUIUtility.labelWidth = 0;

            // Bone Match Option
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            boneMatchOption = (DesiredBoneMatchOption)EditorGUILayout.Popup(new GUIContent("Object Matching", "How the objects are matched between the hierarchies.\nExact name tries to match items with identical names, regardless of location, while closest name does the same but uses the best match.\nBoth of these will strip the prefix/suffix off of all objects before matching."), (int)boneMatchOption, DesiredBoneMatchOptionStrings);

            GUILayout.Space(10);
            if (GUILayout.Button("Join Hierarchies"))
            {
                List<Transform> parentHierarchy = AddAllChildren(new List<Transform> { parentHierarchyRoot });
                List<Transform> childHierarchy = AddAllChildren(new List<Transform> { childHierarchyRoot });

                List<Transform> parentHierarchyFiltered = ExtractTransformsMatchingRegex(parentHierarchy, "^" + parentPrefix + ".*" + parentSuffix + "$");
                List<Transform> childHierarchyFiltered = ExtractTransformsMatchingRegex(childHierarchy, "^" + childPrefix + ".*" + childSuffix + "$");

                Dictionary<Transform, Transform> reparentMap = null;

                try
                {
                    // Make sure child and parent don't contain any of the same transforms
                    if (childHierarchyFiltered.Any(child => parentHierarchyFiltered.Contains(child)))
                    {
                        RaiseBoneMatchError("Child hierarchy contains objects that are also in the parent hierarchy!");
                    }

                    // Make sure there are no duplicate names in any of the transforms
                    if (parentHierarchyFiltered.Count != parentHierarchyFiltered.Select(bone => bone.name).Distinct().Count())
                    {
                        string duplicateNames = string.Join(", ", parentHierarchyFiltered.GroupBy(bone => bone.name).Where(group => group.Count() > 1).Select(group => group.Key).ToArray());
                        RaiseBoneMatchError($"Parent hierarchy has duplicate transform names!\nDuplicate names: {duplicateNames}");
                    }

                    if (childHierarchyFiltered.Count != childHierarchyFiltered.Select(bone => bone.name).Distinct().Count())
                    {
                        string duplicateNames = string.Join(", ", childHierarchyFiltered.GroupBy(bone => bone.name).Where(group => group.Count() > 1).Select(group => group.Key).ToArray());
                        RaiseBoneMatchError($"Child hierarchy has duplicate transform names!\nDuplicate names: {duplicateNames}");
                    }

                    reparentMap = MatchTransformsByName(childHierarchyFiltered, parentHierarchyFiltered, childPrefix, parentPrefix, childSuffix, parentSuffix, boneMatchOption);
                
                    List<GameObject> prefabObjects = new List<GameObject>();

                    // Make sure none of the keys are in a prefab
                    foreach (var pair in reparentMap)
                    {
                        if (PrefabUtility.IsPartOfAnyPrefab(pair.Key.gameObject))
                        {
                            prefabObjects.Add(pair.Key.gameObject);
                        }
                    }

                    if (prefabObjects.Count > 0)
                    {
                        string prefabNames = string.Join(", ", prefabObjects.Select(t => t.name).ToArray());
                        RaiseBoneMatchError("The following objects are part of a prefab and cannot be reparented: " + prefabNames);
                    }
                }
                catch ( BoneMatchException ) { return; }

                Undo.SetCurrentGroupName("Hierarchy Join");
                int undoGroup = Undo.GetCurrentGroup();

                foreach (var pair in reparentMap)
                {
                    Undo.SetTransformParent(pair.Key, pair.Value, "Reparent " + pair.Key.name);
                }

                Undo.CollapseUndoOperations(undoGroup);
                Debug.Log("Reparented " + reparentMap.Count + " objects.");
            }
        }
    }
}
