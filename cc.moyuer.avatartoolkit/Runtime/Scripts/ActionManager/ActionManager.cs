#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using static VRChatAvatarToolkit.MoyuToolkitUtils;
namespace VRChatAvatarToolkit
{
    public class ActionManager : ActionManagerUtils
    {
        internal const int maxActionNum = 255;

        private Vector2 mainScrollPos;

        private GameObject avatar;
        private ActionManagerParameter parameter;
        private string avatarId;
        private bool autoLock = true;

        private List<ActionItemInfo> actionItemList = new List<ActionItemInfo>();
        private void OnEnable()
        {
            foreach (var info in actionItemList)
            {
                info.animBool.valueChanged.RemoveAllListeners();
                info.animBool.valueChanged.AddListener(Repaint);
            }
        }
        private void OnGUI()
        {
            GUILayout.Space(10);
            GUI.skin.label.fontSize = 24;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label("动作管理器");
            GUI.skin.label.fontSize = 12;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label("by:如梦");
            GUILayout.Space(10);
            GUI.skin.label.fontSize = 12;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label("轻松管理模型动作、舞蹈");
            GUILayout.Space(10);

            var newAvatar = (GameObject)EditorGUILayout.ObjectField("选择模型：", avatar, typeof(GameObject), true);
            if (avatar != newAvatar)
            {
                avatar = newAvatar;
                if (newAvatar != null && newAvatar.GetComponent<VRCAvatarDescriptor>() == null)
                {
                    avatar = null;
                    EditorUtility.DisplayDialog("提醒", "本插件仅供SDK3模型使用！", "确认");
                }
                parameter = null;
                actionItemList.Clear();
                if (avatar != null)
                {
                    avatarId = GetOrCreateAvatarId(avatar);
                    ReadParameter();
                }
            }
            GUILayout.Space(10);
            if (avatar == null)
            {
                EditorGUILayout.HelpBox("请先选择一个模型", MessageType.Info);
                GUILayout.Space(10);
            }
            else if (parameter == null)
            {
                Print("创建了新的配置文件");
                parameter = CreateActionManagerParameter(avatar);
                ReadParameter();
            }
            else
            {
                mainScrollPos = GUILayout.BeginScrollView(mainScrollPos);
                // 主UI
                autoLock = EditorGUILayout.Toggle("动作时锁定位置：", autoLock);
                EditorGUI.BeginChangeCheck();
                parameter.audio3D = EditorGUILayout.Toggle("声音根据距离衰减：", parameter.audio3D);
                if (parameter.audio3D)
                {
                    parameter.audioMinDistance = EditorGUILayout.FloatField("   开始衰减距离：", parameter.audioMinDistance);
                    parameter.audioMaxDistance = EditorGUILayout.FloatField("   结束衰减距离：", parameter.audioMaxDistance);
                    if (parameter.audioMinDistance < 0) parameter.audioMinDistance = 0;
                    if (parameter.audioMinDistance >= parameter.audioMaxDistance)
                        parameter.audioMaxDistance = parameter.audioMinDistance + 1;
                }
                var sum = parameter.actionList.Count;
                if (sum == 0)
                {
                    EditorGUILayout.HelpBox("当前动作列表为空，先点击下面按钮添加一个吧", MessageType.Info);
                }
                else
                {
                    var actionNameList = new List<string>();
                    foreach (var info in actionItemList)
                        actionNameList.Add(info.name);
                    // 遍历信息
                    EditorGUILayout.LabelField("动作列表：");
                    var classify = HasClassify(actionItemList);
                    for (var index = 0; index < sum; index++)
                    {
                        var info = actionItemList[index];
                        var name = (classify ? "【" + (info.type.Length > 0 ? info.type : "未分类") + "】" : "") + info.name;
                        if (info.animation == null) name += "（无效）";
                        var newTarget = EditorGUILayout.Foldout(info.animBool.target, name, true);
                        if (newTarget != info.animBool.target)
                        {
                            if (newTarget)
                                foreach (var _info in actionItemList)
                                    _info.animBool.target = false;
                            info.animBool.target = newTarget;
                        }
                        if (EditorGUILayout.BeginFadeGroup(info.animBool.faded))
                        {
                            // 样式嵌套Start
                            EditorGUILayout.BeginVertical(GUI.skin.box);
                            GUILayout.Space(5);
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space(15);
                            EditorGUILayout.BeginVertical();

                            // 内容
                            EditorGUILayout.BeginHorizontal();
                            info.image = (Texture2D)EditorGUILayout.ObjectField("", info.image, typeof(Texture2D), true, GUILayout.Width(60), GUILayout.Height(60));
                            GUILayout.Space(5);
                            EditorGUILayout.BeginVertical();

                            //操作按钮
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.FlexibleSpace();
                            if (index > 0 && GUILayout.Button("上移", GUILayout.Width(60)))
                            {
                                MoveListItem(ref actionItemList, index, index - 1);
                                break;
                            }
                            else if (index < actionItemList.Count - 1 && GUILayout.Button("下移", GUILayout.Width(60)))
                            {
                                MoveListItem(ref actionItemList, index, index + 1);
                                break;
                            }
                            if (GUILayout.Button("删除", GUILayout.Width(60)))
                            {
                                RemoveAction(index);
                                break;
                            }
                            EditorGUILayout.EndHorizontal();

                            EditorGUILayout.BeginHorizontal();
                            //唯一动作名
                            EditorGUILayout.BeginVertical();
                            EditorGUILayout.LabelField("动作名称", GUILayout.Width(55));
                            var newName = EditorGUILayout.TextField(info.name).Trim();
                            if (!actionNameList.Contains(newName) && newName.Length > 0)
                                info.name = newName;
                            EditorGUILayout.EndVertical();
                            //分类
                            EditorGUILayout.BeginVertical();
                            EditorGUILayout.LabelField("分类", GUILayout.Width(55));
                            info.type = EditorGUILayout.TextField(info.type).Trim();
                            EditorGUILayout.EndVertical();
                            EditorGUILayout.EndHorizontal();

                            EditorGUILayout.EndVertical();
                            EditorGUILayout.EndHorizontal();

                            //各种参数
                            info.autoExit = EditorGUILayout.Toggle("播放完自动复位：", info.autoExit);
                            info.animation = (AnimationClip)EditorGUILayout.ObjectField("动作：", info.animation, typeof(AnimationClip), true);
                            info.audio = (AudioClip)EditorGUILayout.ObjectField("音乐：", info.audio, typeof(AudioClip), true);

                            // 样式嵌套End
                            EditorGUILayout.EndVertical();
                            GUILayout.Space(5);
                            EditorGUILayout.EndHorizontal();
                            GUILayout.Space(5);
                            EditorGUILayout.EndVertical();
                        }
                        EditorGUILayout.EndFadeGroup();
                    }
                    // 检测是否有修改
                    if (EditorGUI.EndChangeCheck())
                    {
                        WriteParameter();
                    }
                }
                GUILayout.EndScrollView();
                if (sum < maxActionNum && GUILayout.Button("添加动作"))
                    AddAction();

                GUILayout.Space(5);

                //下操作栏
                GUILayout.Space(10);

                GUILayout.Label("操作菜单");
                GUILayout.Space(5);
                GUILayout.BeginHorizontal();

                if (GUILayout.Button("一键应用到模型"))
                    ApplyToAvatar(avatar, parameter, autoLock);

                GUILayout.EndHorizontal();
                GUILayout.Space(10);
            }
        }
        private void AddAction()
        {
            foreach (var info in actionItemList)
                info.animBool.target = false;
            var name = "动作" + (actionItemList.Count + 1).ToString();
            var actionItemInfo = new ActionItemInfo(name);
            actionItemInfo.autoExit = true;
            actionItemInfo.animBool.valueChanged.AddListener(Repaint);
            actionItemInfo.animBool.target = true;
            actionItemList.Add(actionItemInfo);
            WriteParameter();
        }
        private void RemoveAction(int index)
        {
            if (!EditorUtility.DisplayDialog("注意", "真的要删除这个动作吗？", "确认", "取消"))
                return;
            actionItemList.RemoveAt(index);
            WriteParameter();
        }
        private void ReadParameter()
        {
            actionItemList.Clear();
            if (avatarId == null) return;
            if (parameter == null) parameter = GetActionManagerParameter(avatarId);
            if (parameter == null) return;
            foreach (var info in parameter.actionList)
            {
                var item = new ActionItemInfo(info);
                item.animBool.valueChanged.AddListener(Repaint);
                actionItemList.Add(item);
            }
        }
        private void WriteParameter()
        {
            if (parameter == null) return;
            var actionList = new List<ActionManagerParameter.ActionInfo>();
            foreach (var info in actionItemList)
            {
                var item = new ActionManagerParameter.ActionInfo(info);
                actionList.Add(item);
            }
            parameter.actionList = actionList;
            EditorUtility.SetDirty(parameter);
        }
    }
}
#endif