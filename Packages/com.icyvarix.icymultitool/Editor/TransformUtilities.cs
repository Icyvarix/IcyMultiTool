using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Icyvarix.Multitool.Common.Utility;
using static Icyvarix.Multitool.Common.StringUtilities;

namespace Icyvarix.Multitool.Common
{    
    public class TransformUtilities
    {
        public static string[] DesiredConstraintTypeStrings = new string[] { "Position", "Rotation", "Scale", "Parent" };
        public enum DesiredConstraintType
        {
            Position,
            Rotation,
            Scale,
            Parent
        }

        public static string[] DesiredMatchOptionStrings = new string[] { "By Name", "By Order" };
        public enum DesiredMatchOption
        {
            ByName,
            ByOrder
        }

        public static string[] ConstraintSpaceOptionStrings = new string[] { "VRC World Space", "VRC Local Space", "Unity World Space" };
        public enum ConstraintSpaceOption
        {
            World,
            Local,
            UnityWorld
        }

        public static string[] DesiredTransformMatchingOptionStrings = new string[] { "Match Source", "Match Target", "None" };
        public enum DesiredTransformMatchingOption
        {
            MatchSource,
            MatchTarget,
            None
        }

        public static string[] TransformRepositionOptionStrings = new string[] { "Subject", "Target", "None" };
        public enum TransformRepositionOption
        {
            RepositionSubject,
            RepositionTarget,
            None
        }

        public static Dictionary<Transform, Transform> MapTransformChildren(Transform receiver, Transform target, DesiredMatchOption option, List<Transform> ignoreTransforms)
        {
            if (receiver.childCount == 0 || target.childCount == 0)
            {
                return new Dictionary<Transform, Transform> {}; // No possible matches.
            }

            // Don't do all the logic to deduce the singular possible match.
            if (receiver.childCount == 1 && target.childCount == 1)
            {
                return new Dictionary<Transform, Transform> { { receiver.GetChild(0), target.GetChild(0) } };
            }

            return option == DesiredMatchOption.ByName ? MapTransformChildrenByName(receiver, target, ignoreTransforms) : MapTransformChildrenByIndex(receiver, target, ignoreTransforms);
        }

        public static Dictionary<Transform, Transform> MapTransformChildrenByIndex(Transform receiver, Transform target, List<Transform> ignoreTransforms)
        {
            Dictionary<Transform, Transform> map = new Dictionary<Transform, Transform>();

            // Turn both receiver and target's children into stacks that we can pop from.
            Stack<Transform> receiverStack = new Stack<Transform>();
            Stack<Transform> targetStack = new Stack<Transform>();

            for (int i = receiver.childCount - 1; i >= 0; i--)
            {
                receiverStack.Push(receiver.GetChild(i));
            }

            for (int i = target.childCount - 1; i >= 0; i--)
            {
                targetStack.Push(target.GetChild(i));
            }

            while (receiverStack.Count > 0 && targetStack.Count > 0)
            {
                Transform receiver_child = receiverStack.Pop();

                if (ignoreTransforms.Contains(receiver_child))
                {
                    continue; // Never try to match ignored transforms.
                }

                Transform target_child = targetStack.Pop();

                if (ignoreTransforms.Contains(target_child))
                {
                    targetStack.Push(receiver_child); // Put the receiver child back on the stack to match on the next iteration.
                    continue; // Never try to match ignored transforms.
                }

                map.Add(receiver_child, target_child);
            }

            return map;
        }

