#if UNITY_EDITOR && VRC_SDK_VRCSDK3
using Codice.Client.BaseCommands;
using nadena.dev.modular_avatar.core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.PackageManagement.Core;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using static VRC.Core.ApiVRChatProductDetails;
using static VRChatAvatarToolkit.AvatarWardrobeParameter;
using static VRChatAvatarToolkit.MoyuToolkitUtils;

namespace VRChatAvatarToolkit
{
    public class AvatarWardrobe : AvatarWardrobeUtils
    {
        private Vector2 scrollPos;
        protected SerializedObject serializedObject;
        private int tabIndex = 0;

        private GameObject avatar;
        private AvatarWardrobeParameter parameter;
        private string avatarId;

        public List<BlendShapePack> defaultBlendShapes = new List<BlendShapePack>();
        // 衣服
        public List<ClothObjInfo> clothInfoList = new List<ClothObjInfo>();
        private int defaultClothIndex = -1;

        // 配饰
        public List<OrnamentObjInfo> ornamentInfoList = new List<OrnamentObjInfo>();
        // 互斥饰品
        public List<MutualExclusionObjInfo> mutualExclusionList = new List<MutualExclusionObjInfo>();

        private void OnEnable()
        {
            serializedObject = new SerializedObject(this);
            foreach (var info in clothInfoList)
            {
                info.animBool.valueChanged.RemoveAllListeners();
                info.animBool.valueChanged.AddListener(Repaint);
            }
            foreach (var info in ornamentInfoList)
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
            GUILayout.Label("我的衣柜");
            GUI.skin.label.fontSize = 12;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label("by:如梦、千岁琉璃");
            GUILayout.Space(10);
            GUI.skin.label.fontSize = 12;
            GUI.skin.label.alignment = TextAnchor.MiddleCenter;
            GUILayout.Label("轻松管理多套衣服，让你成为街上最靓的崽");
            GUILayout.Space(10);

            var newAvatar = (GameObject)EditorGUILayout.ObjectField("选择模型：", avatar, typeof(GameObject), true);
            if (!newAvatar && !avatar)
            {
                var rootObjs = SceneManager.GetActiveScene().GetRootGameObjects();
                foreach (var obj in rootObjs)
                {
                    if (obj.GetComponent<Animator>())
                    {
                        newAvatar = obj;
                        break;
                    }
                }
            }
            if (avatar != newAvatar)
            {
                avatar = newAvatar;
                tabIndex = 0;
                if (newAvatar != null && newAvatar.GetComponent<VRCAvatarDescriptor>() == null)
                {
                    avatar = null;
                    EditorUtility.DisplayDialog("提醒", "本插件仅供SDK3模型使用！", "确认");
                }
                avatarId = GetAvatarId(avatar);
                parameter = GetAvatarWardrobeParameter(avatarId);
                ReadParameter();
            }
            if (avatar == null)
            {
                GUILayout.Space(10);
                EditorGUILayout.HelpBox("请先选择一个模型", MessageType.Info);
            }
           
            GUILayout.Space(10);
            if (parameter == null && GUILayout.Button("创建衣柜"))
            {
                parameter = CreateAvatarWardrobeParameter(avatar);
                ReadParameter();
            }
            else if (avatar && parameter)
            {
                tabIndex = GUILayout.Toolbar(tabIndex, new[] { "衣服", "配饰", "互斥配饰"});
                GUILayout.Space(5);
                switch (tabIndex)
                {
                    case 0:
                        {
                            OnGUI_DefaultValue();
                            OnGUI_Cloth();
                        }break;
                    case 1: 
                        {
                            OnGUI_Ornament();
                        }break;
                    case 2:
                        {
                            OnGUI_MutualExclusion();
                        }break;
                }

                GUILayout.Space(5);

                //下操作栏
                GUILayout.Space(10);
                GUILayout.Label("操作菜单");
                GUILayout.Space(5);
                GUILayout.BeginHorizontal();

                if (GUILayout.Button("一键应用到模型"))
                    ApplyToAvatar(avatar, defaultBlendShapes, clothInfoList, defaultClothIndex, ornamentInfoList);

                GUILayout.EndHorizontal();
                GUILayout.Space(10);
            }
        }

        private void OnGUI_DefaultValue()
        {
            GUILayout.Label("BlendShape默认值");
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("defaultBlendShapes"), new GUIContent("BlendShape默认值"));
            CheckAndSave();
        }

