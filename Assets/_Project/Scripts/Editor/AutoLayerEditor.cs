using InElementum.Core.Utils;
using UnityEditor;

namespace InElementum.Editor
{
    public class AutoLayerEditor : UnityEditor.Editor
    {
        [MenuItem("Tools/In Elementum/Set Layers")]
        public static void SetLayers()
        {
            new Layers().AddNewLayer("Character");
        }
    }
}
