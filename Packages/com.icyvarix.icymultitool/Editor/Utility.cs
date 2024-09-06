using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Icyvarix.Multitool.Common
{
    public class Utility
    {
        public static void RaiseCritialError(string message)
        {
            // Present a popup to the user with the error and then throw an exception
            EditorUtility.DisplayDialog("Critical Error", message, "Unfortunate");

            throw new System.Exception(message);
        }
    }
}