        private void OnGUI_Cloth()
        {
            GUILayout.Label("衣柜");
            scrollPos = GUILayout.BeginScrollView(scrollPos);
            var sum = clothInfoList.Count;
            if (sum == 0)
            {
                EditorGUILayout.HelpBox("当前衣服列表为空，先点击下面按钮添加一套吧", MessageType.Info);
            }
            else
            {
                serializedObject.Update();
                var clothNameList = new List<string>();
                foreach (var info in clothInfoList)
                    clothNameList.Add(info.name);
                // 遍历衣服信息
                EditorGUI.BeginChangeCheck();
                var classify = HasClassify(clothInfoList);
                for (var index = 0; index < sum; index++)
                {
                    var info = clothInfoList[index];
                    var clothName = (classify ? "【" + (info.type.Length > 0 ? info.type : "未分类") + "】" : "");
                    clothName += info.name + (defaultClothIndex == index ? "（默认）" : "");
                    var newTarget = EditorGUILayout.Foldout(info.animBool.target, clothName, true);
                    if (newTarget != info.animBool.target)
                    {
                        if (newTarget)
                            foreach (var _info in clothInfoList)
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

                        //sd内容
                        EditorGUILayout.BeginHorizontal();
                        info.image = (Texture2D)EditorGUILayout.ObjectField("", info.image, typeof(Texture2D), true, GUILayout.Width(60), GUILayout.Height(60));
                        GUILayout.Space(5);
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();

                        //操作按钮
                        if (index > 0 && GUILayout.Button("上移", GUILayout.Width(60)))
                        {
                            MoveListItem(ref clothInfoList, index, index - 1);
                            if (defaultClothIndex == index)
                                defaultClothIndex--;
                            else if (defaultClothIndex == index - 1)
                                defaultClothIndex++;
                            break;
                        }
                        else if (index < clothInfoList.Count - 1 && GUILayout.Button("下移", GUILayout.Width(60)))
                        {
                            MoveListItem(ref clothInfoList, index, index + 1);
                            if (defaultClothIndex == index)
                                defaultClothIndex++;
                            else if (defaultClothIndex == index + 1)
                                defaultClothIndex--;
                            break;
                        }
                        if (GUILayout.Button("删除", GUILayout.Width(60)))
                        {
                            DelCloth(index);
                            break;
                        }
                        if (GUILayout.Button("预览", GUILayout.Width(60)))
                        {
                            defaultClothIndex = index;
                            PrviewCloth(avatar, clothInfoList, defaultBlendShapes, index);
                            break;
                        }

                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.BeginHorizontal();
                        //唯一衣服名
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.LabelField("衣服名称", GUILayout.Width(55));
                        var newName = EditorGUILayout.TextField(info.name).Trim();
                        if (!clothNameList.Contains(newName) && newName != "")
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
                        var clothObjectInfoProperty = serializedObject.FindProperty("clothInfoList").GetArrayElementAtIndex(index);
                        EditorGUILayout.PropertyField(clothObjectInfoProperty.FindPropertyRelative("showObjectList"), new GUIContent("衣服元素"));
                        EditorGUILayout.PropertyField(clothObjectInfoProperty.FindPropertyRelative("hideObjectList"), new GUIContent("额外隐藏"));
                        EditorGUILayout.PropertyField(clothObjectInfoProperty.FindPropertyRelative("blendShapePacks"), new GUIContent("身体参数"));
                        if (GUILayout.Button("一键获取身体参数"))
                        {
                            GetCurBlendShape(index);
                        }

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
                CheckAndSave();
            }
            GUILayout.EndScrollView();
            if (CanAdd() && GUILayout.Button("添加衣服"))
                AddCloth();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("根据衣服自动添加MASync和动骨"))
                AutoFixAll();
            if (GUILayout.Button("删除所有的MASync"))
                ClearBlendShapeBindings();
            GUILayout.EndHorizontal();
            if (GUILayout.Button("重置形态"))
                ResetBlendShape();
        }
        private void OnGUI_Ornament()
        {
            scrollPos = GUILayout.BeginScrollView(scrollPos);

            var sum = ornamentInfoList.Count;
            if (sum == 0)
            {
                EditorGUILayout.HelpBox("当前配饰列表为空，先点击下面按钮添加一件吧", MessageType.Info);
            }
            else
            {
                serializedObject.Update();
                var clothNameList = new List<string>();
                foreach (var info in ornamentInfoList)
                    clothNameList.Add(info.name);
                // 遍历配饰信息
                EditorGUI.BeginChangeCheck();
                var classify = HasClassify(ornamentInfoList);
                for (var index = 0; index < sum; index++)
                {
                    var info = ornamentInfoList[index];
                    var name = (classify ? "【" + (info.type.Length > 0 ? info.type : "未分类") + "】" : "");
                    name += info.name + (info.isShow ? "（显示）" : "（隐藏）");
                    var newTarget = EditorGUILayout.Foldout(info.animBool.target, name, true);
                    if (newTarget != info.animBool.target)
                    {
                        if (newTarget)
                            foreach (var _info in ornamentInfoList)
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
                            MoveListItem(ref ornamentInfoList, index, index - 1);
                            break;
                        }
                        else if (index < ornamentInfoList.Count - 1 && GUILayout.Button("下移", GUILayout.Width(60)))
                        {
                            MoveListItem(ref ornamentInfoList, index, index + 1);
                            break;
                        }
                        if (GUILayout.Button("删除", GUILayout.Width(60)))
                        {
                            DelOrnament(index);
                            break;
                        }
                        if (GUILayout.Button(info.isShow ? "隐藏" : "显示", GUILayout.Width(60)))
                        {
                            info.isShow = !info.isShow;
                            foreach (var obj in info.objectList)
                                obj.SetActive(info.isShow);
                        }
                        EditorGUILayout.EndHorizontal();


                        EditorGUILayout.BeginHorizontal();
                        //唯一衣服名
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.LabelField("衣服名称", GUILayout.Width(55));
                        var newName = EditorGUILayout.TextField(info.name).Trim();
                        if (!clothNameList.Contains(newName) && newName.Length > 0)
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
                        var clothObjectInfoProperty = serializedObject.FindProperty("ornamentInfoList").GetArrayElementAtIndex(index);
                        EditorGUILayout.PropertyField(clothObjectInfoProperty.FindPropertyRelative("objectList"), new GUIContent("元素"));

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
                CheckAndSave();
            }

            GUILayout.EndScrollView();
            if (CanAdd() && GUILayout.Button("添加配饰"))
                AddOrnament();
        }
       
        private void OnGUI_MutualExclusion()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            for (int index = 0, max = mutualExclusionList.Count; index < max; ++index)
            {
                var info = mutualExclusionList[index];
                var clothName = info.name + (defaultClothIndex == index ? "（默认）" : "");
                var newTarget = EditorGUILayout.Foldout(info.animBool.target, clothName, true);
                if (newTarget != info.animBool.target)
                {
                    if (newTarget)
                        foreach (var _info in clothInfoList)
                            _info.animBool.target = false;
                    info.animBool.target = newTarget;
                }

                if (EditorGUILayout.BeginFadeGroup(info.animBool.faded))
                { 
                    var mutualExclusionListProperty = serializedObject.FindProperty("mutualExclusionList").GetArrayElementAtIndex(index);

                    // 互斥组信息显示
                    EditorGUILayout.BeginHorizontal();

                    info.image = (Texture2D)EditorGUILayout.ObjectField("", info.image, typeof(Texture2D), true, GUILayout.Width(60), GUILayout.Height(60));
                    GUILayout.Space(5);

                    EditorGUILayout.BeginVertical();

                    EditorGUILayout.BeginHorizontal();

                    GUILayout.FlexibleSpace();
                    if (index > 0 && GUILayout.Button("上移", GUILayout.Width(60)))
                    {
                        MoveListItem(ref mutualExclusionList, index, index - 1);
                        break;
                    }
                    else if (index < mutualExclusionList.Count - 1 && GUILayout.Button("下移", GUILayout.Width(60)))
                    {
                        MoveListItem(ref mutualExclusionList, index, index + 1);
                        break;
                    }
                    if (GUILayout.Button("删除", GUILayout.Width(60)))
                    {
                        DelListData(ref mutualExclusionList, index, "真的要删除这个互斥组吗？");
                        break;
                    }

                    EditorGUILayout.EndHorizontal();

                    var mutualExclusionGroup = mutualExclusionList[index];
                    EditorGUILayout.PropertyField(mutualExclusionListProperty.FindPropertyRelative("name"), new GUIContent(""));
                    if (GUILayout.Button("新增互斥元素"))
                    {
                        mutualExclusionGroup.mutualExclusions.Add(new());
                    }

                    EditorGUILayout.EndVertical();

                    EditorGUILayout.EndHorizontal();

                    // 显示互斥组里面的元素
                    EditorGUILayout.BeginHorizontal();

                    GUILayout.Space(5);

                    EditorGUILayout.BeginVertical();

                    EditorGUILayout.PropertyField(mutualExclusionListProperty.FindPropertyRelative("mutualExclusions"), new GUIContent("互斥列表"));

                    EditorGUILayout.EndVertical();

                    EditorGUILayout.EndHorizontal();

                    //var mutualExclusions = mutualExclusionListProperty.FindPropertyRelative("mutualExclusions");
                    //for (int mutalIndex = 0, count = mutualExclusionGroup.mutualExclusions.Count; mutalIndex < count; ++mutalIndex)
                    //{
                    //    var mutualExclusion = mutualExclusions.GetArrayElementAtIndex(mutalIndex);
                    //    var mutualInfo = mutualExclusionGroup.mutualExclusions[mutalIndex];

                    //    // 样式嵌套Start
                    //    EditorGUILayout.BeginHorizontal();

                    //    mutualInfo.menuImage = (Texture2D)EditorGUILayout.ObjectField("", mutualInfo.menuImage, typeof(Texture2D), true, GUILayout.Width(60), GUILayout.Height(60));
                    //    GUILayout.Space(5);

                    //    EditorGUILayout.BeginVertical();

                    //    EditorGUILayout.BeginHorizontal();

                    //    GUILayout.FlexibleSpace();
                    //    if (index > 0 && GUILayout.Button("上移", GUILayout.Width(60)))
                    //    {
                    //        MoveListItem(ref mutualExclusionGroup.mutualExclusions, index, index - 1);
                    //        break;
                    //    }
                    //    else if (index < mutualExclusionGroup.mutualExclusions.Count - 1 && GUILayout.Button("下移", GUILayout.Width(60)))
                    //    {
                    //        MoveListItem(ref mutualExclusionGroup.mutualExclusions, index, index + 1);
                    //        break;
                    //    }
                    //    if (GUILayout.Button("删除", GUILayout.Width(60)))
                    //    {
                    //        DelListData(ref mutualExclusionGroup.mutualExclusions, index, "真的要删除这件配饰吗？");
                    //        break;
                    //    }

                    //    EditorGUILayout.EndVertical();

                    //    EditorGUILayout.EndHorizontal();

                    //    EditorGUILayout.EndHorizontal();

                    //    EditorGUILayout.LabelField("饰品名称");
                    //    EditorGUILayout.PropertyField(mutualExclusion.FindPropertyRelative("name"), new GUIContent(""));

                    //    EditorGUILayout.EndVertical();

                    //    EditorGUILayout.EndHorizontal();
                    //}

                }
                EditorGUILayout.EndFadeGroup();
            }


            if (GUILayout.Button("添加互斥饰品组"))
            {
                mutualExclusionList.Add(new());
            }

            CheckAndSave();
        }

        private bool CanAdd()
        {
            return (clothInfoList.Count + ornamentInfoList.Count) < maxClothNum;
        }

        private void ClearBlendShapeBindings()
        {
            if (!EditorUtility.DisplayDialog("真的要删除所有衣服的MABlendShapeSync吗？", "一般而言，这个按钮只应该在配置错误时使用", "确认", "取消"))
                return;

            foreach (var clothInfo in clothInfoList)
            {
                foreach(var ma in clothInfo.showObjectList[0].GetComponentsInChildren<ModularAvatarBlendshapeSync>())
                {
                    DestroyImmediate(ma);
                }
            }
        }

        private void DefaultBlendShapePreFix()
        {
            const string SPLIT_CHAR = "|";  // 随便挑一个不可能用来做名字的符号
            HashSet<string> defaultBlendShapeSet = new();
            foreach(var pack in defaultBlendShapes)
            {
                defaultBlendShapeSet.Add($"{pack.path}{SPLIT_CHAR}{pack.name}");
            }

            HashSet<string> customBlendShapeSet = new();
            foreach(var clothInfo in clothInfoList)
            {
                if (clothInfo == null || clothInfo.blendShapePacks == null)
                    continue;

                foreach(var pack in clothInfo.blendShapePacks)
                {
                    customBlendShapeSet.Add($"{pack.path}{SPLIT_CHAR}{pack.name}");
                }
            }

            List<string> delDefaultList = new();
            foreach (var defaultV in defaultBlendShapeSet)
            {
                if (!customBlendShapeSet.Contains(defaultV))
                {
                    delDefaultList.Add(defaultV);
                }
            }

            foreach(var defaultV in delDefaultList)
            {
                var pack = defaultV.Split(SPLIT_CHAR);
                defaultBlendShapeSet.Remove(defaultV);
                foreach (var defaultPack in defaultBlendShapes)
                {
                    if (defaultPack.path == pack[0] && defaultPack.name == pack[1])
                    {
                        defaultBlendShapes.Remove(defaultPack);
                        break;
                    }
                }
            }

            foreach(var customV in customBlendShapeSet)
            {
                if (!defaultBlendShapeSet.Contains(customV))
                {
                    var pack = customV.Split(SPLIT_CHAR);
                    // 这里一定有两个元素
                    defaultBlendShapes.Add(new(pack[0], pack[1], 0));
                }   
            }
        }

        private void GetCurBlendShape(int index)
        {
            var clothInfo = clothInfoList[index];
            clothInfo.blendShapePacks.Clear();

            foreach (Transform t in avatar.transform)
            {
                SkinnedMeshRenderer renderer = t.GetComponent<SkinnedMeshRenderer>();
                if (!renderer)
                {
                    continue;
                }

                for (int i = 0, max = renderer.sharedMesh.blendShapeCount; i < max; i++)
                {
                    float weight = renderer.GetBlendShapeWeight(i);
                    if (weight > 0)
                    {
                        clothInfo.blendShapePacks.Add(new(t.gameObject.name, renderer.sharedMesh.GetBlendShapeName(i), weight));
                    }
                }
            }
        }

        private void ResetBlendShape()
        {
            foreach (Transform t in avatar.transform)
            {
                SkinnedMeshRenderer renderer = t.GetComponent<SkinnedMeshRenderer>();
                if (!renderer)
                {
                    continue;
                }

                for (int i = 0, max = renderer.sharedMesh.blendShapeCount; i < max; i++)
                {
                    renderer.SetBlendShapeWeight(i, 0);
                }
            }
        }

        private void AutoFixAll()
        {
            AutoAddAllDynamicBones(); // 这个要放第一，收集所有动骨
            AutoFixBreast();
            AutoFixMABlendShapeSync();
            AutoFixAnchorOverride();

            CheckAndSave();
        }

        private void AutoAddAllDynamicBones()
        {
            for (int index = 0; index < clothInfoList.Count; ++index)
            {
                if (index >= clothInfoList.Count)
                {
                    return;
                }

                var clothData = clothInfoList[index];
                var showList = clothData.showObjectList;
                if (clothData.showObjectList.Count <= 0 || clothData.showObjectList[0] == null)
                {
                    return;
                }

                var clothName = $"({showList[0].name})";
                List<GameObject> dynamicBonesList = new List<GameObject>();
                FindObjectsByNameRecursive(avatar.transform, clothName, dynamicBonesList);
                showList.RemoveRange(1, showList.Count - 1);
                foreach (var dynamicBone in dynamicBonesList)
                {
                    if (dynamicBone != showList[0])
                    {
                        showList.Add(dynamicBone);
                    }
                }
            }
        }

        private void AutoFixBreast()
        {
            string[] nameList = {"Breast_L", "Breast_R" };

            Dictionary<string, GameObject> breastMap = new Dictionary<string, GameObject>();
            // 找到avatar的胸的位置
            foreach(var name in nameList)
            {
                List<GameObject> outList = new List<GameObject>();
                FindObjectsByNameRecursive(avatar.transform, name, outList);
                breastMap[name] = outList[0];
            }

            // 把衣服的胸放到模型的胸上
            foreach (var clothInfo in clothInfoList)
            {
                if (clothInfo.showObjectList.Count <= 0 || clothInfo.showObjectList[0] == null)
                {
                    continue;
                }

                var clothSubName = $"({clothInfo.showObjectList[0].name})";
                List<GameObject> extraAddList = new List<GameObject>();
                foreach (var boneInfo in clothInfo.showObjectList)
                {
                    if (boneInfo.name.StartsWith("Chest"))
                    {
                        List<GameObject> breatList = new List<GameObject>();
                        foreach (var name in nameList)
                        {
                            var result = FindObjectByNameRecursive(boneInfo.transform, name);
                            if (result)
                            {
                                breatList.Add(result);
                            }
                        }

                        foreach (var breastInfo in breatList)
                        {
                            breastInfo.transform.SetParent(breastMap[breastInfo.name].transform, true);
                            breastInfo.name += clothSubName;
                            extraAddList.Add(breastInfo);              
                        }
                    }
                }

                foreach(var extraInfo in extraAddList)
                {
                    if (!clothInfo.showObjectList.Contains(extraInfo.gameObject))
                    {
                        clothInfo.showObjectList.Add(extraInfo.gameObject);
                    }
                }
            }

            // 模型的胸添加衣服的忽略
            foreach (var breast in breastMap.Values)
            {
                var vpb = breast.GetComponent<VRCPhysBone>();
                foreach (Transform child in breast.transform)
                {
                    if (child.name == $"{breast.name}.001")
                    {
                        continue;
                    }

                    if (!vpb.ignoreTransforms.Contains(child))
                    {
                        vpb.ignoreTransforms.Add(child);
                    }
                }

                vpb.ignoreTransforms.Sort((l, r) =>
                {
                    return !l ? 1 : (!r ? -1 : 0);
                });
                for(int i = 0, count = vpb.ignoreTransforms.Count; i < count; i++)
                {
                    if (!vpb.ignoreTransforms[i])
                    {
                        vpb.ignoreTransforms.RemoveRange(i, count - i);
                        break;
                    }
                }
            }
        }

        private void AutoFixMABlendShapeSync()
        {
            // 建立body的名字映射
            Dictionary<GameObject, Dictionary<string, int>> bodyMeshRenderMap = new();
            Dictionary<GameObject, Dictionary<string, int>> bodyMeshCheckNameSetMap = new();
            foreach (Transform t in avatar.transform)
            {
                var bodyMeshRender = t.GetComponent<SkinnedMeshRenderer>();
                if (bodyMeshRender && bodyMeshRender.sharedMesh.blendShapeCount > 0)
                {
                    Dictionary<string, int> bodyBlendShapeNameMap = new();
                    bodyMeshRenderMap[bodyMeshRender.gameObject] = bodyBlendShapeNameMap;

                    Dictionary<string, int> checkNameSet = new();
                    bodyMeshCheckNameSetMap[bodyMeshRender.gameObject] = checkNameSet;

                    for (int i = 0; i < bodyMeshRender.sharedMesh.blendShapeCount; i++)
                    {
                        var blendShapeName = bodyMeshRender.sharedMesh.GetBlendShapeName(i);
                        bodyBlendShapeNameMap[blendShapeName] = i;
                        checkNameSet[blendShapeName.ToLower()] = i;
                    }
                }
            }

            foreach (var clothInfo in clothInfoList)
            {
                if (clothInfo.showObjectList.Count <= 0 || clothInfo.showObjectList[0] == null)
                {
                    continue;
                }

                var cloth = clothInfo.showObjectList[0];
                foreach (var clothMeshRender in cloth.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var maShapeSync = clothMeshRender.gameObject.GetComponent<ModularAvatarBlendshapeSync>();
                    // 跳过已经生成了的
                    if (maShapeSync)
                    {
                        //continue;
                    }
                    else
                    {
                        maShapeSync = clothMeshRender.gameObject.AddComponent<ModularAvatarBlendshapeSync>();
                    }


                    if (maShapeSync.Bindings == null)
                    {
                        maShapeSync.Bindings = new List<BlendshapeBinding>();
                    }

                    HashSet<string> existBindings = new HashSet<string>();
                    foreach (var blendShape in  maShapeSync.Bindings)
                    {
                        existBindings.Add(blendShape.Blendshape);
                    }

                    // 生成cloth的名字映射
                    Dictionary<string, int> clothBlendShapeNameMap = new Dictionary<string, int>();
                    for (int i = 0; i < clothMeshRender.sharedMesh.blendShapeCount; ++i)
                    {
                        var blendShapeName = clothMeshRender.sharedMesh.GetBlendShapeName(i);
                        clothBlendShapeNameMap[blendShapeName] = i;
                    }

                    if (clothBlendShapeNameMap.Count == 0)
                    {
                        DestroyImmediate(maShapeSync);
                        continue;
                    }

                    foreach (var clothShapeNamePair in clothBlendShapeNameMap)
                    {
                        string checkName = clothShapeNamePair.Key.ToLower();
                        foreach (var pair in bodyMeshRenderMap)
                        {
                            if (bodyMeshCheckNameSetMap[pair.Key].ContainsKey(checkName) && !existBindings.Contains(clothShapeNamePair.Key))
                            {
                                existBindings.Add(clothShapeNamePair.Key);
                                BlendshapeBinding bodyBinding = new BlendshapeBinding();
                                bodyBinding.Blendshape = pair.Value.FindFirstKeyByValue(bodyMeshCheckNameSetMap[pair.Key][checkName]);
                                bodyBinding.LocalBlendshape = clothShapeNamePair.Key;
                                bodyBinding.ReferenceMesh = new AvatarObjectReference();
                                bodyBinding.ReferenceMesh.Set(pair.Key);
                                maShapeSync.Bindings.Add(bodyBinding);
                            }
                        }
                    }

                    if (maShapeSync.Bindings.Count > 0)
                    {
                        maShapeSync.ValidateRebind();
                    }
                    else
                    {
                        DestroyImmediate(maShapeSync);
                    }
                }
            }
        }

        private void AutoFixAnchorOverride()
        {
            var chest = FindObjectByNameRecursive(avatar.transform, "Chest");
            if (chest == null)
            {
                return;
            }

            foreach (var clothInfo in clothInfoList)
            {
                if (clothInfo.showObjectList.Count <= 0 || clothInfo.showObjectList[0] == null)
                {
                    continue;
                }

                foreach (var render in clothInfo.showObjectList[0].GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!render.probeAnchor)
                    {
                        render.probeAnchor = chest.transform;
                    }
                }
            }
        }
        private void FindObjectsByNameRecursive(Transform parent, string clothName, List<GameObject> outList)
        {
            // 遍历该父物体的所有子物体
            foreach (Transform child in parent)
            {
                // 检查名称是否包含
                if (child.name.Contains(clothName))
                {
                    outList.Add(child.gameObject);
                }

                // 递归查找
                FindObjectsByNameRecursive(child, clothName, outList);
            }
        }

