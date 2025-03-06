#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.AnimatedValues;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using UnityEditor;
using System.IO;
using UnityEditor.Animations;
using System;
using static VRChatAvatarToolkit.MoyuToolkitUtils;
using static VRChatAvatarToolkit.AvatarWardrobeParameter;

namespace VRChatAvatarToolkit
{
    public class AvatarWardrobeUtils : EditorWindow
    {
        internal const int maxClothNum = 255;

        [System.Serializable]
        public class ObjInfo
        {
            public string name = "";
            public string type = "";
            public Texture2D image;
            public AnimBool animBool = new AnimBool { speed = 3.0f };
        }

        [System.Serializable]
        public class ClothObjInfo : ObjInfo
        {
            public List<GameObject> showObjectList, hideObjectList;
            public List<BlendShapePack> blendShapePacks;

            public ClothObjInfo(string _name = "新衣服")
            {
                name = _name;
                showObjectList = new List<GameObject>();
                hideObjectList = new List<GameObject>();
            }
        }

        [System.Serializable]
        public class OrnamentObjInfo : ObjInfo
        {
            public bool isShow;
            public List<GameObject> objectList;

            public OrnamentObjInfo(string _name = "新配饰")
            {
                name = _name;
                isShow = true;
                objectList = new List<GameObject>();
            }
        }

        [System.Serializable]
        public class ExclusionObjInfo : ObjInfo
        {
            public List<GameObject> objectList;

            public ExclusionObjInfo()
            {
                name = "新配饰";
                objectList = new List<GameObject>();
            }

            public ExclusionObjInfo(string _name)
            {
                name = _name;
                objectList = new List<GameObject>();
            }
        }

        [System.Serializable]
        public class MutualExclusionObjInfo: ObjInfo
        {
            public uint defaultIndex;
            public List<ExclusionObjInfo> mutualExclusions;

            public MutualExclusionObjInfo()
            {
                name = "新互斥组";
                defaultIndex = 0;
                mutualExclusions = new List<ExclusionObjInfo>();
            }

            public MutualExclusionObjInfo(string _name)
            {
                name = _name;
                defaultIndex = 0;
                mutualExclusions = new List<ExclusionObjInfo>();
            }
        }

        // 获取该模型的参数文件存放位置
        internal static string GetParameterDirPath(string avatarId, string path)
        {
            return "Assets/AvatarData/" + avatarId + "/" + path;
        }
        // 通过检测type字段，判断是否为分类模式
        internal static bool HasClassify(List<ClothObjInfo> list)
        {
            foreach (var item in list)
                if (item.type.Length > 0)
                    return true;
            return false;
        }
        // 通过检测type字段，判断是否为分类模式
        internal static bool HasClassify(List<OrnamentObjInfo> list)
        {
            foreach (var item in list)
                if (item.type.Length > 0)
                    return true;
            return false;
        }

        // 创建衣柜参数文件
        internal static AvatarWardrobeParameter CreateAvatarWardrobeParameter(GameObject avatar)
        {
            if (avatar == null)
                return null;
            var parameter = CreateInstance<AvatarWardrobeParameter>();
            var avatarId = GetOrCreateAvatarId(avatar);
            parameter.avatarId = avatarId;
            parameter.clothList.Add(new AvatarWardrobeParameter.ClothInfo { name = "衣服1" });
            var dir = GetParameterDirPath(avatarId, "");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            AssetDatabase.CreateAsset(parameter, GetParameterDirPath(avatarId, "WardrobeParameter.asset"));
            return parameter;
        }
        // 获取衣柜参数文件
        internal static AvatarWardrobeParameter GetAvatarWardrobeParameter(string avatarId)
        {
            if (avatarId == null)
                return null;
            var path = GetParameterDirPath(avatarId, "/WardrobeParameter.asset");
            if (File.Exists(path))
            {
                var parameter = AssetDatabase.LoadAssetAtPath(path, typeof(AvatarWardrobeParameter)) as AvatarWardrobeParameter;
                return parameter;
            }
            return null;
        }

