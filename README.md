# Icy's Multi Tool

A set of tools for VRChat avatar creation, primarily addressing the issues and needs my friends and I have encountered while working on multi-limbed avatars.

Has a [VCC Listing](https://icyvarix.github.io/IcyMultiTool/) for automatic installation!

# Included Tools:

## Chain Constrain
Tool for constraining one hierarchy to another.  Will first try to match each bone in the receiver hierarchy to a corresponding bone in the target hierarchy, and will then apply the selected constraint type to each valid pair.

## Mesh Rebind
Tool for reparenting a SkinnedMeshRenderer to another armature in the Unity editor.  Will first try to match each bone that the mesh is currently following to a bone in a set of user-supplied target hierarchies.  Will then retarget each vertexgroup from the current bone to the new one.

Supports automatically reparenting children that were not matched, and adjusting bone transforms to match.

## Encapsulate Selected
Splits each selected object into two: a child and a parent.  Can configure which one gets the components of the original object, and which references target which object.

## Hierarchy Join
Joins two hierarchies together, with a parent/child relationship.  Will first try to match each bone in the child hierarchy to a corresponding bone in the parent hierarchy, and will then parent each child bone to the corresponding parent bone.

--------------------

I hope these tools prove useful to you!