        private GameObject FindObjectByNameRecursive(Transform parent, string clothName)
        {
            // 遍历该父物体的所有子物体
            foreach (Transform child in parent)
            {
                // 检查名称是否包含
                if (child.name.Contains(clothName))
                {
                    return child.gameObject;
                }

                // 递归查找
                var result = FindObjectByNameRecursive(child, clothName);
                if (result)
                {
                    return result;
                }
            }

            return null;
        }

        // 添加一套衣服
        private void AddCloth()
        {
            foreach (var info in clothInfoList)
                info.animBool.target = false;
            var name = "衣服" + (clothInfoList.Count + 1).ToString();
            var clothObjectInfo = new ClothObjInfo(name);
            clothObjectInfo.animBool.valueChanged.AddListener(Repaint);
            clothObjectInfo.animBool.target = true;
            clothInfoList.Add(clothObjectInfo);
            WriteParameter();
        }

        // 删除一套衣服
        private void DelCloth(int index)
        {
            if (!EditorUtility.DisplayDialog("注意", "真的要删除这套衣服吗？", "确认", "取消"))
                return;
            if (index == defaultClothIndex)
            {
                if (index > 0)
                    defaultClothIndex = 0;
                else if (clothInfoList.Count > 1)
                    defaultClothIndex = 1;
                else
                    defaultClothIndex = -1;
                PrviewCloth(avatar, clothInfoList, defaultBlendShapes, defaultClothIndex);
            }
            clothInfoList.RemoveAt(index);
            WriteParameter();
        }
        // 添加一件配饰
        private void AddOrnament()
        {
            foreach (var info in ornamentInfoList)
                info.animBool.target = false;
            var name = "配饰" + (ornamentInfoList.Count + 1).ToString();
            var objInfo = new OrnamentObjInfo(name);
            objInfo.animBool.valueChanged.AddListener(Repaint);
            objInfo.animBool.target = true;
            ornamentInfoList.Add(objInfo);
            WriteParameter();
        }
        //删除一件配饰
        private void DelOrnament(int index)
        {
            if (!EditorUtility.DisplayDialog("注意", "真的要删除这件配饰吗？", "确认", "取消"))
                return;
            ornamentInfoList.RemoveAt(index);
            WriteParameter();
        }