        public static Dictionary<Transform, Transform> MapTransformChildrenByName(Transform receiver, Transform target, List<Transform> ignoreTransforms)
        {
            Dictionary<Transform, Transform> map = new Dictionary<Transform, Transform>();
            List<int> exclude_list = new List<int>();

            int longestReceiverName = receiver.Cast<Transform>().Max(t => t.name.Length);

            // Create a queue for receiver children to match to target children.
            Queue<Transform> receiverQueue = new Queue<Transform>();
            
            for (int i = 0; i < receiver.childCount; i++)
            {
                Transform receiver_child = receiver.GetChild(i);

                if (ignoreTransforms.Contains(receiver_child))
                {
                    continue; // Never try to match ignored transforms.
                }

                receiverQueue.Enqueue(receiver_child);
            }

            while (receiverQueue.Count > 0)
            {
                if (exclude_list.Count >= target.childCount) { break; } // We've matched all the children we can.

                Transform bestMatchChild = null;
                int bestMatchChildTargetIndex = -1;
                int bestMatchSubstringSize = -1;

                // Iterate through all items still in the queue to find the one with the longest substring.
                for (int i = receiverQueue.Count; i > 0; i--)
                {
                    int desired_index;
                    int biggest_substring;

                    Transform receiver_child = receiverQueue.Dequeue();

                    (desired_index, biggest_substring) = FindMatchingChildIndexByName(receiver_child, target, exclude_list.ToArray(), ignoreTransforms);

                    if (biggest_substring > bestMatchSubstringSize)
                    {
                        if (bestMatchChild != null)
                        {
                            receiverQueue.Enqueue(bestMatchChild); // Put the previous best match back on the queue to match on the next iteration.
                        }

                        bestMatchSubstringSize = biggest_substring;
                        bestMatchChild = receiver_child;
                        bestMatchChildTargetIndex = desired_index;
                    }
                    else
                    {
                        receiverQueue.Enqueue(receiver_child); // Put the receiver child back on the queue to match on the next iteration.
                    }
                }

                // Determine minimum substring length to consider a match.
                int requiredSubstringLen = 3; // Minimum substring length to consider a match.

                if (longestReceiverName <= 8)
                {
                    requiredSubstringLen = 2;
                }
                else if (longestReceiverName <= 3)
                {
                    requiredSubstringLen = 1;
                }

                // Add our match to the map and remove the children from further consideration.
                if (bestMatchSubstringSize >= requiredSubstringLen)
                {
                    Transform target_child = target.GetChild(bestMatchChildTargetIndex);
                    exclude_list.Add(bestMatchChildTargetIndex);

                    map.Add(bestMatchChild, target_child);

                    // bestChildMatch gets dropped from the queue by virtue of not getting re-added to it.
                }
                else
                {
                    break; // We've matched all the children we can.
                }
            }

            // We should end with no duplicates in exclude_list.
            if (new HashSet<int>(exclude_list).Count != exclude_list.Count)
                RaiseCritialError("[Logic Failure] Name matched the same child twice!  Tell Icy they're bad at programming.");

            return map;
        }

        private static (int, int) FindMatchingChildIndexByName(Transform receiver_child, Transform target, int[] excludedIndices, List<Transform> ignoreTransforms)
        {
            // Get all target children names into an array, with the order of their index:
            string[] targetChildNames = new string[target.childCount];
            for (int i = 0; i < target.childCount; i++)
            {
                if (System.Array.Exists(excludedIndices, element => element == i))
                {
                    targetChildNames[i] = "";
                }
                else if (ignoreTransforms.Contains(target.GetChild(i))) // Never try to match to excluded transforms.
                {
                    targetChildNames[i] = "";
                }
                else
                {
                    targetChildNames[i] = target.GetChild(i).name;
                }
            }

            // No answer if all target children are empty strings.
            if (targetChildNames.All(s => s == ""))
            {
                return (-1, -1);
            }

            return FindIndexOfStringWithLargestCommonSubstring(receiver_child.name, targetChildNames);
        }

        // Returns a list of all children of the given list, without including any members of the list.
        public static List<Transform> GetImmediateHierarchyChildren(List<Transform> hierarchy)
        {
            List<Transform> children = new List<Transform>();

            foreach (Transform parent in hierarchy)
            {
                for (int i = 0; i < parent.childCount; i++)
                {
                    Transform child = parent.GetChild(i);

                    if (!hierarchy.Contains(child) && !children.Contains(child))
                    {
                        children.Add(child);
                    }
                }
            }

            return children;
        }

