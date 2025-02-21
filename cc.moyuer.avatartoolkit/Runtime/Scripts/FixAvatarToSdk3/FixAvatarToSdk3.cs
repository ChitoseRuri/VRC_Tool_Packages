#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor.CustomEyeLookSettings;
using static VRChatAvatarToolkit.MoyuToolkitUtils;

namespace VRChatAvatarToolkit
{
    public class FixAvatarToSdk3 : FixAvatarToSdk3Utils
    {
        private Vector2 mainScrollPos;
        private GameObject avatar;
        private AvatarType avatarType;
        private AnimatorOverrideController standController, sitController;

        private void OnGUI()
        {
            mainScrollPos = GUILayout.BeginScrollView(mainScrollPos);
            GUILayout.Space(10);
            GUI.skin.label.fontSize = 24;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label("SDK升级工具");
            GUI.skin.label.fontSize = 12;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label("by:如梦");
            GUILayout.Space(10);
            GUI.skin.label.fontSize = 12;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label("支持SDK2模型转换至SDK3");
            GUILayout.Space(10);
            var nowAvatar = (GameObject)EditorGUILayout.ObjectField("选择模型", avatar, typeof(GameObject), true);

            switch (avatarType)
            {
                case AvatarType.NONE:
                    EditorGUILayout.HelpBox("请选择你的模型", MessageType.Warning);
                    standController = null;
                    sitController = null;
                    break;
                case AvatarType.DEFAULT:
                    EditorGUILayout.HelpBox("选择的不是SDK2模型，将使用默认参数进行填充，转换后请注意核对参数！", MessageType.Warning);
                    break;
                case AvatarType.SDK3:
                    EditorGUILayout.HelpBox("已是SDK3模型了，不需要转换", MessageType.Error);
                    break;
            }

            if (nowAvatar != avatar)
            {
                avatar = nowAvatar;
                ReloadConfig();
            }

            standController = (AnimatorOverrideController)EditorGUILayout.ObjectField("Stand Controller", standController, typeof(AnimatorOverrideController), true);
            sitController = (AnimatorOverrideController)EditorGUILayout.ObjectField("Sit Controller", sitController, typeof(AnimatorOverrideController), true);

            GUILayout.Space(10);
            EditorGUI.BeginDisabledGroup(avatarType == AvatarType.NONE || avatarType == AvatarType.SDK3);
            if (GUILayout.Button("开始转换"))
            {
                GUI.enabled = false;
                UpdateAvatarSDK(nowAvatar, standController, sitController);
                GUI.enabled = true;
            }
            EditorGUI.EndDisabledGroup();
            GUILayout.EndScrollView();
        }

        private void ReloadConfig()
        {
            standController = null;
            sitController = null;
            avatarType = GetAvatarType(avatar);
            if (avatarType == AvatarType.SDK2)
            {
                var descriptor = avatar.GetComponent<VRCSDK2.VRC_AvatarDescriptor>();
                standController = descriptor.CustomStandingAnims;
                sitController = descriptor.CustomSittingAnims;
            }
        }
    }
}
#endif