        private void DelListData<T>(ref List<T> listData, int index, string info)
        {
            if (!EditorUtility.DisplayDialog("注意", info, "确认", "取消"))
                return;

            listData.RemoveAt(index);
            WriteParameter();
        }

        // 从文件中读取参数
        private void ReadParameter()
        {
            defaultClothIndex = -1;
            clothInfoList.Clear();
            ornamentInfoList.Clear();
            defaultBlendShapes.Clear();
            if (parameter == null)
                return;

            defaultBlendShapes = parameter.defaultBlendShapes;
            foreach (var info in parameter.clothList)
            {
                var clothObjectInfo = new ClothObjInfo
                {
                    name = info.name,
                    image = info.menuImage,
                    type = info.type ?? "",
                    blendShapePacks = info.blendShapePacks,
                };

                clothObjectInfo.blendShapePacks.Sort((l, r) =>
                {
                    return l.name.CompareTo(r.name);
                });

                clothObjectInfo.animBool.valueChanged.AddListener(Repaint);
                foreach (var showPath in info.showPaths)
                {
                    var transform = avatar.transform.Find(showPath);
                    if (transform != null)
                        clothObjectInfo.showObjectList.Add(transform.gameObject);
                }
                foreach (var hidePath in info.hidePaths)
                {
                    var transform = avatar.transform.Find(hidePath);
                    if (transform != null)
                        clothObjectInfo.hideObjectList.Add(transform.gameObject);
                }
                clothInfoList.Add(clothObjectInfo);
            }
            defaultClothIndex = parameter.defaultClothIndex;
            foreach (var info in parameter.ornamentList)
            {
                var ornamentObjInfo = new OrnamentObjInfo
                {
                    name = info.name,
                    isShow = info.isShow,
                    image = info.menuImage,
                    type = info.type ?? ""
                };
                ornamentObjInfo.animBool.valueChanged.AddListener(Repaint);
                foreach (var showPath in info.itemPaths)
                {
                    var transform = avatar.transform.Find(showPath);
                    if (transform != null)
                        ornamentObjInfo.objectList.Add(transform.gameObject);
                }
                ornamentInfoList.Add(ornamentObjInfo);
            }
        }