        // Makes sure all transforms are in a continious hierarchy, meaning you can traverse the tree from any node to any other node without crossing a non-member.
        public static List<Transform> GetTransformsNeededForCompleteTree(List<Transform> transforms, out bool isConnectedTree)
        {
            if (transforms == null || transforms.Count == 0)
            {
                isConnectedTree = true;
                return null;
            }

            // Find the root nodes in the list (nodes whose parents are not in the list)
            List<Transform> roots = transforms.FindAll(transform => transform.parent == null || !transforms.Contains(transform.parent));

            // There should be exactly one root in a complete tree, so if we have one we're good.
            if (roots.Count == 1)
            {
                isConnectedTree = true;
                return null;
            }

            // If we have more than one root, we need to find the missing transforms.
            List<Transform> missingTransforms = new List<Transform>();

            int connectionFailures = 0;

            foreach (var root in roots)
            {
                List<Transform> seenTransforms = new List<Transform>();

                Transform current = root.parent;
                bool foundExistingTransform = false;
                while (current != null)
                {
                    if (transforms.Contains(current))
                    {
                        foundExistingTransform = true;
                        break;
                    }

                    if (!seenTransforms.Contains(current))
                    {
                        seenTransforms.Add(current);
                    }
                    current = current.parent;
                }

                if (foundExistingTransform)
                {
                    missingTransforms.AddRange(seenTransforms);
                }
                else
                {
                    connectionFailures++;
                }
            }

            // The highest tree root will always get to the top of the hierarchy, so we can ignore it.
            if (connectionFailures > 1)
            {
                isConnectedTree = false;
                return null;
            }

            isConnectedTree = true;
            return missingTransforms;
        }

        // Returns a list of all children, all the way down, of the given list, without including any members of the list.
        public static List<Transform> GetAllChildren(List<Transform> transformList)
        {
            List<Transform> children = new List<Transform>();

            foreach (Transform parent in transformList)
            {
                Transform[] allChildren = parent.GetComponentsInChildren<Transform>();

                foreach (Transform child in allChildren)
                {
                    if (!transformList.Contains(child) && !children.Contains(child))
                    {
                        children.Add(child);
                    }
                }
            }

            return children;
        }

        public static List<Transform> AddAllChildren(List<Transform> transformList)
        {
            List<Transform> children = GetAllChildren(transformList);

            // Add original list to children.
            foreach (Transform parent in transformList)
            {
                if (!children.Contains(parent))
                {
                    children.Add(parent);
                }
            }

            return children;
        }

        // Reparents the children of the keys of reparentMap to the values of reparentMap.
        public static void ReparentChildren(Dictionary<Transform, Transform> reparentMap, List<Transform> ignoreTransforms)
        {
            List<Transform> children = GetAllChildren(reparentMap.Keys.ToList());

            foreach (Transform child in children)
            {
                if (ignoreTransforms.Contains(child))
                {
                    continue; // Never try to reparent ignored transforms.
                }

                if (reparentMap.ContainsKey(child.parent))
                {
                    Undo.SetTransformParent(child, reparentMap[child.parent], "Reparenting Children");
                }
            }
        }

        public static List<Transform> ExtractTransformsWithPrefix(List<Transform> transforms, string prefix)
        {
            List<Transform> extracted = new List<Transform>();

            for (int i = transforms.Count - 1; i >= 0; i--)
            {
                if (transforms[i].name.StartsWith(prefix))
                {
                    transforms.RemoveAt(i);
                    extracted.Add(transforms[i]);
                }
            }

            return extracted;
        }

        // Matches each key in matchMap to the corresponding transform in the value of matchMap.
        // Sets global position, rotation, and scale of the key to the value.
        // Always does parents first, so that children are unaffected by their movements.
        public static void MatchTransforms(Dictionary<Transform, Transform> matchMap)
        {
            // Sort keys by depth to ensure parents are processed before children
            var sortedKeys = matchMap.Keys.OrderBy(t => GetTransformDepth(t)).ToList();

            Undo.RecordObjects(sortedKeys.ToArray(), "Match Transforms"); // Record all objects in the list.

            foreach (Transform key in sortedKeys)
            {
                if (matchMap.TryGetValue(key, out Transform target))
                {
                    // Match global position, rotation, and scale
                    key.position = target.position;
                    key.rotation = target.rotation;
                    key.localScale = GetRelativeLocalScale(key.parent, target.localScale);
                }
            }
        }

        public static int GetTransformDepth(Transform t)
        {
            int depth = 0;
            while (t.parent != null)
            {
                depth++;
                t = t.parent;
            }
            return depth;
        }

        // Helper to calculate local scale based on the target global scale
        private static Vector3 GetRelativeLocalScale(Transform parent, Vector3 globalScale)
        {
            if (parent == null)
            {
                return globalScale;
            }
            else
            {
                // Calculate local scale by considering parent's scale
                return new Vector3(
                    globalScale.x / parent.lossyScale.x,
                    globalScale.y / parent.lossyScale.y,
                    globalScale.z / parent.lossyScale.z);
            }
        }
    }
}