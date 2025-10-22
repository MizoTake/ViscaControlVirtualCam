#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ViscaControlVirtualCam.Editor
{
    [CustomEditor(typeof(PtzControllerBehaviour))]
    [CanEditMultipleObjects]
    public class PtzControllerBehaviourEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var ctrl = (PtzControllerBehaviour)target;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Settings Now"))
                {
                    foreach (var t in targets)
                    {
                        var c = t as PtzControllerBehaviour;
                        if (c != null)
                        {
                            Undo.RecordObject(c, "Apply PTZ Settings");
                            c.ApplySettings();
                            EditorUtility.SetDirty(c);
                        }
                    }
                }
            }
        }
    }
}
#endif