        // 获取某件衣服所需显示、隐藏的部件
        internal static Dictionary<string, List<GameObject>> GetClothParameter(List<ClothObjInfo> clothInfoList, int index)
        {
            if (index < 0 || index > clothInfoList.Count)
                return null;
            var showList = new List<GameObject>();
            var hideList = new List<GameObject>();

            // 反转其他衣服参数
            for (var a = 0; a < clothInfoList.Count; a++)
            {
                if (a == index)
                    continue;
                var info = clothInfoList[a];
                hideList = LinkGameObjectList(hideList, info.showObjectList);
                showList = LinkGameObjectList(showList, info.hideObjectList);
            }

            // 覆盖新衣服参数
            {
                var info = clothInfoList[index];
                showList = LinkGameObjectList(showList, info.showObjectList);
                hideList = LinkGameObjectList(hideList, info.hideObjectList);

                foreach (var obj in info.showObjectList)
                    hideList.Remove(obj);
                foreach (var obj in info.hideObjectList)
                    showList.Remove(obj);
            }
            var map = new Dictionary<string, List<GameObject>>
        {
            { "show", showList },
            { "hide", hideList }
        };
            return map;
        }
        // 获取某件衣服所需显示、隐藏的部件路径
        internal static Dictionary<string, List<string>> GetClothPathParameter(GameObject avatar, List<ClothObjInfo> clothInfoList, int index)
        {
            var map = GetClothParameter(clothInfoList, index);
            if (map == null || avatar == null)
                return null;
            var showList = map["show"];
            var hideList = map["hide"];

            // 转换为路径
            var avatarPath = avatar.transform.GetHierarchyPath() + "/";
            var showPathList = new List<string>();
            var hidePathList = new List<string>();
            foreach (var obj in showList)
            {
                var path = obj.transform.GetHierarchyPath();
                showPathList.Add(path.Substring(avatarPath.Length));
            }
            foreach (var obj in hideList)
            {
                var path = obj.transform.GetHierarchyPath();
                hidePathList.Add(path.Substring(avatarPath.Length));
            }

            var pathMap = new Dictionary<string, List<string>> { { "show", showPathList }, { "hide", hidePathList } };
            return pathMap;
        }

        internal static GameObject FindByPath(GameObject root, string path, char separator = '.')
        {
            if (root == null || string.IsNullOrEmpty(path))
                return null;

            Transform current = root.transform;
            string[] names = path.Split(separator);

            foreach (string name in names)
            {
                current = current.Find(name);
                if (current == null)
                    return null; // 路径不存在时返回 null
            }

            return current.gameObject;
        }


        // 预览衣服
        internal static void PrviewCloth(GameObject avatar, List<ClothObjInfo> clothInfoList, List<AvatarWardrobeParameter.BlendShapePack> defaultBlendShapes, int index)
        {
            if (index < 0 || index > clothInfoList.Count)
                return;
            var map = GetClothParameter(clothInfoList, index);
            var hideList = map["hide"];
            var showList = map["show"];

            // 开始执行
            foreach (var item in hideList)
                if (item != null)
                    item.SetActive(false);
            foreach (var item in showList)
                if (item != null)
                    item.SetActive(true);

            // 更新形态
            List<AvatarWardrobeParameter.BlendShapePack> blendShapePacks = new();
            foreach (var pack in clothInfoList[index].blendShapePacks)
            {
                blendShapePacks.Add(pack);
            }

            foreach (var pack in defaultBlendShapes)
            {
                bool needDefaultValue = true;
                foreach (var customPack in blendShapePacks)
                {
                    if (pack.path == customPack.path && pack.name == customPack.name)
                    {
                        needDefaultValue = false;
                        break;
                    }
                }

                if (needDefaultValue)
                {
                    blendShapePacks.Add(pack);
                }
            }

            Dictionary<string, SkinnedMeshRenderer> rendererMap = new();
            foreach(var blendShape in blendShapePacks)
            {
                if (!rendererMap.ContainsKey(blendShape.path))
                {
                    GameObject obj = FindByPath(avatar, blendShape.path);
                    if (obj)
                    {
                        var newRenderer = obj.GetComponent<SkinnedMeshRenderer>();
                        if (newRenderer)
                        {
                            rendererMap[blendShape.path] = newRenderer;
                        }
                    }
                }

                if (!rendererMap.ContainsKey(blendShape.path))
                {
                    Debug.Log($"错误的blendShape路径配置:{blendShape.path}");
                    continue;
                }

                var renderer = rendererMap[blendShape.path];
                var sharedMesh = renderer.sharedMesh;
                for (int i = 0, max = sharedMesh.blendShapeCount; i < max; i++)
                {
                    if (sharedMesh.GetBlendShapeName(i) == blendShape.name)
                    {
                        renderer.SetBlendShapeWeight(i, blendShape.value);
                    }
                }
            }
        }

