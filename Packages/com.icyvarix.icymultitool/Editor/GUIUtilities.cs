using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using UnityEngine.Animations;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.Dynamics;
using static Icyvarix.Multitool.Common.TransformUtilities;
using static Icyvarix.Multitool.Common.Utility;

namespace Icyvarix.Multitool.Common
{
    public class GUIUtilities
    {
        private static string noodleImagePath = "Packages/com.icyvarix.icymultitool/Resources/noodle.png";

        public static Texture LoadNoodleTexture()
        {
            return AssetDatabase.LoadAssetAtPath<Texture>(noodleImagePath);
        }

        public static ReorderableList InitReorderableTransformList(List<Transform> transforms, string listName, string listDescription)
        {
            ReorderableList reorderableList = new ReorderableList(transforms, typeof(Transform), true, true, true, true);
            reorderableList.drawHeaderCallback = (Rect rect) => {
                EditorGUI.LabelField(rect, new GUIContent(listName, listDescription));
            };

            reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
                transforms[index] = (Transform)EditorGUI.ObjectField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    transforms[index],
                    typeof(Transform),
                    true
                );
            };

            reorderableList.onAddCallback = (ReorderableList list) => {
                list.list.Add(null);  // Add null instead of trying to create a new Transform
            };

            return reorderableList;
        }

    }
}