using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;
using System;
using System.Reflection;
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

        public static ReorderableList InitReorderableGUIList<T>(List<T> items, string listName, string listDescription) where T : UnityEngine.Object
        {
            ReorderableList reorderableList = new ReorderableList(items, typeof(T), true, true, true, true);
            reorderableList.drawHeaderCallback = (Rect rect) => {
                EditorGUI.LabelField(rect, new GUIContent(listName, listDescription));
            };

            reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
                items[index] = (T)EditorGUI.ObjectField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    items[index],
                    typeof(T),
                    true
                );
            };

            reorderableList.onAddCallback = (ReorderableList list) => {
                list.list.Add(null);  // Add null for the new element
            };

            return reorderableList;
        }
    }
}