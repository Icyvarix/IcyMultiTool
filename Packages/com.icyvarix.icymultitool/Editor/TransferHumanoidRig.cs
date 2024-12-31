using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using UnityEngine.Animations;
using static Icyvarix.Multitool.Common.TransformUtilities;
using static Icyvarix.Multitool.Common.Utility;
using static Icyvarix.Multitool.Common.GUIUtilities;
using static Icyvarix.Multitool.Common.AnimationUtilities;

namespace Icyvarix.Multitool.Tools
{
    public class TransferHumanoidRig : EditorWindow
    {
        // --------------------------------------------------------------
        // GUI Defines
        public string[] rigTransferTypeStrings = new string[] { "All Data", "Bone Data" };
        public enum RigTransferType
        {
            SerializedData,
            BoneNameMatch
        }

        // --------------------------------------------------------------
        // User Input Variables
        private GameObject sourceFBX;
        private GameObject destinationFBX;
        private RigTransferType rigTransferType = 0;

        // --------------------------------------------------------------
        // GUI
        private Vector2 scrollPosition;
        private const float baseHeight = 200;
        private const float elementHeight = 23;

        private static string logoPath = "Packages/com.icyvarix.icymultitool/Resources/AnimationTrianglesLogo.png";
        private Texture noodleDragon;
        private static Texture2D windowIcon;
        // --------------------------------------------------------------

        [MenuItem("Tools/Icyvarix/Transfer FBX Humanoid Rig")]
        private static void Init()
        {
            var window = (TransferHumanoidRig)EditorWindow.GetWindow(typeof(TransferHumanoidRig));
            windowIcon = (Texture2D)AssetDatabase.LoadAssetAtPath(logoPath, typeof(Texture2D));
            window.titleContent = new GUIContent("Transfer FBX Humanoid Rig", windowIcon);
            window.Show();
        }

        void OnEnable()
        {
            noodleDragon = LoadNoodleTexture();
        }

        private void OnGUI()
        {
            float minHeight = baseHeight;
            this.minSize = new Vector2(340, minHeight);

            // Stick me in there first so all the stuff gets drawn over me.
            if (noodleDragon != null)
            {
                float imageWidth = position.width; // I deserve to be as wide as the window...but should probably actually just stick to the corner.  Maybe later.
                float imageHeight = imageWidth * 0.19f;

                Rect imageRect = new Rect(0, position.height - imageHeight, imageWidth, imageHeight);
                GUI.DrawTexture(imageRect, noodleDragon, ScaleMode.ScaleToFit);
            }

            GUILayout.Label("Transfer the humanoid avatar definition from one FBX to another.", EditorStyles.wordWrappedLabel);

            GUILayout.Space(10);

            GUILayout.Label(new GUIContent("Transfer Method", "SerializedData = Copy the raw serialized data over\nBone Name Matching = Try to reconstruct the bone assignments via name."), EditorStyles.boldLabel);
            rigTransferType = (RigTransferType)GUILayout.Toolbar((int)rigTransferType, rigTransferTypeStrings, GUILayout.ExpandWidth(true));

            GUILayout.Space(10);

            sourceFBX = (GameObject)EditorGUILayout.ObjectField("Source FBX", sourceFBX, typeof(GameObject), false);
            destinationFBX = (GameObject)EditorGUILayout.ObjectField("Destination FBX", destinationFBX, typeof(GameObject), false);

            GUILayout.Space(10);
            if (GUILayout.Button("Retarget Animations"))
            {
                if (sourceFBX == null || destinationFBX == null)
                {
                    EditorUtility.DisplayDialog("Error", "Please assign both source and destination FBX", "Fine");
                    return;
                }

                TransferRigMapping(sourceFBX, destinationFBX, rigTransferType);
            }
        }

