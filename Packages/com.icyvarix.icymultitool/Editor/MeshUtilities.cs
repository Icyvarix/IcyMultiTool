using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Icyvarix.Multitool.Common.Utility;
using static Icyvarix.Multitool.Common.StringUtilities;
using static Icyvarix.Multitool.Common.TransformUtilities;

namespace Icyvarix.Multitool.Common
{   
    public class MeshUtilities
    {
        public static string[] PostRebindOperationsStrings = new string[] { "Reparent + Cleanup", "Reparent Children", "None" };
        public enum PostRebindOperations
        {
            ReparentChildrenAndCleanup,
            ReparentChildren,
            None
        }

        public class BoneMatchException : System.Exception
        {
            public BoneMatchException(string message) : base(message) { }
        }

        public static void RaiseBoneMatchError(string message)
        {
            // Present a popup to the user with the error and then throw an exception
            EditorUtility.DisplayDialog("Matching Error", message, "Unfortunate");

            throw new BoneMatchException(message);
        }

        public static List<Transform> GetMeshBonesAsList(SkinnedMeshRenderer skinnedMeshRenderer, List<Transform> ignoreTransforms = null, string requiredPrefix = null)
        {
            if (skinnedMeshRenderer == null)
            {
                RaiseCritialError("[Logic Failure] SkinnedMeshRenderer is null in mesh bone extraction function.");
            }

            List<Transform> boneList = new List<Transform>();
            boneList.AddRange(skinnedMeshRenderer.bones);

            if (ignoreTransforms != null)
            {
                boneList = boneList.Except(ignoreTransforms).ToList();
            }

            // Make sure all mesh bones start with the required prefix.
            if (requiredPrefix != null)
            {
                List<Transform> invalidBones = boneList.Where(bone => !bone.name.StartsWith(requiredPrefix)).ToList();

                // Display an error if any bones don't start with the required prefix, that lists all the names.
                if (invalidBones.Count > 0)
                {
                    RaiseBoneMatchError($"Not all mesh bones start with '{requiredPrefix}'! Invalid bones: {string.Join(", ", invalidBones.Select(bone => bone.name))}");
                }
            }

            return boneList;
        }

        public static List<Transform> GetAllMeshBonesAsList(List<SkinnedMeshRenderer> skinnedMeshRenderers, List<Transform> ignoreTransforms = null, string requiredPrefix = null)
        {
            List<Transform> meshBones = new List<Transform>();

            // Add all skinned mesh renderer bones to the mesh bones list, if they're not already in there.
            for (int i = 0; i < skinnedMeshRenderers.Count; i++)
            {
                List<Transform> currentMeshBones = GetMeshBonesAsList(skinnedMeshRenderers[i], ignoreTransforms, requiredPrefix);
                if (i == 0)
                {
                    meshBones = currentMeshBones;
                }
                else
                {
                    meshBones = meshBones.Intersect(currentMeshBones).ToList();
                }
            }

            return meshBones;
        }

        public static Dictionary<Transform, Transform> MatchToMeshBones(List<SkinnedMeshRenderer> skinnedMeshRenderers, List<Transform> targetBones, DesiredBoneMatchOption boneMatchOption, List<Transform> ignoreTransforms = null, string targetBonePrefix = null, string meshBonePrefix = null)
        {
            if (skinnedMeshRenderers == null || skinnedMeshRenderers.Count == 0)
            {
                RaiseCritialError("[Logic Failure] SkinnedMeshRenderers is null or empty in mesh bone extraction function.");
            }

            if (targetBones == null || targetBones.Count == 0)
            {
                RaiseCritialError("[Logic Failure] Target bones are null or empty in mesh bone extraction function.");
            }

            // Ignore transforms always include all their children, even if we're searching by name.
            if (ignoreTransforms != null)
            {
                ignoreTransforms = AddAllChildren(ignoreTransforms);
            }

            List<Transform> meshBones = GetAllMeshBonesAsList(skinnedMeshRenderers, ignoreTransforms, meshBonePrefix);

            return MatchTransformsByName(meshBones, targetBones, meshBonePrefix, targetBonePrefix, "", "", boneMatchOption);
        }