        // 获取换装动画
        internal static AnimationClip GetClothAnimClip(List<ClothObjInfo> clothInfoList, List<AvatarWardrobeParameter.BlendShapePack> defaultBlendShapes, GameObject avatar, int index)
        {
            var clip = new AnimationClip { name = clothInfoList[index].name };
            var map = GetClothPathParameter(avatar, clothInfoList, index);
            var showList = map["show"];
            var hideList = map["hide"];
            foreach (var path in showList)
            {
                var frame = new Keyframe { time = 0, value = 1 };
                var curve = new AnimationCurve { keys = new Keyframe[] { frame } };
                EditorCurveBinding bind = new EditorCurveBinding
                {
                    path = path,
                    propertyName = "m_IsActive",
                    type = typeof(GameObject)
                };
                AnimationUtility.SetEditorCurve(clip, bind, curve);
            }
            foreach (var path in hideList)
            {
                var frame = new Keyframe { time = 0, value = 0 };
                var curve = new AnimationCurve { keys = new Keyframe[] { frame } };
                EditorCurveBinding bind = new EditorCurveBinding
                {
                    path = path,
                    propertyName = "m_IsActive",
                    type = typeof(GameObject)
                };
                AnimationUtility.SetEditorCurve(clip, bind, curve);
            }

            // 添加blendshape自定义
            foreach (var blendShapePack in clothInfoList[index].blendShapePacks)
            {
                var frame = new Keyframe { time = 0, value = blendShapePack.value };
                var curve = new AnimationCurve { keys = new Keyframe[] { frame } };
                EditorCurveBinding bind = new EditorCurveBinding
                {
                    path = blendShapePack.path,
                    propertyName = $"blendShape.{blendShapePack.name}",
                    type = typeof(SkinnedMeshRenderer)
                };
                AnimationUtility.SetEditorCurve(clip, bind, curve);
            }
            // 添加blendshape默认值
            foreach (var blendShapePack in defaultBlendShapes)
            {
                bool needDefaultValue = true;
                // 检查默认值是否有被自定义
                foreach (var customBlendShapePack in clothInfoList[index].blendShapePacks)
                {
                    if (blendShapePack.path == customBlendShapePack.path && blendShapePack.name == customBlendShapePack.name)
                    {
                        needDefaultValue = false;
                        break;
                    }
                }

                if (needDefaultValue)
                {
                    var frame = new Keyframe { time = 0, value = blendShapePack.value };
                    var curve = new AnimationCurve { keys = new Keyframe[] { frame } };
                    EditorCurveBinding bind = new EditorCurveBinding
                    {
                        path = blendShapePack.path,
                        propertyName = $"blendShape.{blendShapePack.name}",
                        type = typeof(SkinnedMeshRenderer)
                    };
                    AnimationUtility.SetEditorCurve(clip, bind, curve);
                }
            }
 
            return clip;
        }
        // 获取配饰开关动画
        internal static AnimationClip[] GetOrnamentAnimClip(List<OrnamentObjInfo> ornamentObjInfos, GameObject avatar, int index)
        {
            var clipList = new AnimationClip[2];
            var avatarPath = avatar.transform.GetHierarchyPath() + "/";
            var itemList = ornamentObjInfos[index].objectList;
            {
                var clip = new AnimationClip { name = ornamentObjInfos[index].name };
                foreach (var obj in itemList)
                {
                    var frame = new Keyframe { time = 0, value = 0 };
                    var curve = new AnimationCurve { keys = new Keyframe[] { frame } };
                    EditorCurveBinding bind = new EditorCurveBinding
                    {
                        path = obj.transform.GetHierarchyPath().Replace(avatarPath, ""),
                        propertyName = "m_IsActive",
                        type = typeof(GameObject)
                    };
                    AnimationUtility.SetEditorCurve(clip, bind, curve);
                }
                clipList[0] = clip;
            }
            {
                var clip = new AnimationClip { name = ornamentObjInfos[index].name };
                foreach (var obj in itemList)
                {
                    var frame = new Keyframe { time = 0, value = 1 };
                    var curve = new AnimationCurve { keys = new Keyframe[] { frame } };
                    EditorCurveBinding bind = new EditorCurveBinding
                    {
                        path = obj.transform.GetHierarchyPath().Replace(avatarPath, ""),
                        propertyName = "m_IsActive",
                        type = typeof(GameObject)
                    };
                    AnimationUtility.SetEditorCurve(clip, bind, curve);
                }
                clipList[1] = clip;
            }
            return clipList;
        }
        // 一键应用到模型
        internal static void ApplyToAvatar(GameObject avatar, List<AvatarWardrobeParameter.BlendShapePack> defaultBlendShapes,
            List<ClothObjInfo> clothInfoList, int defaultClothIndex, List<OrnamentObjInfo> ornamentInfoList)
        {
            ClearConsole();
            var avatarId = GetAvatarId(avatar);
            var dirPath = GetParameterDirPath(avatarId, "");
            var descriptor = avatar.GetComponent<VRCAvatarDescriptor>();
            var expressionParameters = descriptor.expressionParameters;
            var expressionsMenu = descriptor.expressionsMenu;

            /*** 配置VRCExpressionParameters ***/
            if (expressionParameters == null || expressionParameters.parameters == null)
            {
                expressionParameters = CreateInstance<VRCExpressionParameters>();
                var parameterTemplate = AssetDatabase.LoadAssetAtPath(GetAssetsPath("Assets/SDK3/Asset/Parameters.asset"),
                    typeof(VRCExpressionParameters)) as VRCExpressionParameters;
                expressionParameters.parameters = parameterTemplate.parameters;
                AssetDatabase.CreateAsset(expressionParameters, dirPath + "ExpressionParameters.asset");
            }
            {
                var parameters = expressionParameters.parameters;
                var newParameters = new List<VRCExpressionParameters.Parameter>();
                foreach (var parameter in parameters)
                    if (!parameter.name.StartsWith("Wardrobe") && parameter.name != "")
                        newParameters.Add(parameter);
                newParameters.Add(new VRCExpressionParameters.Parameter
                {
                    name = "Wardrobe_Int1",
                    valueType = VRCExpressionParameters.ValueType.Int,
                    defaultValue = defaultClothIndex,
                    saved = true
                });

                for (var i = 0; i < ornamentInfoList.Count; i++)
                {
                    newParameters.Add(new VRCExpressionParameters.Parameter
                    {
                        name = "WardrobeParts_" + i,
                        valueType = VRCExpressionParameters.ValueType.Bool,
                        defaultValue = ornamentInfoList[i].isShow ? 1 : 0,
                        saved = true
                    });
                }
                expressionParameters.parameters = newParameters.ToArray();
            }

            /*** 配置VRCExpressionsMenu ***/
            // 创建新Menu文件夹
            var menuDir = dirPath + "Menu/Wardrobe";
            if (Directory.Exists(menuDir))
                Directory.Delete(menuDir, true);
            Directory.CreateDirectory(menuDir);
            menuDir += "/";

            // 删除旧版本换装菜单（1.0.0版本）
            for (var i = 0; i <= maxClothNum / 7; i++)
            {
                var path = dirPath + "WardrobeMenu_" + i + ".asset";
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(path + ".meta"))
                    File.Delete(path + ".meta");
            }