        private void TransferRigMapping(GameObject sourceFBX, GameObject destinationFBX, RigTransferType transferType)
        {
            string sourcePath = AssetDatabase.GetAssetPath(sourceFBX);
            string destPath   = AssetDatabase.GetAssetPath(destinationFBX);

            var sourceImporter = AssetImporter.GetAtPath(sourcePath) as ModelImporter;
            var destImporter   = AssetImporter.GetAtPath(destPath)   as ModelImporter;

            if (sourceImporter == null || destImporter == null)
            {
                EditorUtility.DisplayDialog("Error", "Unable to retrieve ModelImporter for one of the FBXs. Check that both are valid Model assets.", "Fine");
                return;
            }

            // Ensure both rigs are set to Humanoid
            if (sourceImporter.animationType != ModelImporterAnimationType.Human ||
                destImporter.animationType   != ModelImporterAnimationType.Human)
            {
                EditorUtility.DisplayDialog("Error", "Both Source and Destination FBXs must be set to Humanoid rig type.", "Fine");
                return;
            }

            if (transferType == RigTransferType.SerializedData)
            {
                // Wrap them in SerializedObject so we can copy serialized data
                SerializedObject srcSerialized = new SerializedObject(sourceImporter);
                SerializedObject dstSerialized = new SerializedObject(destImporter);

                // The property we need to copy is "m_HumanDescription"
                SerializedProperty srcHumanDesc = srcSerialized.FindProperty("m_HumanDescription");
                SerializedProperty dstHumanDesc = dstSerialized.FindProperty("m_HumanDescription");

                if (srcHumanDesc != null && dstHumanDesc != null)
                {
                    // Copy the entire "m_HumanDescription" from source to target
                    dstSerialized.CopyFromSerializedProperty(srcHumanDesc);
                    // Apply changes
                    dstSerialized.ApplyModifiedProperties();
                }
                else
                {
                    // Clearly tell the user which property is missing, srcHumanDesc or dstHumanDesc or both
                    string missing_string;

                    if (srcHumanDesc == null && dstHumanDesc == null)
                    {
                        missing_string = "source and destination";
                    }
                    else if (srcHumanDesc == null)
                    {
                        missing_string = "source";
                    }
                    else
                    {
                        missing_string = "destination";
                    }

                    EditorUtility.DisplayDialog("Error", $"Could not find m_HumanDescription on {missing_string}.", "Fine");
                }
            }
            else if (transferType == RigTransferType.BoneNameMatch)
            {
                HumanDescription sourceHumanDesc = sourceImporter.humanDescription;
                HumanBone[] sourceHumanBones     = sourceHumanDesc.human;

                HumanDescription destHumanDesc   = destImporter.humanDescription;
                SkeletonBone[] destSkeleton      = destHumanDesc.skeleton;

                // Prepare an array for the new human bones in the destination
                HumanBone[] newDestHumanBones = new HumanBone[sourceHumanBones.Length];

                for (int i = 0; i < sourceHumanBones.Length; i++)
                {
                    HumanBone sourceBone = sourceHumanBones[i];
                    string sourceBoneName = sourceBone.boneName;
                    string sourceHumanName = sourceBone.humanName;

                    // Create a new HumanBone with the same humanName
                    HumanBone newBone = new HumanBone
                    {
                        humanName = sourceHumanName,
                        limit     = sourceBone.limit // Copy rotation limits as well
                    };

                    if (!string.IsNullOrEmpty(sourceBoneName))
                    {
                        // Attempt to find a matching bone name in the destination skeleton
                        bool foundDestinationBone = false;
                        foreach (var destSkelBone in destSkeleton)
                        {
                            if (destSkelBone.name == sourceBoneName)
                            {
                                newBone.boneName = destSkelBone.name;
                                foundDestinationBone = true;
                                break;
                            }
                        }

                        if (!foundDestinationBone)
                        {
                            Debug.LogWarning($"Could not find a matching bone name '{sourceBoneName}' in destination skeleton. This bone will not be assigned.");
                            newBone.boneName = string.Empty;  // or keep it empty
                        }
                    }
                    else
                    {
                        newBone.boneName = string.Empty;
                    }

                    newDestHumanBones[i] = newBone;
                }

                // Apply the new human bone array to the destination's HumanDescription
                destHumanDesc.human = newDestHumanBones;

                // Reassign the HumanDescription and save changes
                destImporter.humanDescription = destHumanDesc;
            }

            destImporter.SaveAndReimport();
            Debug.Log($"Successfully transferred humanoid rig mapping from {sourceFBX.name} to {destinationFBX.name}");
        }
    }
}