        // Rebinds the bones of a SkinnedMeshRenderer, replacing all keys of replaceMap with their corresponding values
        // Returns all keys in replaceMap that are not in the skinnedMeshRenderer's bones array.
        public static List<Transform> RebindBones(SkinnedMeshRenderer skinnedMeshRenderer, Dictionary<Transform, Transform> replaceMap)
        {
            if (skinnedMeshRenderer == null)
            {
                RaiseCritialError("[Logic Failure] SkinnedMeshRenderer is null in mesh rebind function.");
            }

            if (replaceMap == null)
            {
                RaiseCritialError("[Logic Failure] Replace map is null in mesh rebind function.");
            }

            Transform[] oldBones = skinnedMeshRenderer.bones;
            Transform[] newBones = new Transform[oldBones.Length];
            List<Transform> matchedBones = new List<Transform>();

            for (int i = 0; i < oldBones.Length; i++)
            {
                if (replaceMap.ContainsKey(oldBones[i]))
                {
                    newBones[i] = replaceMap[oldBones[i]];
                    matchedBones.Add(oldBones[i]);
                }
                else
                {
                    newBones[i] = oldBones[i]; // Fallback to old bone
                }
            }

            // Make sure we matched all bones in replaceMap.
            List<Transform> unmatchedBones = replaceMap.Keys.Except(matchedBones).ToList();

            //Register undo operation and then perform the rebind
            Undo.RecordObject(skinnedMeshRenderer, "Rebind Bones");

            skinnedMeshRenderer.bones = newBones;

            return unmatchedBones;
        }

        public static void RebindAndAdjustBones(List<SkinnedMeshRenderer> skinnedMeshRenderers, Dictionary<Transform, Transform> rebindMap, PostRebindOperations postRebindOperations, TransformRepositionOption repositionBones, List<Transform> ignoreTransforms = null)
        {
            Undo.SetCurrentGroupName("Rebind Bones");
            int undoGroup = Undo.GetCurrentGroup();

            if (repositionBones == TransformRepositionOption.RepositionSubject)
            {
                MatchTransforms(rebindMap);
            }
            else if (repositionBones == TransformRepositionOption.RepositionTarget)
            {
                Dictionary<Transform, Transform> rebindMapFlipped = rebindMap.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

                MatchTransforms(rebindMapFlipped);
            }

            List<Transform> unmatchedBones = new List<Transform>();

            // Rebind all skinned mesh renderers, make sure that each bone matches at least one time.
            for (int i = 0; i < skinnedMeshRenderers.Count; i++)
            {
                List<Transform> unmatchedBonesForMesh = RebindBones(skinnedMeshRenderers[i], rebindMap);
                
                if (i == 0)
                {
                    unmatchedBones = unmatchedBonesForMesh;
                }
                else
                {
                    unmatchedBones = unmatchedBones.Intersect(unmatchedBonesForMesh).ToList();
                }
            }

            // I don't know why but weird issues happen without this seperating the rebinding and the reparenting.
            Undo.CollapseUndoOperations(undoGroup);

            // Throw the error after the undo collapse so the user can undo the operation.
            if (unmatchedBones.Count > 0)
            {
                RaiseCritialError("[Logic Failure] Failed to match all bones in replace map. Unmatched bones: " + string.Join(", ", unmatchedBones.Select(bone => bone.name)));
            }

            if (postRebindOperations != PostRebindOperations.None)
            {
                if (postRebindOperations == PostRebindOperations.ReparentChildren || postRebindOperations == PostRebindOperations.ReparentChildrenAndCleanup)
                {
                    ReparentChildren(rebindMap, ignoreTransforms);
                }

                // Remove freshly unbound bones if we're cleaning up.
                if (postRebindOperations == PostRebindOperations.ReparentChildrenAndCleanup)
                {
                    // First order rebindmap keys by hierarchy depth, so we can destroy children before parents.
                    List<Transform> orderedBones = rebindMap.Keys.OrderByDescending(t => GetTransformDepth(t)).ToList();

                    // Remove all bones that are keys in the rebind map, as they now have no skinned mesh renderer bound to them.
                    // Make sure to do this through the undo system so the user can undo the operation.
                    // Make sure each bone has no children before destruction.
                    foreach (Transform bone in orderedBones)
                    {
                        if (bone.childCount > 0)
                        {
                            // Just print console warning, we're mid-operation so we can't throw an exception.
                            Debug.LogWarning($"Bone '{bone.name}' has children and will not be removed.");
                        }
                        else
                        {
                            Undo.DestroyObjectImmediate(bone.gameObject);
                        }
                    }
                }

                Undo.CollapseUndoOperations(undoGroup);
            }
        }