            /*** 生成衣服换装菜单 ***/
            var mainClothMenu = CreateInstance<VRCExpressionsMenu>();
            {
                var hasClassify = HasClassify(clothInfoList);
                // 归类
                var clothTypeMap = new Dictionary<string, List<ClothObjInfo>>();
                foreach (var info in clothInfoList)
                {
                    var type = info.type.Length == 0 ? "未分类" : info.type;
                    if (!clothTypeMap.ContainsKey(type))
                        clothTypeMap.Add(type, new List<ClothObjInfo>());
                    clothTypeMap[type].Add(info);
                }

                var nowWardrobeMenu = mainClothMenu;
                var clothMenuMap = new Dictionary<string, VRCExpressionsMenu>();
                var mainClothMenuIndex = 0;
                // 生成类型菜单
                foreach (var item in clothTypeMap)
                {
                    var name = item.Key;
                    var infoList = item.Value;

                    var menuList = new List<VRCExpressionsMenu>();
                    var nowClothMenu = CreateInstance<VRCExpressionsMenu>();
                    AssetDatabase.CreateAsset(nowClothMenu, menuDir + "ClothType_" + name + ".asset");
                    EditorUtility.SetDirty(nowClothMenu);
                    menuList.Add(nowClothMenu);
                    clothMenuMap.Add(name, nowClothMenu);

                    // 判断是否已分类
                    if (!hasClassify)
                        mainClothMenu = nowClothMenu;

                    if (nowWardrobeMenu.controls.Count == 7)
                    {
                        var newMenu = CreateInstance<VRCExpressionsMenu>();
                        AssetDatabase.CreateAsset(newMenu, menuDir + "ClothMenu_" + mainClothMenuIndex++ + ".asset");
                        nowWardrobeMenu.controls.Add(new VRCExpressionsMenu.Control
                        {
                            name = "下一页",
                            type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                            subMenu = newMenu
                        });
                        nowWardrobeMenu = newMenu;
                        menuList.Add(newMenu);
                        EditorUtility.SetDirty(newMenu);
                    }
                    nowWardrobeMenu.controls.Add(new VRCExpressionsMenu.Control
                    {
                        name = name,
                        type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                        subMenu = nowClothMenu
                    });

                    // 生成衣服选项
                    foreach (var info in infoList)
                    {
                        if (nowClothMenu.controls.Count == 7)
                        {
                            var newMenu = CreateInstance<VRCExpressionsMenu>();
                            AssetDatabase.CreateAsset(newMenu, menuDir + "ClothType_" + name + "_" + (menuList.Count) + ".asset");
                            EditorUtility.SetDirty(newMenu);
                            if (nowClothMenu != null)
                            {
                                nowClothMenu.controls.Add(new VRCExpressionsMenu.Control
                                {
                                    name = "下一页",
                                    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                                    subMenu = newMenu
                                });
                                EditorUtility.SetDirty(nowClothMenu);
                                AssetDatabase.SaveAssets();
                            }
                            menuList.Add(newMenu);
                            nowClothMenu = newMenu;
                        }
                        nowClothMenu.controls.Add(new VRCExpressionsMenu.Control
                        {
                            name = info.name,
                            icon = info.image,
                            type = VRCExpressionsMenu.Control.ControlType.Toggle,
                            parameter = new VRCExpressionsMenu.Control.Parameter { name = "Wardrobe_Int1" },
                            value = clothInfoList.IndexOf(info)// + 1
                        });
                        EditorUtility.SetDirty(nowClothMenu);
                        AssetDatabase.SaveAssets();
                    }
                }
                if (hasClassify)
                    AssetDatabase.CreateAsset(mainClothMenu, menuDir + "ClothMenu.asset");
            }

