﻿using System.Collections.Generic;
using UnityEngine;

namespace VRChatAvatarToolkit {

    public class AvatarWardrobeParameter : ScriptableObject {
        internal string avatarId; // 绑定模型ID
        public int defaultClothIndex; // 默认衣服
        public string avatarBodyName; // 体形形态的位置
        public List<BlendShapePack> defaultBlendShapes = new List<BlendShapePack>();
        public List<ClothInfo> clothList = new List<ClothInfo>(); // 衣服列表
        public List<OrnamentInfo> ornamentList = new List<OrnamentInfo>(); // 配饰列表
        public List<MutualExclusionInfo> mutualExclusionList = new List<MutualExclusionInfo>(); // 互斥饰品列表

        [System.Serializable]
        public class BlendShapePack
        {
            public BlendShapePack()
            {
                
            }

            public BlendShapePack(string path, string name, float value)
            {
                this.path = path;
                this.name = name;
                this.value = value;
            }

            public string path;
            public string name;
            public float value;
        }

        public static Dictionary<string, float> BlendShapePacksDefault = new Dictionary<string, float>
        {
            { "Foot_heel_high", 0 },
            { "Shrink_Foot", 0 },
        };

        [System.Serializable]
        public class ClothInfo {
            public string name; //衣服名称，每套衣服名字唯一
            public string type; //分类
            public Texture2D menuImage; //菜单图标
            public List<string> showPaths = new List<string>(); //显示元素
            public List<string> hidePaths = new List<string>(); //隐藏元素
            public List<BlendShapePack> blendShapePacks = new List<BlendShapePack>();
        }

        [System.Serializable]
        public class OrnamentInfo {
            public string name; //配饰名称，每套饰品唯一
            public string type; //分类
            public Texture2D menuImage; //菜单图标
            public bool isShow; //是否默认显示
            public List<string> itemPaths = new List<string>(); //元素
        }

        [System.Serializable]
        public class ExclusionInfo
        {
            public string name; //配饰名称，每套饰品唯一
            public Texture2D menuImage; //菜单图标
            public List<string> itemPaths = new List<string>(); //元素
        }

        [System.Serializable]
        public class MutualExclusionInfo
        {
            public string name;
            public Texture2D menuImage; //菜单图标
            public List<ExclusionInfo> mutualExclusions = new();
        }
    }
}