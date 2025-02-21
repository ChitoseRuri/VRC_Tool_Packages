#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System;
using System.Collections;
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

    public class FixAvatarToSdk3Utils : EditorWindow
    {
        // private static readonly string[] visemeBlendShapes = { "vrc.v_sil", "vrc.v_pp", "vrc.v_ff", "vrc.v_th", "vrc.v_dd", "vrc.v_kk", "vrc.v_ch", "vrc.v_ss", "vrc.v_nn", "vrc.v_rr", "vrc.v_aa", "vrc.v_e", "vrc.v_ih", "vrc.v_oh", "vrc.v_ou" };
        public enum AvatarType
        {
            NONE = 0,
            DEFAULT = 1,
            SDK2 = 2,
            SDK3 = 3,
        }
        // 判断模型类型
        public static AvatarType GetAvatarType(GameObject avatar)
        {
            if (avatar == null)
                return AvatarType.NONE;
            else if (avatar.GetComponent<VRCSDK2.VRC_AvatarDescriptor>() != null)
                return AvatarType.SDK2;
#if VRC_SDK_VRCSDK3
            else if (avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>() != null)
                return AvatarType.SDK3;
#endif
            return AvatarType.DEFAULT;
        }

        public static void UpdateAvatarSDK(GameObject sourceAvatar, AnimatorOverrideController standController = null, AnimatorOverrideController sitController = null)
        {
            ClearConsole();
            // 检查模型
            var avatarType = GetAvatarType(sourceAvatar);
            switch (avatarType)
            {
                case AvatarType.NONE:
                    EditorUtility.DisplayDialog("提醒", "请先选择您的模型！", "确认");
                    return;
                case AvatarType.SDK3:
                    EditorUtility.DisplayDialog("提醒", "已是SDK3模型了，不需要转换！", "确认");
                    return;
            }

            // 克隆模型，并隐藏旧模型
            GameObject newAvatar;
            {
                newAvatar = Instantiate(sourceAvatar, sourceAvatar.transform.localPosition, Quaternion.identity);
                var isPrefabFile = PrefabUtility.IsPartOfPrefabAsset(sourceAvatar);
                if (!isPrefabFile)
                    sourceAvatar.SetActive(false);
                newAvatar.SetActive(true);
                newAvatar.name = sourceAvatar.name + "_sdk3";
            }

            // 获取旧AvatarDescriptor信息
            var oldDescriptor = newAvatar.GetComponent<VRCSDK2.VRC_AvatarDescriptor>();

            // 添加新AvatarDescriptor
            var descriptor = newAvatar.AddComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();

            var boneMap = GetHumanBoneFromName(newAvatar, new string[] { "LeftEye", "RightEye" });

            // ViewPosition
            if (oldDescriptor != null)
            {
                descriptor.ViewPosition = oldDescriptor.ViewPosition;
            }
            else if (boneMap["LeftEye"] != null)
            {
                var lPos = boneMap["LeftEye"].transform.position;
                descriptor.ViewPosition = new Vector3(0, lPos.y, Mathf.Abs(lPos.z * 1.2f));
            }
            else
            {
                Print("无法判断视角球位置，请手动进行调整！");
            }

            // lipSync
            /*{
                var succ = true;
                if (oldDescriptor != null && oldDescriptor.VisemeSkinnedMesh != null)
                {
                    descriptor.VisemeSkinnedMesh = oldDescriptor.VisemeSkinnedMesh;
                }
                else
                {
                    var map = GetGameObjectForName(newAvatar, new string[] { "Body", "body" });
                    if (map.Count > 0)
                        descriptor.VisemeSkinnedMesh = (map["Body"] ?? map["body"]).GetComponent<SkinnedMeshRenderer>();
                    else
                        succ = false;
                }
                if (succ)
                {
                    descriptor.lipSync = VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape;
                    descriptor.VisemeBlendShapes = visemeBlendShapes;
                }
                else
                {
                    descriptor.lipSync = VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.Default;
                    Debug.LogWarning("因无法获取模型的Body元素，已使用默认参数，请仔细检查模型的LipSync参数！");
                }
            }*/
            AutoDetectLipSync(descriptor);

            // Eye Look 无合适的判断方法，已弃用
            Print("如果需要使用眼睛追踪，请手动设置模型的Eye Look选项.");
            /*if (boneMap["LeftEye"] != null && boneMap["RightEye"] != null)
            {
                var leftEyeTran = boneMap["LeftEye"].transform;
                var rightEyeTran = boneMap["RightEye"].transform;

                descriptor.enableEyeLook = true;
                var customEyeLookSettings = descriptor.customEyeLookSettings;
                customEyeLookSettings.leftEye = leftEyeTran;
                customEyeLookSettings.rightEye = rightEyeTran;

                var eyes = new Transform[] { leftEyeTran, rightEyeTran };
                customEyeLookSettings.eyesLookingStraight = GetEyeRotations(eyes, 0, 0, 0);
                customEyeLookSettings.eyesLookingUp = GetEyeRotations(eyes, -10, 0, 0);
                customEyeLookSettings.eyesLookingDown = GetEyeRotations(eyes, 10, 0, 0);
                customEyeLookSettings.eyesLookingLeft = GetEyeRotations(eyes, 0, -15, 0);
                customEyeLookSettings.eyesLookingRight = GetEyeRotations(eyes, 0, 15, 0);
                
                // customEyeLookSettings.eyelidType = EyelidType.Blendshapes;
                // customEyeLookSettings.eyelidsSkinnedMesh = map["Body"].GetComponent<SkinnedMeshRenderer>();
                
                descriptor.customEyeLookSettings = customEyeLookSettings;
            }*/


            // 分配模型ID，定义配置存放目录
            var avatarId = GetOrCreateAvatarId(newAvatar);
            var avatarPath = "Assets/AvatarData/" + avatarId + "/";

            // 复制所需文件
            {
                var defintePath = GetAssetsPath("Assets/SDK3");
                CopyFolder(defintePath, avatarPath);
                AssetDatabase.Refresh();
            }

            // 配置AvatarDescriptor PlayableLayers
            {
                descriptor.customizeAnimationLayers = true;
                var baseAnimationLayerType = new int[] { 0, 2, 3, 4, 5 };
                var baseAnimationLayers = new CustomAnimLayer[baseAnimationLayerType.Length];
                for (var i = 0; i < baseAnimationLayers.Length; i++)
                {
                    var type = (AnimLayerType)baseAnimationLayerType[i];
                    baseAnimationLayers[i].type = type;
                    string path;
                    switch (type)
                    {
                        case AnimLayerType.Base:
                            path = avatarPath + "Controller/Locomotion.controller";
                            break;
                        case AnimLayerType.Additive:
                            path = avatarPath + "Controller/Idle.controller";
                            break;
                        case AnimLayerType.Gesture:
                            path = avatarPath + "Controller/HandsLayer.controller";
                            break;
                        case AnimLayerType.Action:
                            path = avatarPath + "Controller/Action.controller";
                            break;
                        case AnimLayerType.FX:
                            path = avatarPath + "Controller/FXLayer.controller";
                            break;
                        default:
                            return;
                    }
                    var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
                    if (controller == null)
                    {
                        Debug.LogError("资源不存在：" + path);
                        baseAnimationLayers[i].isDefault = true;
                    }
                    else
                    {
                        baseAnimationLayers[i].animatorController = controller;
                        baseAnimationLayers[i].isDefault = false;
                    }
                }
                descriptor.baseAnimationLayers = baseAnimationLayers;
            }
            {
                var specialAnimationLayerType = new int[] { 6, 7, 8 };
                var specialAnimationLayers = new CustomAnimLayer[specialAnimationLayerType.Length];
                for (var i = 0; i < specialAnimationLayers.Length; i++)
                {
                    var type = (AnimLayerType)specialAnimationLayerType[i];
                    specialAnimationLayers[i].type = type;
                    string path = avatarPath + "Controller/" + type + ".controller";

                    var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
                    if (controller == null)
                    {
                        Debug.LogError("资源不存在：" + path);
                        specialAnimationLayers[i].isDefault = true;
                    }
                    else
                    {
                        specialAnimationLayers[i].animatorController = controller;
                        specialAnimationLayers[i].isDefault = false;
                    }
                }
                descriptor.specialAnimationLayers = specialAnimationLayers;
            }

            // 查找动作姿态
            //var clips = oldSitController.animationClips;
            var animMap = new Dictionary<string, AnimationClip>();
            var list = new List<KeyValuePair<AnimationClip, AnimationClip>>();

            if (standController)
            {
                standController.GetOverrides(list);
                foreach (var item in list)
                {
                    var key = item.Key.name;
                    var val = item.Value;
                    if (val != null)
                    {
                        animMap.Add(key, val);
                    }
                }
            }
            if (sitController)
            {
                sitController.GetOverrides(list);
                foreach (var item in list)
                {
                    var key = item.Key.name;
                    var val = item.Value;
                    if (key.Equals("IDLE") && val != null)
                        animMap.Add("SITTING", val);
                }
            }

            // Standing
            var standing = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.BlendTree>(avatarPath + "BlendTree/Standing.asset");
            var children = standing.children;
            if (animMap.ContainsKey("IDLE"))
                children[3].motion = animMap["IDLE"];
            standing.children = children;

            // Locomotion
            var locomotion = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(avatarPath + "Controller/Locomotion.controller");
            foreach (var layer in locomotion.layers)
                if (layer.name.Equals("Locomotion"))
                    foreach (var val in layer.stateMachine.states)
                        if (val.state.name.Equals("Standing"))
                            val.state.motion = standing;

            // HandsLayer
            var clipList = new List<string>();
            clipList.AddRange(new string[] { "Fist", "Open", "Point", "Peace", "RockNRoll", "Gun", "Thumbs up" });
            var clipKeys = new string[] { "FIST", "HANDOPEN", "FINGERPOINT", "VICTORY", "ROCKNROLL", "HANDGUN", "THUMBSUP" };
            var handsLayer = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(avatarPath + "Controller/HandsLayer.controller");
            foreach (var layer in handsLayer.layers)
            {
                if (layer.name.Equals("Left Hand") || layer.name.Equals("Right Hand"))
                {
                    foreach (var val in layer.stateMachine.states)
                    {
                        var index = clipList.IndexOf(val.state.name);
                        if (index >= 0 && animMap.ContainsKey(clipKeys[index]))
                        {
                            val.state.motion = animMap[clipKeys[index]];
                        }
                    }
                }
            }

            // FXLayer
            var fxLayer = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(avatarPath + "Controller/FXLayer.controller");
            foreach (var layer in fxLayer.layers)
            {
                if (layer.name.Equals("Left Hand Facial") || layer.name.Equals("Right Hand Facial"))
                {
                    foreach (var val in layer.stateMachine.states)
                    {
                        var index = clipList.IndexOf(val.state.name);
                        if (index >= 0 && animMap.ContainsKey(clipKeys[index]))
                        {
                            val.state.motion = animMap[clipKeys[index]];
                        }
                    }
                }
            }

            // Idle
            if (animMap.ContainsKey("IDLE"))
            {
                var idleLayer = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(avatarPath + "Controller/Idle.controller");
                foreach (var layer in idleLayer.layers)
                {
                    if (layer.name.Equals("Idle"))
                    {
                        foreach (var val in layer.stateMachine.states)
                        {
                            if (val.state.name.Equals("Upright Idle"))
                            {
                                val.state.motion = animMap["IDLE"];
                            }
                        }
                    }
                }
            }

            // Sitting
            if (animMap.ContainsKey("SITTING"))
            {
                var sittingLayer = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(avatarPath + "Controller/Sitting.controller");
                foreach (var layer in sittingLayer.layers)
                {
                    if (layer.name.Equals("Sitting"))
                    {
                        foreach (var val in layer.stateMachine.states)
                        {
                            var name = val.state.name;
                            if (name.Equals("WaitForSit") || name.Equals("SittingPose") || name.Equals("RestoreTracking"))
                            {
                                val.state.motion = animMap["SITTING"];
                            }
                        }
                    }
                }
            }

            // 配置AvatarDescriptor Expression
            var expressionsMenu = AssetDatabase.LoadAssetAtPath<VRCExpressionsMenu>(avatarPath + "Asset/Menu.asset");
            var expressionParameters = AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(avatarPath + "Asset/Parameters.asset");
            if (expressionsMenu != null && expressionParameters != null)
            {
                descriptor.customExpressions = true;
                descriptor.expressionsMenu = expressionsMenu;
                descriptor.expressionParameters = expressionParameters;
            }

            // 移除旧AvatarDescriptor
            if (oldDescriptor != null)
                DestroyImmediate(oldDescriptor);

            Print("转换成功！");
            EditorUtility.DisplayDialog("提醒", "转换成功！\r\n新模型名字：" + newAvatar.name + "\r\n请检查各项参数是否正常", "确认");
            EditorGUIUtility.PingObject(newAvatar);
            Selection.activeGameObject = newAvatar;
        }

        /*private static EyeRotations GetEyeRotations(Transform[] eyes, float x, float y, float z)
        {
            var eyesLooking = new EyeRotations
            {
                linked = false,
                left = Quaternion.Euler(eyes[0].localEulerAngles + new Vector3(x, y, z)),
                right = Quaternion.Euler(eyes[1].localEulerAngles + new Vector3(x, y, z))
            };
            return eyesLooking;
        }*/


        private static void AutoDetectLipSync(VRC.SDK3.Avatars.Components.VRCAvatarDescriptor avatarDescriptor)
        {
            SkinnedMeshRenderer[] renderers = avatarDescriptor.GetComponentsInChildren<SkinnedMeshRenderer>();

            string[] baseVisemeNames = Enum.GetNames(typeof(VRC.SDKBase.VRC_AvatarDescriptor.Viseme));
            int visemeCount = baseVisemeNames.Length - 1;
            string[] reversedVisemeNames = new string[visemeCount];
            string[] reversedVVisemeNames = new string[visemeCount];
            for (int i = 0; i < visemeCount; i++)
            {
                string visemeName = baseVisemeNames[i];
                char[] tmpArray = visemeName.ToLowerInvariant().ToCharArray();
                Array.Reverse(tmpArray);
                reversedVisemeNames[i] = new string(tmpArray);
                reversedVVisemeNames[i] = $"{reversedVisemeNames[i]}_v";
            }

            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                if (renderer.sharedMesh.blendShapeCount <= 0) continue;

                if (renderer.sharedMesh.blendShapeCount >= visemeCount)
                {
                    string[] rendererBlendShapeNames = new string[renderer.sharedMesh.blendShapeCount];
                    for (int i = 0; i < renderer.sharedMesh.blendShapeCount; i++)
                    {
                        rendererBlendShapeNames[i] = renderer.sharedMesh.GetBlendShapeName(i);
                    }

                    string[] visemeStrings = new string[visemeCount];
                    int foundVisemes = 0;

                    string[] reversedRendererNames = new string[rendererBlendShapeNames.Length];
                    Dictionary<string, string> reverseMap = new Dictionary<string, string>();

                    for (int i = 0; i < rendererBlendShapeNames.Length; i++)
                    {
                        string rendererBlendShapeName = rendererBlendShapeNames[i];
                        char[] tmpArray = rendererBlendShapeName.ToLowerInvariant().ToCharArray();
                        Array.Reverse(tmpArray);
                        reversedRendererNames[i] = new string(tmpArray);
                        if (reverseMap.ContainsKey(reversedRendererNames[i]))
                        {
                            continue;
                        }
                        reverseMap.Add(reversedRendererNames[i], rendererBlendShapeName);
                    }

                    for (int i = 0; i < reversedVisemeNames.Length; i++)
                    {
                        string visemeName = reversedVisemeNames[i];
                        string vVisemeName = reversedVVisemeNames[i];

                        List<string> matchingStrings = new List<string>();
                        foreach (string reversedRendererName in reversedRendererNames)
                        {
                            if (reversedRendererName.Contains(vVisemeName))
                            {
                                matchingStrings.Add(reversedRendererName);
                            }
                        }
                        if (matchingStrings.Count == 0)
                        {
                            foreach (string reversedRendererName in reversedRendererNames)
                            {
                                if (reversedRendererName.Contains(visemeName))
                                {
                                    matchingStrings.Add(reversedRendererName);
                                }
                            }
                        }

                        matchingStrings.Sort(new SearchComparer(visemeName));

                        if (matchingStrings.Count <= 0) continue;

                        visemeStrings[i] = reverseMap[matchingStrings[0]];
                        foundVisemes++;
                    }

                    //Threshold to see if we did a good enough job to bother showing the user
                    if (foundVisemes > 2)
                    {
                        avatarDescriptor.lipSync = VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape;
                        avatarDescriptor.VisemeSkinnedMesh = renderer;
                        avatarDescriptor.VisemeBlendShapes = visemeStrings;
                        avatarDescriptor.lipSyncJawBone = null;
                        return;
                    }
                }

                avatarDescriptor.lipSync = VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape;
                avatarDescriptor.VisemeSkinnedMesh = renderer;
                avatarDescriptor.lipSyncJawBone = null;
                return;
            }


            if (avatarDescriptor.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Jaw) == null) return;
            avatarDescriptor.lipSync = VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.JawFlapBone;
            avatarDescriptor.lipSyncJawBone = avatarDescriptor.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Jaw);
            avatarDescriptor.VisemeSkinnedMesh = null;
        }
    }
    class SearchComparer : IComparer<string>
    {
        string ReplaceFirst(string text, string search, string replace)
        {
            int pos = text.IndexOf(search, StringComparison.Ordinal);
            if (pos < 0)
            {
                return text;
            }
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }

        public SearchComparer(string searchString)
        {
            _searchString = searchString;
        }
        private readonly string _searchString;
        public int Compare(string x, string y)
        {
            if (x == null || y == null)
            {
                return 0;
            }
            //-1 is they're out of order, 0 is order doesn't matter, 1 is they're in order

            x = ReplaceFirst(x, "const ", "");
            y = ReplaceFirst(y, "const ", "");

            int xIndex = x.IndexOf(_searchString, StringComparison.InvariantCultureIgnoreCase);
            int yIndex = y.IndexOf(_searchString, StringComparison.InvariantCultureIgnoreCase);
            int compareIndex = xIndex.CompareTo(yIndex);
            if (compareIndex != 0) return compareIndex;

            string xDiff = ReplaceFirst(x, _searchString, "");
            string yDiff = ReplaceFirst(y, _searchString, "");
            return string.Compare(xDiff, yDiff, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
#endif