            /*** 生成配饰菜单 ***/
            var mainOrnamentMenu = CreateInstance<VRCExpressionsMenu>();
            if (ornamentInfoList.Count > 0)
            {
                var hasClassify = HasClassify(ornamentInfoList);
                // 归类
                var ornamentTypeMap = new Dictionary<string, List<OrnamentObjInfo>>();
                foreach (var info in ornamentInfoList)
                {
                    var type = info.type.Length == 0 ? "未分类" : info.type;
                    if (!ornamentTypeMap.ContainsKey(type))
                        ornamentTypeMap.Add(type, new List<OrnamentObjInfo>());
                    ornamentTypeMap[type].Add(info);
                }

                var nowWardrobeMenu = mainOrnamentMenu;
                var ornamentMenuMap = new Dictionary<string, VRCExpressionsMenu>();
                var mainOrnamentMenuIndex = 0;
                // 生成类型菜单
                foreach (var item in ornamentTypeMap)
                {
                    var name = item.Key;
                    var infoList = item.Value;


                    var menuList = new List<VRCExpressionsMenu>();
                    var nowOrnamentMenu = CreateInstance<VRCExpressionsMenu>();
                    AssetDatabase.CreateAsset(nowOrnamentMenu, menuDir + "OrnamentType_" + name + ".asset");
                    menuList.Add(nowOrnamentMenu);
                    ornamentMenuMap.Add(name, nowOrnamentMenu);

                    // 判断是否已分类
                    if (!hasClassify)
                        mainOrnamentMenu = nowOrnamentMenu;

                    if (nowWardrobeMenu.controls.Count == 7)
                    {
                        var newMenu = CreateInstance<VRCExpressionsMenu>();
                        AssetDatabase.CreateAsset(newMenu, menuDir + "OrnamentMenu_" + mainOrnamentMenuIndex++ + ".asset");
                        EditorUtility.SetDirty(newMenu);
                        nowWardrobeMenu.controls.Add(new VRCExpressionsMenu.Control
                        {
                            name = "下一页",
                            type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                            subMenu = newMenu
                        });
                        EditorUtility.SetDirty(nowWardrobeMenu);
                        AssetDatabase.SaveAssets();
                        menuList.Add(newMenu);
                        nowWardrobeMenu = newMenu;
                    }
                    nowWardrobeMenu.controls.Add(new VRCExpressionsMenu.Control
                    {
                        name = name,
                        type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                        subMenu = nowOrnamentMenu
                    });

                    // 生成配饰选项
                    foreach (var info in infoList)
                    {
                        if (nowOrnamentMenu.controls.Count == 7)
                        {
                            var newMenu = CreateInstance<VRCExpressionsMenu>();
                            AssetDatabase.CreateAsset(newMenu, menuDir + "OrnamentType_" + name + "_" + (menuList.Count) + ".asset");
                            EditorUtility.SetDirty(newMenu);
                            if (nowOrnamentMenu != null)
                            {
                                nowOrnamentMenu.controls.Add(new VRCExpressionsMenu.Control
                                {
                                    name = "下一页",
                                    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                                    subMenu = newMenu
                                });
                                EditorUtility.SetDirty(nowOrnamentMenu);
                                AssetDatabase.SaveAssets();
                            }
                            menuList.Add(newMenu);
                            nowOrnamentMenu = newMenu;
                        }

                        nowOrnamentMenu.controls.Add(new VRCExpressionsMenu.Control
                        {
                            name = info.name,
                            type = VRCExpressionsMenu.Control.ControlType.Toggle,
                            icon = info.image,
                            parameter = new VRCExpressionsMenu.Control.Parameter { name = "WardrobeParts_" + ornamentInfoList.IndexOf(info) },
                        });

                        EditorUtility.SetDirty(nowOrnamentMenu);
                        AssetDatabase.SaveAssets();
                    }
                }
                if (hasClassify)
                    AssetDatabase.CreateAsset(mainOrnamentMenu, menuDir + "OrnamentMenu.asset");
            }

