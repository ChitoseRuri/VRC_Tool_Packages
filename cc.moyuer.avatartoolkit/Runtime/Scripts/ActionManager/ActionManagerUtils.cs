#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;
using static VRC.SDK3.Avatars.Components.VRCAvatarDescriptor;
using static VRChatAvatarToolkit.MoyuToolkitUtils;

namespace VRChatAvatarToolkit
{
    public class ActionManagerUtils : EditorWindow
    {

        [System.Serializable]
        public class ActionItemInfo
        {
            public string name;
            public string type;
            public Texture2D image;
            public AnimBool animBool = new AnimBool { speed = 3.0f };
            public AnimationClip animation;
            public AudioClip audio;
            public bool autoExit = true;
            public ActionItemInfo(string _name = "新动作", string _type = "")
            {
                name = _name;
                type = _type;
            }
            public ActionItemInfo(ActionManagerParameter.ActionInfo info)
            {
                name = info.name;
                type = info.type;
                image = info.menuImage;
                animation = info.animation;
                audio = info.audio;
                autoExit = info.autoExit;
            }
        }

        // 创建衣柜参数文件
        internal static ActionManagerParameter CreateActionManagerParameter(GameObject avatar)
        {
            if (avatar == null)
                return null;
            var parameter = CreateInstance<ActionManagerParameter>();
            var avatarId = GetOrCreateAvatarId(avatar);
            parameter.avatarId = avatarId;
            parameter.actionList.Add(new ActionManagerParameter.ActionInfo { name = "动作1", type = "" });
            var dir = GetParameterDirPath(avatarId, "");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            AssetDatabase.CreateAsset(parameter, GetParameterDirPath(avatarId, "ActionManagerParameter.asset"));
            return parameter;
        }
        // 获取衣柜参数文件
        internal static ActionManagerParameter GetActionManagerParameter(string avatarId)
        {
            if (avatarId == null)
                return null;
            var path = GetParameterDirPath(avatarId, "/ActionManagerParameter.asset");
            if (File.Exists(path))
            {
                var parameter = AssetDatabase.LoadAssetAtPath(path, typeof(ActionManagerParameter)) as ActionManagerParameter;
                return parameter;
            }
            return null;
        }

        // 获取该模型的参数文件存放位置
        internal static string GetParameterDirPath(string avatarId, string path)
        {
            return "Assets/AvatarData/" + avatarId + "/" + path;
        }

        // 通过检测type字段，判断是否为分类模式
        internal static bool HasClassify(List<ActionManagerParameter.ActionInfo> list)
        {
            foreach (var item in list)
                if (item.type != null && item.type.Length > 0)
                    return true;
            return false;
        }
        // 通过检测type字段，判断是否为分类模式
        internal static bool HasClassify(List<ActionItemInfo> list)
        {
            foreach (var item in list)
                if (item.type != null && item.type.Length > 0)
                    return true;
            return false;
        }

