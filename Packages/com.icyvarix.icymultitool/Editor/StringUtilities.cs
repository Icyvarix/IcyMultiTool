using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Icyvarix.Multitool.Common.Utility;
using static Icyvarix.Multitool.Common.StringUtilities;
using System;
using System.Text.RegularExpressions;

namespace Icyvarix.Multitool.Common
{
    public class StringUtilities
    {
        // $oldname will be replaced with the old name.  $oldname[x:y] will be replaced with a substring of the old name.
        public static string CalculateNewNameFromFormatString(string fmt, string oldname)
        {
            if (string.IsNullOrEmpty(fmt))
                return oldname;

            // Regex to find $oldname[x:y] patterns
            string pattern = @"\$oldname\[(\d*):(\d*)\]";
            string result = Regex.Replace(fmt, pattern, match =>
            {
                // Parse the start and end indices, defaulting to full range if blank
                string startStr = match.Groups[1].Value;
                string endStr = match.Groups[2].Value;

                int start = string.IsNullOrEmpty(startStr) ? 0 : int.Parse(startStr);
                int end = string.IsNullOrEmpty(endStr) ? oldname.Length : int.Parse(endStr);

                // Clamp indices to valid range
                start = Math.Max(0, Math.Min(start, oldname.Length));
                end = Math.Max(start, Math.Min(end, oldname.Length));

                // Extract the substring
                string substring = oldname.Substring(start, end - start);
                return substring;
            });

            // Replace all instances of $oldname with the full oldname
            result = result.Replace("$oldname", oldname);

            return result;
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

        // Returns a dictionary of strings from list1 to strings in list2 that have the longest common substring.
        // Will find the longest substring of all the strings in list1 before moving on to the next iteration, to prevent order from causing issues.
        // Resulting dictionary will be the size of the smallest list.
        public static Dictionary<string, string> MatchByLongestSubstring(List<string> list1, List<string> list2)
        {
            var result = new Dictionary<string, string>();
            var remainingList1 = new List<string>(list1);
            var remainingList2 = new List<string>(list2);

            while (remainingList1.Count > 0 && remainingList2.Count > 0)
            {
                (string item1, string item2) bestMatch = ("", "");
                int maxMatchLength = 0;
                List<(string item1, string item2, List<int> lengths)> candidates = new List<(string, string, List<int>)>();

                foreach (var item1 in remainingList1)
                {
                    foreach (var item2 in remainingList2)
                    {
                        int length = FindLongestCommonSubstring(item1, item2).Length;
                        if (length > maxMatchLength)
                        {
                            maxMatchLength = length;
                            bestMatch = (item1, item2);
                            candidates = new List<(string, string, List<int>)> { (item1, item2, GetAllCommonSubstringLengths(item1, item2)) };
                        }
                        else if (length == maxMatchLength)
                        {
                            candidates.Add((item1, item2, GetAllCommonSubstringLengths(item1, item2)));
                        }
                    }
                }

                if (candidates.Count > 1)
                {
                    candidates = candidates.OrderByDescending(c => c.lengths, new LengthComparer()).ToList();
                    bestMatch = (candidates.First().item1, candidates.First().item2);
                }

                result[bestMatch.item1] = bestMatch.item2;
                remainingList1.Remove(bestMatch.item1);
                remainingList2.Remove(bestMatch.item2);
            }

            return result;
        }

        private static char[] prefixSuffixDelimiters = { ' ', '.', '_' };

        public static string StripCommonPrefix(List<string> strings)
        {
            if (strings == null || !strings.Any())
                return null;

            // Find the shortest string to limit the prefix search.
            var minLength = strings.Min(s => s.Length);
            string prefix = null;

            for (int i = 0; i < minLength; i++)
            {
                // Check if the current character is the same across all strings
                char current = strings[0][i];
                if (!strings.All(s => s[i] == current))
                    break;

                // Check if the character is a delimiter
                if (prefixSuffixDelimiters.Contains(current))
                {
                    prefix = strings[0].Substring(0, i + 1);
                    break;
                }
            }

            if (prefix != null)
            {
                // Remove the prefix from all strings in the list
                for (int i = 0; i < strings.Count; i++)
                {
                    strings[i] = strings[i].Substring(prefix.Length);
                }
            }

            return prefix;
        }

        public static string StripCommonSuffix(List<string> strings)
        {
            if (strings == null || !strings.Any())
                return null;

            // Find the shortest string to limit the suffix search.
            var minLength = strings.Min(s => s.Length);
            string suffix = null;

            for (int i = 0; i < minLength; i++)
            {
                // Check if the current character is the same across all strings from the end
                char current = strings[0][strings[0].Length - i - 1];
                if (!strings.All(s => s[s.Length - i - 1] == current))
                    break;

                // Check if the character is a delimiter
                if (prefixSuffixDelimiters.Contains(current))
                {
                    suffix = strings[0].Substring(strings[0].Length - i - 1);
                    break;
                }
            }

            if (suffix != null)
            {
                // Remove the suffix from all strings in the list
                for (int i = 0; i < strings.Count; i++)
                {
                    strings[i] = strings[i].Substring(0, strings[i].Length - suffix.Length);
                }
            }

            return suffix;
        }

        public static void StripCommonPrefixAndSuffix(List<string> strings)
        {
            StripCommonPrefix(strings);
            StripCommonSuffix(strings);
        }

        public static bool StripDefinedPrefix(List<string> stringList, string commonString)
        {
            bool allStripped = true;
            for (int i = 0; i < stringList.Count; i++)
            {
                if (stringList[i].StartsWith(commonString))
                {
                    stringList[i] = stringList[i].Substring(commonString.Length);
                }
                else
                {
                    allStripped = false;
                }
            }
            return allStripped;
        }

        public static bool StripDefinedSuffix(List<string> stringList, string commonString)
        {
            bool allStripped = true;
            for (int i = 0; i < stringList.Count; i++)
            {
                if (stringList[i].EndsWith(commonString))
                {
                    stringList[i] = stringList[i].Substring(0, stringList[i].Length - commonString.Length);
                }
                else
                {
                    allStripped = false;
                }
            }
            return allStripped;
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

        private static List<int> GetAllCommonSubstringLengths(string s1, string s2)
        {
            int[,] dp = new int[s1.Length + 1, s2.Length + 1];
            List<int> lengths = new List<int>();

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    if (s1[i - 1] == s2[j - 1])
                    {
                        dp[i, j] = dp[i - 1, j - 1] + 1;
                        if (dp[i, j] > 0)
                        {
                            lengths.Add(dp[i, j]);
                        }
                    }
                }
            }

            lengths.Sort((a, b) => b.CompareTo(a));
            return lengths;
        }

        private class LengthComparer : IComparer<List<int>>
        {
            public int Compare(List<int> x, List<int> y)
            {
                for (int i = 0; i < Mathf.Min(x.Count, y.Count); i++)
                {
                    if (x[i] != y[i])
                    {
                        return y[i].CompareTo(x[i]);
                    }
                }
                return x.Count.CompareTo(y.Count);
            }
        }
    }
}