            // 配置主菜单
            if (expressionsMenu == null)
                expressionsMenu = CreateInstance<VRCExpressionsMenu>();

            VRCExpressionsMenu.Control clothControl = null, ornamentControl = null;
            // 换装入口
            foreach (var control in expressionsMenu.controls)
            {
                if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu)
                {
                    if (control.name == "换装")
                        clothControl = control;
                    else if (control.name == "配饰")
                        ornamentControl = control;
                }
            }
            if (clothControl == null)
            {
                expressionsMenu.controls.Add(new VRCExpressionsMenu.Control
                {
                    name = "换装",
                    type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                    subMenu = mainClothMenu,
                });
            }
            else
            {
                clothControl.subMenu = mainClothMenu;
            }
            // 配饰入口
            if (ornamentInfoList.Count > 0)
            {
                if (ornamentControl == null)
                {
                    expressionsMenu.controls.Add(new VRCExpressionsMenu.Control
                    {
                        name = "配饰",
                        type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                        subMenu = mainOrnamentMenu,
                    });
                }
                else
                {
                    ornamentControl.subMenu = mainOrnamentMenu;
                }
            }

            if (AssetDatabase.GetAssetPath(expressionsMenu) == "")
                AssetDatabase.CreateAsset(expressionsMenu, dirPath + "ExpressionsMenu.asset");
            else
                EditorUtility.SetDirty(expressionsMenu);