        // 保存参数到文件
        private void WriteParameter()
        {
            if (avatar == null || parameter == null)
                return;
            var parentPath = avatar.transform.GetHierarchyPath() + "/";
            parameter.defaultBlendShapes = defaultBlendShapes;
            // 检查冲突项
            // 如果元素不在模型范围内则移除
            // 如果元素属于某件衣服，则不允许添加进其他衣服的隐藏元素
            var clothItemList = new List<GameObject>();
            foreach (var info in clothInfoList)
            {
                for (var i = 0; i < info.showObjectList.Count; i++)
                {
                    var obj = info.showObjectList[i];
                    if (obj == null)
                        continue;
                    var path = obj.transform.GetHierarchyPath();
                    if (!path.StartsWith(parentPath))
                    {
                        Debug.LogWarning("【衣柜】" + obj.name + "(" + path + ")不在模型目录下，已自动移除！");
                        info.showObjectList[i] = null;
                        continue;
                    }
                    if (!clothItemList.Contains(obj))
                        clothItemList.Add(obj);
                }
                for (var i = 0; i < info.hideObjectList.Count; i++)
                {
                    var obj = info.hideObjectList[i];
                    if (obj == null)
                        continue;
                    var path = obj.transform.GetHierarchyPath();
                    if (!path.StartsWith(parentPath))
                    {
                        Debug.LogWarning("【衣柜】" + obj.name + "(" + path + ")不在模型目录下，已自动移除！");
                        info.hideObjectList[i] = null;
                        continue;
                    }
                    if (clothItemList.Contains(obj))
                    {
                        Debug.Log("【衣柜】" + obj.name + "(" + path + ")元素属于某件衣服，不需要再添加进额外隐藏中！");
                        info.hideObjectList[i] = null;
                    }
                }

            }
            // 将GameObject转换为path保存
            var clothList = new List<AvatarWardrobeParameter.ClothInfo>();
            foreach (var info in clothInfoList)
            {
                var clothInfo = new AvatarWardrobeParameter.ClothInfo { name = info.name };
                clothInfo.menuImage = info.image;
                clothInfo.type = info.type;
                for (var i = 0; i < info.showObjectList.Count; i++)
                {
                    var obj = info.showObjectList[i];
                    if (obj == null)
                        continue;
                    var path = obj.transform.GetHierarchyPath();
                    path = path.Substring(parentPath.Length);
                    if (clothInfo.showPaths.Contains(path))
                        info.showObjectList[i] = null;
                    else
                        clothInfo.showPaths.Add(path);
                }
                for (var i = 0; i < info.hideObjectList.Count; i++)
                {
                    var obj = info.hideObjectList[i];
                    if (obj == null)
                        continue;
                    var path = obj.transform.GetHierarchyPath();
                    if (!path.StartsWith(parentPath))
                    {
                        Debug.LogWarning("【衣柜】" + obj.name + "(" + path + ")不在模型范围内，已自动移除！");
                        info.hideObjectList[i] = null;
                    }
                    else
                    {
                        path = path.Substring(parentPath.Length);
                        if (clothInfo.hidePaths.Contains(path))
                            info.hideObjectList[i] = null;
                        else
                            clothInfo.hidePaths.Add(path);
                    }
                }
                clothInfo.blendShapePacks = info.blendShapePacks;
                clothList.Add(clothInfo);
            }
            parameter.defaultClothIndex = defaultClothIndex;
            parameter.clothList = clothList;

            var ornamentList = new List<AvatarWardrobeParameter.OrnamentInfo>();
            foreach (var info in ornamentInfoList)
            {
                var ornamentInfo = new AvatarWardrobeParameter.OrnamentInfo { name = info.name };
                ornamentInfo.menuImage = info.image;
                ornamentInfo.isShow = info.isShow;
                ornamentInfo.type = info.type;
                for (var i = 0; i < info.objectList.Count; i++)
                {
                    var obj = info.objectList[i];
                    if (obj == null)
                        continue;
                    var path = obj.transform.GetHierarchyPath();
                    if (!path.StartsWith(parentPath))
                    {
                        Debug.LogWarning("【衣柜】" + obj.name + "(" + path + ")不在模型范围内，已自动移除！");
                        info.objectList[i] = null;
                    }
                    else
                    {
                        path = path.Substring(parentPath.Length);
                        if (ornamentInfo.itemPaths.Contains(path))
                            info.objectList[i] = null;
                        else
                        {
                            ornamentInfo.itemPaths.Add(path);
                        }
                    }
                }
                ornamentList.Add(ornamentInfo);
            }
            parameter.ornamentList = ornamentList;
            EditorUtility.SetDirty(parameter);
        }

        private void CheckAndSave()
        {
            // 检测是否有修改
            if (EditorGUI.EndChangeCheck())
            {
                DefaultBlendShapePreFix();
                serializedObject.ApplyModifiedProperties();
                WriteParameter();
            }
        }
    }
}
#endif