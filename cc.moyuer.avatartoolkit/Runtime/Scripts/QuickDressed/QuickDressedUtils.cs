#if UNITY_EDITOR
// 使用旧版动骨
// #define USE_DynamicBone

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using VRC.SDK3.Dynamics.PhysBone.Components;
using static VRChatAvatarToolkit.MoyuToolkitUtils;


namespace VRChatAvatarToolkit.QuickDressed
{
    public class QuickDressedUtils : EditorWindow
    {
        protected readonly static string[] itemTypeNames = new string[] { "衣服", "头发", "其他" };
        protected enum ItemType
        {
            Cloths,
            Hair,
            Other
        }

        protected readonly static string[] dressModeNames = new string[] { "移动到骨骼(稳定)", "父约束(实验性)" };
        protected enum DressMode
        {
            Move,
            ParentConstraint
        }

        protected GameObject AutoDress(GameObject sourceAvatar, GameObject sourceItem, ItemType itemType, Transform targetTransform, Transform headTransform, DressMode dressMode, string itemName)
        {
            ClearConsole();

            var errMsg = "发生了未知的错误，请检查Console了解详情！";
            GameObject avatar = null, item = null;
            try
            {
                avatar = Instantiate(sourceAvatar, sourceAvatar.transform.localPosition, Quaternion.identity);
                item = Instantiate(sourceItem, sourceItem.transform.localPosition, Quaternion.identity);
                avatar.name = sourceAvatar.name;
                item.name = itemName;
                avatar.SetActive(true);
                item.SetActive(true);

                if (targetTransform != null)
                    targetTransform = avatar.transform.Find(GetTransfromPath(targetTransform).Substring(GetTransfromPath(sourceAvatar).Length + 1));
                if (headTransform != null)
                    headTransform = avatar.transform.Find(GetTransfromPath(headTransform).Substring(GetTransfromPath(sourceAvatar).Length + 1));

                var avatarAnimator = avatar.GetComponent<Animator>();
                var armatureName = GetAvatarArmatureName(avatar);
                var humanBoneDict = GetAvatarHumanBones(avatarAnimator);

                if (itemType == ItemType.Cloths)
                {
                    item.transform.SetParent(avatar.transform, true);
                    // 把动骨移出来
                    MoveItemDynamicBone(avatar, item);

                    if (dressMode == DressMode.Move) // 移动骨骼
                    {
                        // 修复VRCPhysBone
                        var physBoneMap = new Dictionary<VRCPhysBone, string>();
                        {
                            var physBones = item.GetComponentsInChildren<VRCPhysBone>();
                            foreach (var physBone in physBones)
                            {
                                if (physBone.rootTransform != null)
                                {
                                    var path = GetTransfromPath(physBone.rootTransform);
                                    path = path.Replace(GetTransfromPath(item.transform) + "/", "");
                                    physBoneMap.Add(physBone, path);
                                }
                            }
                        }

                        // 移动骨骼
                        var renameList = new List<GameObject>();
                        var avatarArmatureMap = GetAllChild(avatar.transform);
                        var clothArmatureMap = GetAllChild(item.transform);
                        var moveBoneList = new List<Transform>();
                        foreach (var armature in clothArmatureMap)
                        {
                            var path = armature.Key;
                            var transform = armature.Value;
                            if (path.StartsWith(armatureName + "/"))
                            {
                                var isHumanBone = humanBoneDict.ContainsValue(avatar.transform.Find(path));
                                if (avatarArmatureMap.ContainsKey(path) && isHumanBone)
                                {
                                    // 如果是人形骨骼，就移动过去
                                    var supArmature = avatar.transform.Find(path);

                                    transform.parent = supArmature;
                                    renameList.Add(transform.gameObject);
                                }
                                else
                                {
                                    // 模型没有对应骨骼或这个骨骼不是人物骨骼，可能是衣服特有的
                                    var supArmature = avatar.transform.Find(path.Substring(0, path.LastIndexOf("/")));
                                    // 父骨骼移动过了，就不用再移动了
                                    if (!moveBoneList.Contains(transform.parent))
                                    {
                                        transform.parent = supArmature;
                                        renameList.Add(transform.gameObject);
                                    }
                                }
                                moveBoneList.Add(transform);
                            }
                        }

                        // 改名骨骼
                        for (var i = 0; i < renameList.Count; i++)
                            renameList[i].name += "(" + itemName + ")";

                        // 删除衣服Armature
                        DestroyImmediate(item.transform.Find(armatureName).gameObject);
                    }
                    else if (dressMode == DressMode.ParentConstraint) // 父约束
                    {
                        // 使用父约束绑定骨骼
                        var moveBoneList = new List<Transform>();
                        var avatarArmatureMap = GetAllChild(avatar.transform);
                        var clothArmatureMap = GetAllChild(item.transform);
                        var headTranPath = GetTransfromPath(humanBoneDict[HumanBodyBones.Head]).Substring(avatar.name.Length + 1);
                        foreach (var armature in clothArmatureMap)
                        {
                            var path = armature.Key;
                            var transform = armature.Value;
                            if (path.StartsWith(armatureName + "/") && avatarArmatureMap.ContainsKey(path))
                            {
                                var supArmature = avatar.transform.Find(path);
                                var isHumanBone = humanBoneDict.ContainsValue(avatar.transform.Find(path));
                                if (path.StartsWith(headTranPath))
                                {
                                    // 头上的骨骼要移动过去，不然会遮挡视角
                                    // 父骨骼移动过了，就不用再移动了
                                    if (!moveBoneList.Contains(transform.parent))
                                    {
                                        transform.parent = supArmature;
                                        transform.gameObject.name += "(" + itemName + ")";
                                        moveBoneList.Add(transform);
                                    }
                                }
                                else if (isHumanBone)
                                {
                                    var parentConstraint = transform.gameObject.AddComponent<ParentConstraint>();
                                    parentConstraint.AddSource(new ConstraintSource() { sourceTransform = supArmature, weight = 1 });
                                    parentConstraint.constraintActive = true;
                                }
                            }
                        }
                    }
                }
                else if (itemType == ItemType.Hair)
                {
                    item.transform.SetParent(avatar.transform, true);
                    MoveItemDynamicBone(avatar, item); // 把动骨移出来
                    var itemArmature = item.transform.Find(armatureName);
                    itemArmature.name = item.name;
                    itemArmature.SetParent(headTransform, true);
                }
                else if (itemType == ItemType.Other)
                {
                    if (dressMode == DressMode.Move) // 移动
                    {
                        item.transform.SetParent(targetTransform, true);
                    }
                    else if (dressMode == DressMode.ParentConstraint) // 父约束
                    {
                        item.transform.SetParent(avatar.transform, true);
                        var parentConstraint = item.AddComponent<ParentConstraint>();
                        parentConstraint.AddSource(new ConstraintSource() { sourceTransform = targetTransform, weight = 1 });
                        parentConstraint.constraintActive = true;
                    }
                }

                Undo.RecordObject(sourceAvatar, "Backup Avatar");
                sourceAvatar.name += "(backup)";
                sourceAvatar.SetActive(false);
                Undo.RegisterCreatedObjectUndo(avatar, "ApplyDressed");
                Undo.DestroyObjectImmediate(sourceItem);
                EditorGUIUtility.PingObject(item);
                return avatar;
            }
            catch (Exception e)
            {
                if (item != null) DestroyImmediate(item);
                if (avatar != null) DestroyImmediate(avatar);
                Debug.LogException(e);
                EditorUtility.DisplayDialog("警告", errMsg, "确认");
                return null;
            }
        }