            /*** 创建动画文件 ***/
            // 删除旧文件，并创建动画文件夹
            var animDir = dirPath + "Anim/Wardrobe";
            if (Directory.Exists(animDir))
                Directory.Delete(animDir, true);
            Directory.CreateDirectory(animDir);
            animDir += "/";

            // 导出所有衣服动画
            var clothAnimClipList = new List<AnimationClip>();
            for (var index = 0; index < clothInfoList.Count; index++)
            {
                var clip = GetClothAnimClip(clothInfoList, defaultBlendShapes, avatar, index);
                clothAnimClipList.Add(clip);
                AssetDatabase.CreateAsset(clip, animDir + "Cloth_" + clothInfoList[index].name + ".anim");
            }
            // 导出所有配饰动画
            var ornamentAnimClipList = new List<AnimationClip[]>();
            for (var index = 0; index < ornamentInfoList.Count; index++)
            {
                var clips = GetOrnamentAnimClip(ornamentInfoList, avatar, index);
                ornamentAnimClipList.Add(clips);
                AssetDatabase.CreateAsset(clips[0], animDir + "Ornament_" + ornamentInfoList[index].name + "_Hide.anim");
                AssetDatabase.CreateAsset(clips[1], animDir + "Ornament_" + ornamentInfoList[index].name + "_Show.anim");
                clips[0].name = "隐藏_" + ornamentInfoList[index].name;
                clips[1].name = "显示_" + ornamentInfoList[index].name;
            }

