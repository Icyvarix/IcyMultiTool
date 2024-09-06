using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Icyvarix.Multitool.Common.Utility;

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

        public static (int, int) FindIndexOfStringWithLargestCommonSubstring(string targetString, string[] strings)
        {
            if (strings == null || strings.Length == 0 || targetString == null || targetString.Length == 0)
                return (-1, -1);

            int n = strings.Length;
            Dictionary<int, string> candidates = strings.Select((s, i) => new { String = s, Index = i })
                                                        .ToDictionary(x => x.Index, x => x.String);

            int biggestMaxLength = -1; // The biggest substring we found during this search.

            while (true)
            {
                // Dictionary to hold the length of the longest common substrings and the substrings themselves
                Dictionary<int, (int Length, string Substring)> substrings = candidates.ToDictionary(
                    pair => pair.Key,
                    pair => FindLongestCommonSubstring(targetString, pair.Value)
                );

                // Find the maximum length of substrings found
                int maxLength = substrings.Max(pair => pair.Value.Length);
                biggestMaxLength = Mathf.Max(biggestMaxLength, maxLength);
                
                // Did not find a substring, so we failed to find a match.
                if (maxLength == 0)
                {
                    return (-1, biggestMaxLength);
                }

                // Filter the candidates that have the maximum substring length
                var maxCandidates = substrings.Where(pair => pair.Value.Length == maxLength)
                                            .ToDictionary(pair => pair.Key, pair => candidates[pair.Key]);

                // Should be impossible...but just in case
                if (maxCandidates.Count == 0)
                    RaiseCritialError("[Logic Failure] Got 0 maxCandidates for string " + targetString + "!  Yell at Icy.");

                // If only one candidate remains, return its index
                if (maxCandidates.Count == 1)
                {
                    return (maxCandidates.First().Key, biggestMaxLength);
                }

                // If all remaining candidates are identical, we failed to find a clear winner.
                else if (maxCandidates.All(kvp => kvp.Value == maxCandidates.First().Value))
                {
                    return (-1, biggestMaxLength);
                }
                else
                {
                    // Blank out all values in candidates that are not in maxCandidates
                    foreach (var kvp in candidates.ToList())
                    {
                        if (!maxCandidates.ContainsKey(kvp.Key))
                        {
                            candidates[kvp.Key] = ""; // Remove from the running but don't change the index.
                        }
                    }

                    // If we have multiple candidates, we need a tiebreaker.
                    // Update candidates by removing the found substrings and continue
                    foreach (var kvp in maxCandidates)
                    {
                        string currentString = kvp.Value;
                        string substringToRemove = substrings[kvp.Key].Substring;

                        // Make sure no infinite loops happen
                        if (substringToRemove.Length == 0)
                        {
                            RaiseCritialError("[Logic Failure] Tried to remove substring of length 0 while scanning " + targetString + "! Icy doesn't know what they're doing! Yell at them!");
                        }

                        candidates[kvp.Key] = currentString.Replace(substringToRemove, "");
                    }
                }
            }
        }

        public static (int Length, string Substring) FindLongestCommonSubstring(string s1, string s2)
        {
            int[,] lengths = new int[s1.Length + 1, s2.Length + 1];
            int greatestLength = 0;
            string longestSubstring = "";

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    if (s1[i - 1] == s2[j - 1])
                    {
                        lengths[i, j] = lengths[i - 1, j - 1] + 1;
                        if (lengths[i, j] > greatestLength)
                        {
                            greatestLength = lengths[i, j];
                            longestSubstring = s1.Substring(i - greatestLength, greatestLength);
                        }
                    }
                    else
                    {
                        lengths[i, j] = 0;
                    }
                }
            }

            return (greatestLength, longestSubstring);
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
                else if (ignoreTransforms.Contains(target.GetChild(i))) // NEver try to match to excluded transforms.
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
    }
}