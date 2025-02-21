#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;
using static VRChatAvatarToolkit.MoyuToolkitUtils;

namespace VRChatAvatarToolkit.QuickDressed
{
    public class QuickDressedEditor : QuickDressedUtils
    {
        private Vector2 mainScrollPos;
        private readonly AnimBool advModeAnimBool = new AnimBool();
        private bool isDefault = true;

        protected class AvatarBone
        {
            public HumanBodyBones bone;
            public Transform tran;
            public string name;
        }

        private AvatarBone[] dressPositions = new AvatarBone[] { };
        private string[] dressPositionNames = new string[] { };

        private GameObject avatar, item;
        private ItemType itemType;
        private int dressPosition;
        private DressMode dressMode;
        private string itemName;
        private Transform targetTransform, headTransform;

        private void OnEnable()
        {
            advModeAnimBool.valueChanged.RemoveAllListeners();
            advModeAnimBool.valueChanged.AddListener(Repaint);

            avatar = item = null;
            dressPosition = 0;
            dressMode = DressMode.Move;
            itemName = "";
            targetTransform = null;
            dressPositions = new AvatarBone[] { };
            dressPositionNames = new string[] { };

            if (avatar != null && item != null) return;
            var animatorList = FindObjectsOfType<Animator>();
            for (var i = 0; i < animatorList.Length; i++)
            {
                var animator = animatorList[i];
                if (animator.transform.parent != null) continue;
                if (animator.isHuman && avatar == null)
                {
                    avatar = animator.gameObject;
                    ReloadHumanBones();
                }
                else if (!animator.isHuman && item == null)
                {
                    item = animator.gameObject;
                    itemName = item.name;
                }
            }
            isDefault = avatar != null && item != null;
            itemType = GetItemType();
            ReloadTargetTransform();
        }

        private void ReloadTargetTransform()
        {
            if (dressPosition == dressPositions.Length - 1) return;
            switch (itemType)
            {
                case ItemType.Cloths:
                    targetTransform = null;
                    break;
                case ItemType.Hair:
                    targetTransform = null;
                    break;
                case ItemType.Other:
                    targetTransform = (dressPosition >= 0 && dressPosition < dressPositions.Length) ? dressPositions[dressPosition].tran : null;
                    break;
            }
        }