        public static Dictionary<Transform, Transform> GenerateAndValidateRebindMap(List<SkinnedMeshRenderer> skinnedMeshRenderers, List<Transform> targetTransforms, DesiredBoneMatchOption boneMatchOption, PostRebindOperations postRebindOperations, List<Transform> ignoreTransforms, string targetBonePrefix, string meshBonePrefix)
        {
            List<Transform> targetBones = new List<Transform>(targetTransforms);
            targetBones = AddAllChildren(targetBones);

            // Print all target bones
            Debug.Log($"Total target bones: {targetBones.Count}");

            List<Transform> fullIgnoreTransforms = new List<Transform>(ignoreTransforms);
            fullIgnoreTransforms = AddAllChildren(fullIgnoreTransforms);

            if (targetBonePrefix != null && targetBonePrefix.Length > 0)
            {
                targetBones = ExtractTransformsWithPrefix(targetBones, targetBonePrefix);
            }

            try
            {
                // Remove ignoreTransforms from targetbones
                targetBones = targetBones.Except(fullIgnoreTransforms).ToList();

                // Make sure no two target bones have the same name.
                if (targetBones.Count != targetBones.Select(bone => bone.name).Distinct().Count())
                {
                    string duplicateNames = string.Join(", ", targetBones.GroupBy(bone => bone.name).Where(group => group.Count() > 1).Select(group => group.Key).ToArray());
                    // Raise match error with a message that lists all the duplicate names
                    RaiseBoneMatchError($"Target bones have duplicate names!\nDuplicate names: {duplicateNames}");
                }

                List<Transform> meshBones = GetAllMeshBonesAsList(skinnedMeshRenderers, fullIgnoreTransforms, meshBonePrefix);
                
                // Two mesh bones should never have the same name.
                if (meshBones.Count != meshBones.Select(bone => bone.name).Distinct().Count())
                {
                    string duplicateNames = string.Join(", ", meshBones.GroupBy(bone => bone.name).Where(group => group.Count() > 1).Select(group => group.Key).ToArray());
                    // Raise match error with a message that lists all the duplicate names
                    RaiseBoneMatchError($"Mesh bones have duplicate names!\nDuplicate names: {duplicateNames}");
                }

                // Print total mesh bones
                Debug.Log($"Total eligible mesh bones: {meshBones.Count}");

                // Print total target bones
                Debug.Log($"Total eligible target bones: {targetBones.Count}");

                // Attempt to match target transforms to skinned mesh renderer bones
                Dictionary<Transform, Transform> rebindMap = MatchToMeshBones(skinnedMeshRenderers, targetBones, boneMatchOption, ignoreTransforms, targetBonePrefix, meshBonePrefix);

                // Print number of bones we matched
                Debug.Log($"Total matched bones: {rebindMap.Count}");

                // Make sure prefabs won't interfere with anything.
                // Only care about children if it's reparent mode, and but care about the bones themselves if it's cleanup mode since we can't delete them if they're in a prefab.
                if (postRebindOperations == PostRebindOperations.ReparentChildrenAndCleanup)
                {
                    // Make sure no mesh bones are in a prefab as otherwise we can't delete them
                    List<Transform> prefabMeshBones = rebindMap.Keys.Where(bone => PrefabUtility.IsPartOfAnyPrefab(bone)).ToList();
                    if (prefabMeshBones.Count > 0)
                    {
                        List<string> badBones = prefabMeshBones.Select(bone => bone.name).Take(10).ToList();

                        if (prefabMeshBones.Count > 10)
                        {
                            badBones.Add("...");
                        }

                        RaiseBoneMatchError($"Some mesh bones are in a prefab!\nRebinding from prefab bones are not supported when cleanup is active.\nPrefab bones: {string.Join(", ", badBones)}");
                    }
                }
                else if (postRebindOperations == PostRebindOperations.ReparentChildren)
                {
                    List<Transform> eligibleMeshBoneChildren = GetAllChildren(rebindMap.Keys.ToList());

                    // Make sure no mesh bone children are in a prefab as otherwise we can't reparent them
                    List<Transform> prefabMeshBones = eligibleMeshBoneChildren.Where(bone => PrefabUtility.IsPartOfAnyPrefab(bone)).ToList();
                    if (prefabMeshBones.Count > 0)
                    {
                        List<string> badBones = prefabMeshBones.Select(bone => bone.name).Take(10).ToList();

                        if (prefabMeshBones.Count > 10)
                        {
                            badBones.Add("...");
                        }

                        RaiseBoneMatchError($"Some mesh bone children are in a prefab!\nReparent children mode can not work on prefab bones.\nPrefab bones: {string.Join(", ", badBones)}");
                    }
                }

                // Ensure all transforms in the mesh are in a continuous hierarchy
                // If they're not, it's going to mess up our child reparenting logic.
                if (postRebindOperations == PostRebindOperations.ReparentChildren || postRebindOperations == PostRebindOperations.ReparentChildrenAndCleanup)
                {
                    bool isConnected;
                    List<Transform> missingTransforms = GetTransformsNeededForCompleteTree(rebindMap.Keys.ToList(), out isConnected);

                    if (missingTransforms != null)
                    {
                        RaiseBoneMatchError($"Not all mesh bones are in a continuous hierarchy!\nHoles are not supported when reparent children mode is active.\nMissing transforms: " + string.Join(", ", missingTransforms.Select(t => t.name)));
                    }
                }

                return rebindMap;
            }
            catch ( BoneMatchException ) { return null; }
        }
    }
}