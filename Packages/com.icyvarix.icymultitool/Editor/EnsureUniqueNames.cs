using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using UnityEngine.Animations;
using System.Linq;
using static Icyvarix.Multitool.Common.GUIUtilities;

namespace Icyvarix.Multitool.Tools
{
    public class EnsureUniqueNamesTool : EditorWindow
    {
        // --------------------------------------------------------------
        // User Input Variables
        private List<Transform> checkRootTransformsList = new List<Transform>();
        private ReorderableList reorderableCheckRootTransformsList;

        // --------------------------------------------------------------
        // GUI
        private const float baseHeight = 200;
        private const float elementHeight = 23;

        private static string logoPath = "Packages/com.icyvarix.icymultitool/Resources/EnsureNamesLogo.png";
        private Texture noodleDragon;
        private static Texture2D windowIcon;
        // --------------------------------------------------------------

        [MenuItem("Tools/Icyvarix/Find Duplicate Names")]
        private static void Init()
        {
            var window = (EnsureUniqueNamesTool)EditorWindow.GetWindow(typeof(EnsureUniqueNamesTool));
            windowIcon = (Texture2D)AssetDatabase.LoadAssetAtPath(logoPath, typeof(Texture2D));
            window.titleContent = new GUIContent("Find Duplicate Names", windowIcon);
            window.Show();
        }

        void OnEnable()
        {
            reorderableCheckRootTransformsList = InitReorderableGUIList<Transform>(checkRootTransformsList, "Root Transforms", "Transforms to include in the check for duplicate names.  All transforms and their children will be checked at once.");
            noodleDragon = LoadNoodleTexture();
        }

        private void OnGUI()
        {
            this.minSize = new Vector2(340, baseHeight + Mathf.Max(checkRootTransformsList.Count - 1, 0) * elementHeight);

            // Stick me in there first so all the stuff gets drawn over me.
            if (noodleDragon != null)
            {
                float imageWidth = position.width; // I deserve to be as wide as the window...but should probably actually just stick to the corner.  Maybe later.
                float imageHeight = imageWidth * 0.19f;

                Rect imageRect = new Rect(0, position.height - imageHeight, imageWidth, imageHeight);
                GUI.DrawTexture(imageRect, noodleDragon, ScaleMode.ScaleToFit);
            }

            GUILayout.Label("Checks transforms and their children for duplicate names.", EditorStyles.wordWrappedLabel);

            GUILayout.Space(10);
            reorderableCheckRootTransformsList.DoLayoutList();

            GUILayout.Space(10);
            if (GUILayout.Button("Check Names for Duplicates"))
            {
                List<Transform> allTransforms = new List<Transform>();
                foreach (Transform root in checkRootTransformsList)
                {
                    if (root == null)
                    {
                        continue;
                    }

                    allTransforms.AddRange(root.GetComponentsInChildren<Transform>());
                }

                // First identify all duplicate transforms, then select them in the hierarchy.
                List<Transform> duplicateTransforms = allTransforms.GroupBy(bone => bone.name).Where(group => group.Count() > 1).SelectMany(group => group).ToList();

                if (duplicateTransforms.Count > 0)
                {
                    // We need to select gameobjects not transforms
                    List<GameObject> duplicateGameObjects = new List<GameObject>();
                    foreach (var t in duplicateTransforms)
                    {
                        duplicateGameObjects.Add(t.gameObject);
                    }

                    Selection.objects = duplicateGameObjects.ToArray();

                    // Highlight each object by pinging it
                    foreach (var t in duplicateGameObjects)
                    {
                        EditorGUIUtility.PingObject(t);
                    }

                    // Just use a log message instead of a popup
                    Debug.Log("Found " + duplicateTransforms.Count + " duplicate names.");
                }
                else
                {
                    EditorUtility.DisplayDialog("Check Names for Duplicates", "No duplicate names found.", "Awesome");
                }
            }
        }
    }
}