        private ItemType GetItemType()
        {
            if (avatar != null && item != null)
            {
                try
                {
                    var armatureName = GetAvatarArmatureName(avatar);
                    var isCloths = item.transform.Find(armatureName) != null;
                    return isCloths ? ItemType.Cloths : ItemType.Other;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            return ItemType.Other;
        }

        private void CheckClothTran()
        {
            if (item == null || avatar == null) return;
            var itemTran = GetTransfromPath(item.transform);
            var avatarTran = GetTransfromPath(avatar.transform);
            if (itemTran.StartsWith(avatarTran + "/"))
            {
                EditorUtility.DisplayDialog("提醒", "不能选择已经在Avatar身上的物件！", "确认");
                item = null;
                return;
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(5, 10, position.width - 10, position.height - 20));
            EditorGUI.BeginChangeCheck();

            mainScrollPos = GUILayout.BeginScrollView(mainScrollPos);
            GUILayout.Label("快速穿戴", new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter
            });
            GUILayout.Label("支持模型快速绑定衣服、头发、配饰等", new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            });
            GUILayout.Space(10);

            EditorGUI.BeginChangeCheck();
            avatar = (GameObject)EditorGUILayout.ObjectField("Avatar", avatar, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                dressPosition = 0;
                targetTransform = null;
                dressPositions = new AvatarBone[] { };
                dressPositionNames = new string[] { };
                if (avatar != null)
                {
                    try
                    {
                        var animator = avatar.GetComponent<Animator>();
                        if (!animator.isHuman) throw new Exception("Is not human");
                        ReloadHumanBones();
                        ReloadTargetTransform();
                    }
                    catch (Exception e)
                    {
                        avatar = null;
                        Debug.LogException(e);
                        EditorUtility.DisplayDialog("提醒", "检测到模型类型不是Human，请修改模型动画类型后再试！", "确认");
                    }
                }
                itemType = GetItemType();
                CheckClothTran();
            }
            EditorGUI.BeginChangeCheck();
            item = (GameObject)EditorGUILayout.ObjectField("物件", item, typeof(GameObject), true);
            if (EditorGUI.EndChangeCheck())
            {
                itemName = item != null ? item.name : "";
                itemType = GetItemType();
                CheckClothTran();
                ReloadTargetTransform();
            }


            EditorGUI.BeginChangeCheck();
            itemType = (ItemType)EditorGUILayout.Popup("物件类型", (int)itemType, itemTypeNames);
            if (EditorGUI.EndChangeCheck())
            {
                if (avatar != null && item != null && itemType == ItemType.Cloths && GetItemType() != ItemType.Cloths)
                {
                    EditorUtility.DisplayDialog("提醒", "检测不到该物件的" + GetAvatarArmatureName(avatar) + "，无法切换选择为衣服！", "确认");
                    itemType = ItemType.Other;
                }
                if (itemType == ItemType.Hair)
                    dressMode = DressMode.Move;
                ReloadTargetTransform();
            }

            EditorGUI.BeginDisabledGroup(avatar == null);
            if (itemType == ItemType.Other)
            {
                EditorGUI.BeginChangeCheck();
                dressPosition = EditorGUILayout.Popup("绑定位置", dressPosition, dressPositionNames);
                if (EditorGUI.EndChangeCheck())
                {
                    ReloadTargetTransform();
                    if (targetTransform != null)
                        EditorGUIUtility.PingObject(targetTransform);
                }
                if (dressPosition == dressPositions.Length - 1)
                {
                    EditorGUI.BeginChangeCheck();
                    targetTransform = (Transform)EditorGUILayout.ObjectField("自定义位置", targetTransform, typeof(Transform), true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        var newTranPath = GetTransfromPath(targetTransform);
                        var avatarTran = GetTransfromPath(avatar.transform);
                        if (!newTranPath.StartsWith(avatarTran))
                        {
                            ClearConsole();
                            Debug.LogWarning("选择的骨骼不属于Avatar");
                            Debug.Log("选择的Avatar: " + avatarTran);
                            Debug.Log("选择的骨骼: " + newTranPath);
                            EditorUtility.DisplayDialog("提醒", "选择的骨骼不属于Avatar，详情请查看Console吧~", "确认");
                            targetTransform = null;
                        }
                    }
                }
            }
            EditorGUI.EndDisabledGroup();

            advModeAnimBool.target = EditorGUILayout.Foldout(advModeAnimBool.target, "高级配置", true);
            if (EditorGUILayout.BeginFadeGroup(advModeAnimBool.faded))
            {
                EditorGUI.BeginChangeCheck();
                dressMode = (DressMode)EditorGUILayout.Popup("绑定模式", (int)dressMode, dressModeNames);
                if (EditorGUI.EndChangeCheck())
                {
                    if (itemType == ItemType.Hair)
                    {
                        dressMode = DressMode.Move;
                        EditorUtility.DisplayDialog("提醒", "头发不能选择父约束模式，否则会出现遮挡视角问题", "确认");
                    }
                }
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("物件重命名", GUILayout.Width(150), GUILayout.ExpandWidth(false));
                itemName = EditorGUILayout.TextField(itemName).Trim();
                if (itemName.Length == 0 && item != null) itemName = item.name;
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndFadeGroup();

            GUILayout.Space(10);
            if (isDefault) EditorGUILayout.HelpBox("以上参数为自动填充，请确认后再使用", MessageType.Info);
            var existItem = avatar != null && item != null && avatar.transform.Find(item.name) != null;
            if (existItem) EditorGUILayout.HelpBox("模型下已有同名配饰，请选择另一物件或 修改高级配置->物件重命名 后再试", MessageType.Error);
            if (avatar == null) EditorGUILayout.HelpBox("请添加Avatar", MessageType.Warning);
            if (item == null) EditorGUILayout.HelpBox("请添加物件", MessageType.Warning);
            if (avatar != null && headTransform == null) EditorGUILayout.HelpBox("奇怪，找不到Avatar的头了。检查一下FBX文件的Rig设置吧", MessageType.Error);
            if (itemType == ItemType.Cloths && dressMode == DressMode.ParentConstraint) EditorGUILayout.HelpBox("衣服在父约束模式下，仍然会移动Head骨骼以避免视角遮挡", MessageType.Info);
            var dressPositionMiss = itemType == ItemType.Other && dressPosition == dressPositions.Length - 1 && targetTransform == null;
            if (dressPositionMiss)
                EditorGUILayout.HelpBox("请填写绑定位置", MessageType.Warning);

            EditorGUI.BeginDisabledGroup(avatar == null || item == null || dressPositionMiss || existItem || headTransform == null);
            if (GUILayout.Button("应用") && avatar != null && item != null)
            {
                var newAvatar = AutoDress(avatar, item, itemType, targetTransform, headTransform, dressMode, itemName);
                if (newAvatar != null)
                {
                    avatar = newAvatar;
                    item = null;
                    itemName = "";
                    ReloadHumanBones();
                    EditorUtility.DisplayDialog("提醒", "应用成功！建议使用下面的工具测试衣服是否正确绑定~", "确认");
                }
            }
            EditorGUI.EndDisabledGroup();

            GUILayout.EndScrollView();
            EditorGUILayout.HelpBox("测试穿戴后是否正常？", MessageType.None);
            if (GUILayout.Button("打开动作播放器"))
                GetWindow(typeof(AvatarAnimationPlayer.AvatarAnimationPlayerEditor));

            if (EditorGUI.EndChangeCheck())
                isDefault = false;
            GUILayout.EndArea();
        }


        private void ReloadHumanBones()
        {
            headTransform = null;
            var animator = avatar.GetComponent<Animator>();
            if (animator == null)
            {
                dressPositions = new AvatarBone[] { };
                dressPositionNames = new string[] { };
                return;
            }
            var boneMap = new Dictionary<string, HumanBodyBones>()
            {
                {"头", HumanBodyBones.Head},
                {"脖子", HumanBodyBones.Neck},

                {"身体/臀部", HumanBodyBones.Hips},
                {"身体/脊柱", HumanBodyBones.Spine},
                {"身体/胸部", HumanBodyBones.Chest},

                {"左手/肩膀", HumanBodyBones.LeftShoulder},
                {"左手/上臂", HumanBodyBones.LeftUpperArm},
                {"左手/下臂", HumanBodyBones.LeftLowerArm},
                {"左手/手腕", HumanBodyBones.LeftHand},

                {"右手/肩膀", HumanBodyBones.RightShoulder},
                {"右手/上臂", HumanBodyBones.RightUpperArm},
                {"右手/下臂", HumanBodyBones.RightLowerArm},
                {"右手/手腕", HumanBodyBones.RightHand},

                {"左腿/大腿", HumanBodyBones.LeftUpperLeg},
                {"左腿/小腿", HumanBodyBones.LeftUpperLeg},
                {"左腿/脚", HumanBodyBones.LeftFoot},
                {"左腿/脚尖", HumanBodyBones.LeftToes},

                {"右腿/大腿", HumanBodyBones.RightUpperLeg},
                {"右腿/小腿", HumanBodyBones.RightUpperLeg},
                {"右腿/脚", HumanBodyBones.RightFoot},
                {"右腿/脚尖", HumanBodyBones.RightToes},
            };

            var dressPositionList = new List<AvatarBone>();
            var dressPositionNameList = new List<string>();
            foreach (var boneInfo in boneMap)
            {
                var tran = animator.GetBoneTransform(boneInfo.Value);
                if (tran == null) continue;
                dressPositionList.Add(new AvatarBone()
                {
                    name = boneInfo.Key,
                    bone = boneInfo.Value,
                    tran = tran,
                });
                dressPositionNameList.Add(boneInfo.Key);

                if (boneInfo.Value == HumanBodyBones.Head)
                    headTransform = tran;
            }

            dressPositionNameList.Add("自定义");

            dressPositions = dressPositionList.ToArray();
            dressPositionNames = dressPositionNameList.ToArray();
        }
    }
}
#endif