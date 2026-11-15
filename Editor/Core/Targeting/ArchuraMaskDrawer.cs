using UnityEditor;
using UnityEngine;

namespace Archura.UVMaskPainter.Editor.Targeting
{
    /// <summary>Backs the [ArchuraMask] shader attribute; draws like the default property.</summary>
    public sealed class ArchuraMaskDrawer : MaterialPropertyDrawer
    {
        public override void OnGUI(Rect position, MaterialProperty prop, GUIContent label, MaterialEditor editor)
        {
            editor.DefaultShaderProperty(position, prop, label.text);
        }

        public override float GetPropertyHeight(MaterialProperty prop, string label, MaterialEditor editor)
        {
            return MaterialEditor.GetDefaultPropertyHeight(prop);
        }
    }
}
