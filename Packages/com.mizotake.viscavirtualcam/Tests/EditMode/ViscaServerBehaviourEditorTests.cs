#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ViscaControlVirtualCam;

public class ViscaServerBehaviourEditorTests
{
    [Test]
    public void CustomEditor_BindsPendingQueueLimitAndIpSetupNetwork()
    {
        var editorType = ResolveCustomEditorType();
        var go = new GameObject("ViscaServerBehaviourEditorTests");
        UnityEditor.Editor editor = null;

        try
        {
            var behaviour = go.AddComponent<ViscaServerBehaviour>();
            editor = UnityEditor.Editor.CreateEditor(behaviour, editorType);
            Assert.IsNotNull(editor, "Custom editor instance should be created.");

            var pendingQueueLimit = GetPrivateSerializedProperty(editor, "pendingQueueLimit");
            var ipSetupNetwork = GetPrivateSerializedProperty(editor, "ipSetupNetwork");

            Assert.IsNotNull(pendingQueueLimit);
            Assert.IsNotNull(ipSetupNetwork);
            Assert.AreEqual("pendingQueueLimit", pendingQueueLimit.propertyPath);
            Assert.AreEqual("ipSetupNetwork", ipSetupNetwork.propertyPath);
        }
        finally
        {
            if (editor != null) UnityEngine.Object.DestroyImmediate(editor);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static Type ResolveCustomEditorType()
    {
        var type = AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(asm => asm.GetType("ViscaControlVirtualCam.Editor.ViscaServerBehaviourEditor", false))
            .FirstOrDefault(t => t != null);

        Assert.IsNotNull(type, "ViscaServerBehaviourEditor type was not found in loaded assemblies.");
        return type;
    }

    private static SerializedProperty GetPrivateSerializedProperty(UnityEditor.Editor editor, string fieldName)
    {
        var field = editor.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Field '{fieldName}' was not found.");
        return field.GetValue(editor) as SerializedProperty;
    }
}
#endif