        // 获取模型上的人形骨骼
        private static Dictionary<HumanBodyBones, Transform> GetAvatarHumanBones(Animator animator)
        {
            var dict = new Dictionary<HumanBodyBones, Transform>();
            for (var i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                var tran = animator.GetBoneTransform((HumanBodyBones)i);
                if (tran) dict.Add((HumanBodyBones)i, tran);
            }
            return dict;
        }

        // 移动动骨组件
        private static void MoveItemDynamicBone(GameObject avatar, GameObject item)
        {
            var armatureName = GetAvatarArmatureName(avatar);
#if USE_DynamicBone
            var dynamicBones = item.transform.Find(armatureName).GetComponentsInChildren<DynamicBone>();
            if(dynamicBones.Length > 0)
            {
                foreach (var dynamicBone in dynamicBones)
                {
                    var itemDB = new GameObject(item.name + "_PB");
                    itemDB.transform.SetParent(item.transform);
                    UnityEditorInternal.ComponentUtility.CopyComponent(dynamicBone);
                    UnityEditorInternal.ComponentUtility.PasteComponentAsNew(itemDB);
                    DestroyImmediate(dynamicBone);
                }
            }
#endif
            var physBones = item.transform.Find(armatureName).GetComponentsInChildren<VRCPhysBone>();
            if (physBones.Length > 0)
            {
                var itemPB = new GameObject(item.name + "_PB");
                Undo.RegisterCreatedObjectUndo(itemPB, "ApplyDressed");
                itemPB.transform.SetParent(item.transform);
                foreach (var physBone in physBones)
                {
                    if(physBone.rootTransform == null)physBone.rootTransform = physBone.transform;
                    UnityEditorInternal.ComponentUtility.CopyComponent(physBone);
                    UnityEditorInternal.ComponentUtility.PasteComponentAsNew(itemPB);
                    DestroyImmediate(physBone);
                }
            }
        }

        private static Dictionary<string, Transform> GetAllChild(Transform transform)
        {
            var map = new Dictionary<string, Transform>();
            GetAllChild2(transform, transform, ref map);
            map.Remove(GetTransfromPath(transform));
            return map;
        }

        private static void GetAllChild2(Transform transform, Transform superTransform, ref Dictionary<string, Transform> list)
        {
            if (transform != superTransform)
            {
                var path = GetTransfromPath(transform);
                path = path.Substring(GetTransfromPath(superTransform).Length + 1);
                list.Add(path, transform);
            }
            if (transform.childCount == 0)
            {
                return;
            }
            else
            {
                foreach (Transform child in transform)
                {
                    GetAllChild2(child, superTransform, ref list);
                }
            }
        }
    }
}
#endif