            /*** 修改FXLayer控制器 ***/
            AnimatorController fxController = null;
            var avatarDescriptor = avatar.GetComponent<VRCAvatarDescriptor>();
            avatarDescriptor.customizeAnimationLayers = true;
            for (var i = 0; i < avatarDescriptor.baseAnimationLayers.Length; i++)
            {
                var item = avatarDescriptor.baseAnimationLayers[i];
                if (item.type == VRCAvatarDescriptor.AnimLayerType.FX)
                {
                    fxController = item.animatorController as AnimatorController;
                    // 如果模型没有FX，那就创建一个
                    if (fxController == null)
                    {
                        fxController = new AnimatorController();
                        var stateMachine = new AnimatorStateMachine { name = "AllParts", hideFlags = HideFlags.HideInHierarchy };
                        var layer = new AnimatorControllerLayer
                        {
                            name = "AllParts",
                            defaultWeight = 1,
                            stateMachine = stateMachine
                        };
                        fxController.AddLayer(layer);
                        AssetDatabase.CreateAsset(fxController, dirPath + "FXLayer.controller");
                        AssetDatabase.AddObjectToAsset(stateMachine, AssetDatabase.GetAssetPath(fxController));
                        avatarDescriptor.baseAnimationLayers[i].animatorController = fxController;
                        avatarDescriptor.baseAnimationLayers[i].isEnabled = true;
                        avatarDescriptor.baseAnimationLayers[i].isDefault = false;
                    }
                    break;
                }
            }
            if (fxController == null)
            {
                EditorUtility.DisplayDialog("错误", "发生意料之外的情况，请重新设置 AvatarDescriptor 中的 Playable Layers 后再试！", "确认");
                return;
            }
            else
            {
                // 替换Parameter
                for (var i = 0; i < fxController.parameters.Length; i++)
                    if (fxController.parameters[i].name.StartsWith("Wardrobe"))
                    {
                        fxController.RemoveParameter(i);
                        i--;
                    }
                // 删除旧Layer
                for (var i = 0; i < fxController.layers.Length; i++)
                    if (fxController.layers[i].name.StartsWith("Wardrobe"))
                    {
                        fxController.RemoveLayer(i);
                        i--;
                    }
                // 换装Layer
                {
                    fxController.AddParameter(new AnimatorControllerParameter()
                    {
                        name = "Wardrobe_Int1",
                        type = AnimatorControllerParameterType.Int,
                        defaultInt = defaultClothIndex
                    });
                    var stateMachine = new AnimatorStateMachine()
                    {
                        name = "WardrobeCloth",
                        hideFlags = HideFlags.HideInHierarchy
                    };
                    AssetDatabase.AddObjectToAsset(stateMachine, AssetDatabase.GetAssetPath(fxController)); // 必须放这，我也不知道为什么
                    stateMachine.defaultState = stateMachine.AddState("Idle");
                    for (var index = 0; index < clothAnimClipList.Count; index++)
                    {
                        var clip = clothAnimClipList[index];
                        var state = stateMachine.AddState(clip.name);
                        state.motion = clip;

                        var tran = stateMachine.AddAnyStateTransition(state);
                        tran.duration = 0;
                        tran.AddCondition(AnimatorConditionMode.Equals, index, "Wardrobe_Int1");
                    }
                    var layer = new AnimatorControllerLayer
                    {
                        name = stateMachine.name,
                        defaultWeight = 1f,
                        stateMachine = stateMachine
                    };
                    fxController.AddLayer(layer);
                }
                // 配饰Layer
                for (var index = 0; index < ornamentInfoList.Count; index++)
                {
                    var info = ornamentInfoList[index];

                    fxController.AddParameter(new AnimatorControllerParameter()
                    {
                        name = "WardrobeParts_" + index,
                        type = AnimatorControllerParameterType.Bool,
                        defaultBool = info.isShow
                    });

                    var stateMachine = new AnimatorStateMachine()
                    {
                        name = "WardrobeParts_" + info.name,
                        hideFlags = HideFlags.HideInHierarchy
                    };
                    AssetDatabase.AddObjectToAsset(stateMachine, AssetDatabase.GetAssetPath(fxController));

                    var clips = ornamentAnimClipList[index];
                    for (var show = 0; show <= 1; show++)
                    {
                        var state = stateMachine.AddState(clips[show].name);
                        state.motion = clips[show];
                        state.writeDefaultValues = true;

                        if (Convert.ToInt32(info.isShow) == show)
                            stateMachine.defaultState = state;

                        var tran = stateMachine.AddAnyStateTransition(state);
                        tran.duration = 0;
                        tran.AddCondition(show == 1 ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, "WardrobeParts_" + index);
                    }

                    var layer = new AnimatorControllerLayer
                    {
                        name = stateMachine.name,
                        defaultWeight = 1f,
                        stateMachine = stateMachine
                    };
                    fxController.AddLayer(layer);
                }
            }

            /*** 应用修改 ***/
            EditorUtility.SetDirty(expressionsMenu);
            EditorUtility.SetDirty(expressionParameters);
            descriptor.customExpressions = true;
            descriptor.expressionParameters = expressionParameters;
            descriptor.expressionsMenu = expressionsMenu;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("提醒", "配置成功，快上传模型测试下效果吧~", "确认");
        }
    }
}
#endif