using System.Collections.Generic;
using UnityEngine;

namespace VRChatAvatarToolkit
{
    public class ActionManagerParameter : ScriptableObject
    {
        internal string avatarId; // 绑定模型ID
        public List<ActionInfo> actionList = new List<ActionInfo>(); // 衣服列表
        public bool audio3D = true; // 声音根据距离衰减
        public float audioMinDistance = 2;  // 声音开始衰减距离
        public float audioMaxDistance = 8; // 衰减到没有声音的距离

        [System.Serializable]
        public class ActionInfo
        {
            public string name; //唯一名称
            public string type; //分类
            public bool autoExit; //自动结束播放
            public Texture2D menuImage; //菜单图标
            public AnimationClip animation;  //动作文件
            public AudioClip audio;   //音乐
            public ActionInfo() { }
#if UNITY_EDITOR
            public ActionInfo(ActionManagerUtils.ActionItemInfo info)
            {
                name = info.name;
                type = info.type;
                menuImage = info.image;
                animation = info.animation;
                audio = info.audio;
                autoExit = info.autoExit;
            }
#endif
        }
    }
}