        // 应用到模型
        internal static void ApplyToAvatar(GameObject avatar, ActionManagerParameter parameter, bool autoLock = false)
        {
            ClearConsole();
            var actionList = new List<ActionManagerParameter.ActionInfo>();
            var _actionList = parameter.actionList;
            foreach (var info in _actionList)
            {
                if (info.animation == null) continue;
                actionList.Add(info);
            }

            var avatarId = GetAvatarId(avatar);
            var dirPath = GetParameterDirPath(avatarId, "");
            var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
            var expressionParameters = descriptor.expressionParameters;
            var expressionsMenu = descriptor.expressionsMenu;

            /*** 添加Audio Source组件 ***/
            {
                var audioTransform = avatar.transform.Find("Audio");
                if (audioTransform != null)
                    DestroyImmediate(audioTransform.gameObject);
                audioTransform = new GameObject("Audio").transform;
                audioTransform.parent = avatar.transform;
                foreach (var info in actionList)
                {
                    if (info.audio == null) continue;
                    var obj = new GameObject(info.name);
                    obj.SetActive(false);
                    obj.transform.parent = audioTransform;
                    var audio = obj.AddComponent<AudioSource>();
                    audio.clip = info.audio;
                    if (parameter.audio3D)
                    {
                        audio.spatialBlend = 1;
                        audio.rolloffMode = AudioRolloffMode.Linear;
                        audio.minDistance = parameter.audioMinDistance;
                        audio.maxDistance = parameter.audioMaxDistance;
                    }
                }
            }

            /*** 输出Anim文件 ***/
            var fxAnimMap = new Dictionary<string, AnimationClip>();
            var actionAnimMap = new Dictionary<string, AnimationClip>();
            {
                var animDir = dirPath + "Anim/ActionManager";
                if (Directory.Exists(animDir))
                    Directory.Delete(animDir, true);
                Directory.CreateDirectory(animDir);
                animDir += "/";
                var curve0 = new AnimationCurve(new Keyframe[] { new Keyframe(0, 0) });
                var curve1 = new AnimationCurve(new Keyframe[] { new Keyframe(0, 1) });
                {
                    AnimationClip clip = new AnimationClip { name = "stop" };
                    foreach (var info2 in actionList)
                    {
                        if (info2.audio == null) continue;
                        EditorCurveBinding bind = new EditorCurveBinding
                        {
                            path = "Audio/" + info2.name,
                            propertyName = "m_IsActive",
                            type = typeof(GameObject)
                        };
                        AnimationUtility.SetEditorCurve(clip, bind, curve0);
                    }
                    AssetDatabase.CreateAsset(clip, animDir + "Audio_Stop.anim");
                    fxAnimMap.Add("_Stop_", clip);
                }
                foreach (var info in actionList)
                {
                    var sourceClip = info.animation;
                    var frameRate = sourceClip.frameRate;
                    var fxClip = new AnimationClip { name = info.name, frameRate = frameRate };
                    var actionClip = new AnimationClip { name = info.name, frameRate = frameRate };
                    // Audio
                    foreach (var info2 in actionList)
                    {
                        if (info2.audio == null) continue;
                        var flag = info2.name == info.name;
                        EditorCurveBinding bind = new EditorCurveBinding
                        {
                            path = "Audio/" + info2.name,
                            propertyName = "m_IsActive",
                            type = typeof(GameObject)
                        };
                        AnimationUtility.SetEditorCurve(fxClip, bind, flag ? curve1 : curve0);
                    }
                    // Morph & Action
                    var binds = AnimationUtility.GetCurveBindings(sourceClip);
                    var lastFrameTime = info.audio != null ? info.audio.length : 0;
                    if (lastFrameTime < sourceClip.length) lastFrameTime = sourceClip.length;
                    lastFrameTime = (int)(lastFrameTime + 0.5f) + 3;
                    for (var i = 0; i < binds.Length; i++)
                    {
                        var bind = binds[i];
                        var curve = AnimationUtility.GetEditorCurve(sourceClip, binds[i]);
                        // 将动画时长调整到与音乐一致或更长
                        var lastKey = curve.keys[curve.keys.Length - 1];
                        if (lastKey.time < lastFrameTime)
                            curve.AddKey(lastFrameTime, lastKey.value);
                        // 分类
                        var isBlendShape = bind.propertyName.StartsWith("blendShape.");
                        AnimationUtility.SetEditorCurve(isBlendShape ? fxClip : actionClip, bind, curve);
                    }
                    // 输出
                    var sourceClipSetting = AnimationUtility.GetAnimationClipSettings(sourceClip);
                    var actionClipSetting = AnimationUtility.GetAnimationClipSettings(actionClip);
                    actionClipSetting.loopTime = false;
                    actionClipSetting.orientationOffsetY = sourceClipSetting.orientationOffsetY;
                    actionClipSetting.loopBlendOrientation = sourceClipSetting.loopBlendOrientation;
                    actionClipSetting.loopBlendPositionY = sourceClipSetting.loopBlendPositionY;
                    actionClipSetting.loopBlendPositionXZ = sourceClipSetting.loopBlendPositionXZ;
                    actionClipSetting.stopTime = lastFrameTime;
                    AnimationUtility.SetAnimationClipSettings(actionClip, actionClipSetting);

                    AssetDatabase.CreateAsset(fxClip, animDir + "Audio_Morph_" + info.name + ".anim");
                    AssetDatabase.CreateAsset(actionClip, animDir + "Action_" + info.name + ".anim");
                    fxAnimMap.Add(info.name, fxClip);
                    actionAnimMap.Add(info.name, actionClip);
                }
                AssetDatabase.Refresh();
            }

            /*** 准备动画控制器 ***/
            AnimatorController baseController = null;
            AnimatorController actionController = null;
            AnimatorController fxController = null;
            {
                descriptor.customizeAnimationLayers = true;
                descriptor.customExpressions = true;
                var baseAnimationLayers = descriptor.baseAnimationLayers;
                for (var i = 0; i < baseAnimationLayers.Length; i++)
                {
                    var layer = descriptor.baseAnimationLayers[i];
                    if (layer.type == AnimLayerType.Base || layer.type == AnimLayerType.Action || layer.type == AnimLayerType.FX)
                    {
                        AnimatorController controller;
                        if (layer.isDefault || layer.animatorController == null)
                        {
                            var filePath = dirPath;
                            var templateName = GetAssetsPath("Assets/SDK3/Controller/");
                            switch (layer.type)
                            {
                                case AnimLayerType.Base:
                                    filePath += "Locomotion.controller";
                                    templateName += "Locomotion.controller";
                                    break;
                                case AnimLayerType.Action:
                                    filePath += "Action.controller";
                                    templateName += "Action.controller";
                                    break;
                                case AnimLayerType.FX:
                                    filePath += "FXLayer.controller";
                                    templateName += "FXLayer.controller";
                                    break;
                            }
                            if (!File.Exists(filePath))
                            {
                                File.Copy(templateName, filePath);
                                AssetDatabase.Refresh();
                            }
                            controller = AssetDatabase.LoadAssetAtPath(filePath, typeof(AnimatorController)) as AnimatorController;

                            layer.isDefault = false;
                            layer.isEnabled = true;
                            layer.animatorController = controller;
                            baseAnimationLayers[i] = layer;
                        }
                        else
                        {
                            controller = layer.animatorController as AnimatorController;
                        }
                        // 添加必备参数
                        if (layer.type != AnimLayerType.FX)
                            AddControllerParameter(controller, "Action_Int1", AnimatorControllerParameterType.Int);
                        if (layer.type != AnimLayerType.Base)
                            AddControllerParameter(controller, "Action_Int2", AnimatorControllerParameterType.Int);

                        // 设置到变量
                        switch (layer.type)
                        {
                            case AnimLayerType.Base:
                                baseController = layer.animatorController as AnimatorController;
                                break;
                            case AnimLayerType.Action:
                                actionController = layer.animatorController as AnimatorController;
                                break;
                            case AnimLayerType.FX:
                                fxController = layer.animatorController as AnimatorController;
                                break;
                        }
                    }
                }
                descriptor.baseAnimationLayers = baseAnimationLayers;
                if (baseController == null || actionController == null || fxController == null)
                {
                    EditorUtility.DisplayDialog("错误", "发生意料之外的情况，请重新设置 AvatarDescriptor 中的 Playable Layers 后再试！", "确认");
                    return;
                }
                AddControllerParameter(actionController, "Seated", AnimatorControllerParameterType.Bool);
            }

            /*** 调整Base动画控制器 - 避免跳跃时发生异常 ***/
            {
                for (var a = 0; a < baseController.layers.Length; a++)
                {
                    if (baseController.layers[a].name != "Locomotion") continue;
                    var layer = baseController.layers[a];
                    var stateMachine = layer.stateMachine;
                    for (var b = 0; b < stateMachine.states.Length; b++)
                    {
                        var state = stateMachine.states[b].state;
                        if (state.name != "Standing") continue;
                        var nameList = new List<string> { "SmallHop", "Short Fall", "LongFall" };
                        for (int c = 0; c < state.transitions.Length; c++)
                        {
                            var transition = state.transitions[c];
                            if (!nameList.Contains(transition.destinationState.name)) continue;
                            for (int d = 0; d < transition.conditions.Length; d++)
                                if (transition.conditions[d].parameter == "Action_Int1")
                                {
                                    transition.RemoveCondition(transition.conditions[d]);
                                    d--;
                                }
                            transition.AddCondition(AnimatorConditionMode.Equals, 0, "Action_Int1");
                        }
                    }
                    break;
                }
            }

            /*** 调整Action动画控制器 - 舞蹈动作 ***/
            {
                // 删除旧Layer
                for (var i = 0; i < actionController.layers.Length; i++)
                {
                    var layer = actionController.layers[i];
                    if (layer.name.StartsWith("ActionLayer"))
                    {
                        actionController.RemoveLayer(i);
                        i--;
                    }
                    else if (layer.name == "Action")
                    {
                        foreach (var childState in layer.stateMachine.states)
                            foreach (var transition in childState.state.transitions)
                                if (transition.destinationState != null && transition.destinationState.name == "Afk Init")
                                {
                                    for (int a = 0; a < transition.conditions.Length; a++)
                                        if (transition.conditions[a].parameter == "Action_Int1")
                                        {
                                            transition.RemoveCondition(transition.conditions[a]);
                                            a--;
                                        }
                                    transition.AddCondition(AnimatorConditionMode.Equals, 0, "Action_Int1");
                                }
                    }
                }
                // 添加新StateMachine
                var stateMachine = new AnimatorStateMachine()
                {
                    name = "ActionLayer",
                    hideFlags = HideFlags.HideInHierarchy
                };
                AssetDatabase.AddObjectToAsset(stateMachine, AssetDatabase.GetAssetPath(actionController));

                var waitState = stateMachine.AddState("Wait");
                stateMachine.defaultState = waitState;
                // 准备State
                var readyState = stateMachine.AddState("Ready");
                var readyBehaviour_layer = readyState.AddStateMachineBehaviour<VRCPlayableLayerControl>();
                readyBehaviour_layer.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                readyBehaviour_layer.goalWeight = 1;
                var readyBehaviour_track = readyState.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
                SetAnimatorTrackingControl(readyBehaviour_track, VRC_AnimatorTrackingControl.TrackingType.Animation);
                var readyTran = waitState.AddTransition(readyState);
                readyTran.AddCondition(AnimatorConditionMode.NotEqual, 0, "Action_Int1");
                // 结束
                var endState = stateMachine.AddState("End");
                // 播放完毕
                var overState = stateMachine.AddState("Over");
                overState.AddTransition(endState).hasExitTime = true;
                // 坐下时自动停止
                var seatedTran = stateMachine.AddAnyStateTransition(overState);
                seatedTran.AddCondition(AnimatorConditionMode.If, 1, "Seated");
                seatedTran.AddCondition(AnimatorConditionMode.NotEqual, 0, "Action_Int1");

                if (autoLock)
                {
                    var readyBehaviour_lc = readyState.AddStateMachineBehaviour<VRCAnimatorLocomotionControl>();
                    readyBehaviour_lc.disableLocomotion = true;
                    var endSBehaviour_lc = endState.AddStateMachineBehaviour<VRCAnimatorLocomotionControl>();
                    endSBehaviour_lc.disableLocomotion = false;
                }
                // 动作清单
                foreach (var info in actionList)
                {
                    var mValue = actionList.IndexOf(info) + 1;
                    var state = stateMachine.AddState(info.name);
                    state.motion = actionAnimMap[info.name];
                    var tran = readyState.AddTransition(state);
                    tran.duration = 0.5f;
                    tran.AddCondition(AnimatorConditionMode.Equals, mValue, "Action_Int1");
                    // 播放完毕
                    if (info.autoExit)
                    {
                        var overTran = state.AddTransition(overState);
                        overTran.duration = 0.5f;
                        overTran.hasExitTime = true;
                        overTran.exitTime = 1;
                    }
                    // 中途跳出
                    var endTran = state.AddTransition(endState);
                    endTran.AddCondition(AnimatorConditionMode.NotEqual, mValue, "Action_Int1");
                    // 同步播放音乐
                    var playBehaviour_parameter = state.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                    playBehaviour_parameter.parameters.Add(new VRC_AvatarParameterDriver.Parameter() { name = "Action_Int2", value = mValue });

                }
                // 播放完毕State
                var overBehaviour_parameter = overState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                overBehaviour_parameter.parameters.Add(new VRC_AvatarParameterDriver.Parameter() { name = "Action_Int1", value = 0 });

                // 结束State
                var endBehaviour_layer = endState.AddStateMachineBehaviour<VRCPlayableLayerControl>();
                endBehaviour_layer.layer = VRC_PlayableLayerControl.BlendableLayer.Action;
                endBehaviour_layer.goalWeight = 0;
                var endBehaviour_track = endState.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
                SetAnimatorTrackingControl(endBehaviour_track, VRC_AnimatorTrackingControl.TrackingType.Tracking);
                var endBehaviour_parameter = endState.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                endBehaviour_parameter.parameters.Add(new VRC_AvatarParameterDriver.Parameter() { name = "Action_Int2", value = 0 });
                var endTransition = endState.AddExitTransition();
                endTransition.hasExitTime = true;
                endTransition.exitTime = 0;

                actionController.AddLayer(new AnimatorControllerLayer
                {
                    name = stateMachine.name,
                    defaultWeight = 1f,
                    stateMachine = stateMachine
                });
            }

            /*** 调整FXLayer动画控制器 - 音乐&表情 ***/
            {
                // 预处理Layer
                var fxLayerWeightMap = new Dictionary<int, float>();
                var nameList = new List<string> { "facial", "hand", "left", "right" };
                for (var i = 0; i < fxController.layers.Length; i++)
                {
                    var layer = fxController.layers[i];
                    var layName = layer.name;
                    if (layName.StartsWith("ActionLayer"))
                    {
                        fxController.RemoveLayer(i);
                        i--;
                    }
                    else if (layer.defaultWeight > 0)
                    {
                        var flag = false;
                        foreach (var name in nameList)
                        {
                            if (layName.ToLower().Contains(name))
                            {
                                flag = true;
                                break;
                            }
                        }
                        if (flag) fxLayerWeightMap.Add(i, layer.defaultWeight);
                    }
                }
                // 添加新StateMachine
                var stateMachine = new AnimatorStateMachine()
                {
                    name = "ActionLayer",
                    hideFlags = HideFlags.HideInHierarchy
                };
                AssetDatabase.AddObjectToAsset(stateMachine, AssetDatabase.GetAssetPath(fxController));

                // 添加基本状态
                var waitState = stateMachine.AddState("Wait");
                stateMachine.defaultState = waitState;
                /* var readyState = stateMachine.AddState("Ready");
                foreach (var pair in fxLayerWeightMap)
                {
                    var layerControl = readyState.AddStateMachineBehaviour<VRCAnimatorLayerControl>();
                    layerControl.playable = VRC_AnimatorLayerControl.BlendableLayer.FX;
                    layerControl.layer = pair.Key;
                    layerControl.goalWeight = 0;
                }
                var waitTransition = waitState.AddTransition(readyState);
                waitTransition.hasExitTime = true;
                waitTransition.exitTime = 0;
                waitTransition.duration = 0;
                waitTransition.AddCondition(AnimatorConditionMode.NotEqual, 0, "Action_Int2");
                */

                var exitState = stateMachine.AddState("Exit");
                exitState.motion = fxAnimMap["_Stop_"];
                exitState.AddExitTransition().hasExitTime = true;
                foreach (var pair in fxLayerWeightMap)
                {
                    var layerControl = exitState.AddStateMachineBehaviour<VRCAnimatorLayerControl>();
                    layerControl.playable = VRC_AnimatorLayerControl.BlendableLayer.FX;
                    layerControl.layer = pair.Key;
                    layerControl.goalWeight = pair.Value;
                }

                // 添加动作状态
                foreach (var info in actionList)
                {
                    var state = stateMachine.AddState(info.name);
                    if (fxAnimMap.ContainsKey(info.name))
                        state.motion = fxAnimMap[info.name];

                    //var inTran = readyState.AddTransition(state);
                    var inTran = waitState.AddTransition(state);
                    inTran.AddCondition(AnimatorConditionMode.Equals, actionList.IndexOf(info) + 1, "Action_Int2");
                    inTran.duration = 0f;
                    state.AddTransition(exitState).AddCondition(AnimatorConditionMode.NotEqual, actionList.IndexOf(info) + 1, "Action_Int2");


                    foreach (var pair in fxLayerWeightMap)
                    {
                        var layerControl = state.AddStateMachineBehaviour<VRCAnimatorLayerControl>();
                        layerControl.playable = VRC_AnimatorLayerControl.BlendableLayer.FX;
                        layerControl.layer = pair.Key;
                        layerControl.goalWeight = 0;
                    }
                }

                // 添加新Layer
                fxController.AddLayer(new AnimatorControllerLayer
                {
                    name = stateMachine.name,
                    defaultWeight = 1f,
                    stateMachine = stateMachine
                });
            }

            /*** 配置VRCExpressionParameters ***/
            {
                if (expressionParameters == null || expressionParameters.parameters == null)
                {
                    expressionParameters = CreateInstance<VRCExpressionParameters>();
                    var parameterTemplate = AssetDatabase.LoadAssetAtPath(GetAssetsPath("Assets/SDK3/Asset/Parameters.asset"),
                        typeof(VRCExpressionParameters)) as VRCExpressionParameters;
                    expressionParameters.parameters = parameterTemplate.parameters;
                    AssetDatabase.CreateAsset(expressionParameters, dirPath + "ExpressionParameters.asset");
                }
                var parameters = expressionParameters.parameters;
                var newParameters = new List<VRCExpressionParameters.Parameter>();
                foreach (var par in parameters)
                    if (!par.name.StartsWith("Action_") && par.name != "")
                        newParameters.Add(par);
                newParameters.Add(new VRCExpressionParameters.Parameter
                {
                    name = "Action_Int1",
                    valueType = VRCExpressionParameters.ValueType.Int,
                    defaultValue = 0,
                    saved = true
                });
                newParameters.Add(new VRCExpressionParameters.Parameter
                {
                    name = "Action_Int2",
                    valueType = VRCExpressionParameters.ValueType.Int,
                    defaultValue = 0,
                    saved = true
                });
                expressionParameters.parameters = newParameters.ToArray();
            }

            /*** 配置VRCExpressionsMenu ***/
            {
                // 创建新Menu文件夹
                var menuDir = dirPath + "Menu/ActionManager";
                if (Directory.Exists(menuDir))
                    Directory.Delete(menuDir, true);
                Directory.CreateDirectory(menuDir);
                menuDir += "/";

                /*** 生成动作菜单 ***/
                var mainActionMenu = CreateInstance<VRCExpressionsMenu>();
                {
                    var hasClassify = HasClassify(actionList);
                    // 归类
                    var actionTypeMap = new Dictionary<string, List<ActionManagerParameter.ActionInfo>>();
                    foreach (var info in actionList)
                    {
                        var type = info.type.Length == 0 ? "未分类" : info.type;
                        if (!actionTypeMap.ContainsKey(type))
                            actionTypeMap.Add(type, new List<ActionManagerParameter.ActionInfo>());
                        actionTypeMap[type].Add(info);
                    }

                    var actionMenuMap = new Dictionary<string, VRCExpressionsMenu>();
                    var mainActionMenuIndex = 0;
                    // 生成类型菜单
                    foreach (var item in actionTypeMap)
                    {
                        var name = item.Key;
                        var infoList = item.Value;

                        var menuList = new List<VRCExpressionsMenu>();
                        var nowActionMenu = CreateInstance<VRCExpressionsMenu>();
                        AssetDatabase.CreateAsset(nowActionMenu, menuDir + "ActionType_" + name + ".asset");
                        EditorUtility.SetDirty(nowActionMenu);
                        menuList.Add(nowActionMenu);
                        actionMenuMap.Add(name, nowActionMenu);

                        // 判断是否已分类
                        if (!hasClassify)
                            mainActionMenu = nowActionMenu;

                        if (nowActionMenu.controls.Count == 7)
                        {
                            var newMenu = CreateInstance<VRCExpressionsMenu>();
                            AssetDatabase.CreateAsset(newMenu, menuDir + "ActionMenu_" + mainActionMenuIndex++ + ".asset");
                            nowActionMenu.controls.Add(new VRCExpressionsMenu.Control
                            {
                                name = "下一页",
                                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                                subMenu = newMenu
                            });
                            nowActionMenu = newMenu;
                            menuList.Add(newMenu);
                            EditorUtility.SetDirty(newMenu);
                        }
                        if (hasClassify)
                            mainActionMenu.controls.Add(new VRCExpressionsMenu.Control
                            {
                                name = name,
                                type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                                subMenu = nowActionMenu
                            });

                        // 生成动作选项
                        foreach (var info in infoList)
                        {
                            if (nowActionMenu.controls.Count == 7)
                            {
                                var newMenu = CreateInstance<VRCExpressionsMenu>();
                                AssetDatabase.CreateAsset(newMenu, menuDir + "ActionType_" + name + "_" + (menuList.Count) + ".asset");
                                EditorUtility.SetDirty(newMenu);
                                if (nowActionMenu != null)
                                {
                                    nowActionMenu.controls.Add(new VRCExpressionsMenu.Control
                                    {
                                        name = "下一页",
                                        type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                                        subMenu = newMenu
                                    });
                                }
                                menuList.Add(newMenu);
                                nowActionMenu = newMenu;
                            }
                            nowActionMenu.controls.Add(new VRCExpressionsMenu.Control
                            {
                                name = info.name,
                                icon = info.menuImage,
                                type = VRCExpressionsMenu.Control.ControlType.Toggle,
                                parameter = new VRCExpressionsMenu.Control.Parameter { name = "Action_Int1" },
                                value = actionList.IndexOf(info) + 1
                            });
                        }
                    }
                    if (hasClassify)
                        AssetDatabase.CreateAsset(mainActionMenu, menuDir + "ActionMenu.asset");
                }

                // 配置主菜单
                if (expressionsMenu == null)
                    expressionsMenu = CreateInstance<VRCExpressionsMenu>();
                VRCExpressionsMenu.Control actionControl = null;
                // 换装入口
                foreach (var control in expressionsMenu.controls)
                {
                    if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                    {
                        if (control.name == "动作")
                            actionControl = control;
                    }
                }
                if (actionControl == null)
                {
                    expressionsMenu.controls.Add(new VRCExpressionsMenu.Control
                    {
                        name = "动作",
                        type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                        subMenu = mainActionMenu,
                    });
                }
                else
                {
                    actionControl.subMenu = mainActionMenu;
                }
                if (AssetDatabase.GetAssetPath(expressionsMenu) == "")
                    AssetDatabase.CreateAsset(expressionsMenu, dirPath + "ExpressionsMenu.asset");
                else
                    EditorUtility.SetDirty(expressionsMenu);
            }

            /*** 应用修改 ***/
            EditorUtility.SetDirty(baseController);
            EditorUtility.SetDirty(actionController);
            EditorUtility.SetDirty(fxController);

            EditorUtility.SetDirty(expressionsMenu);
            EditorUtility.SetDirty(expressionParameters);
            descriptor.customExpressions = true;
            descriptor.expressionParameters = expressionParameters;
            descriptor.expressionsMenu = expressionsMenu;
            EditorUtility.DisplayDialog("提醒", "应用成功，快上传模型测试下效果吧~", "确认");
        }

        private static void SetAnimatorTrackingControl(VRCAnimatorTrackingControl control, VRC_AnimatorTrackingControl.TrackingType type)
        {
            control.trackingHead = type;
            control.trackingLeftHand = type;
            control.trackingRightHand = type;
            control.trackingHip = type;
            control.trackingLeftFoot = type;
            control.trackingRightFoot = type;
            control.trackingLeftFingers = type;
            control.trackingRightFingers = type;
            control.trackingEyes = type;
            control.trackingMouth = type;
        }
    }
}
#endif