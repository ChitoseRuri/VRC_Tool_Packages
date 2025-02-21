#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Collections.Generic;
using static VRChatAvatarToolkit.MoyuToolkitUtils;
using System.IO;

namespace VRChatAvatarToolkit.AvatarAnimationPlayer
{
    public class AvatarAnimationPlayerEditor : EditorWindow
    {
        private Vector2 mainScrollPos;
        private Dictionary<string, string> animationMap = new Dictionary<string, string>();
        private Animator avatar;
        private bool isRunning = false;

        private void Awake()
        {
            ReloadAnimation();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(5, 10, position.width - 10, position.height - 20));

            mainScrollPos = GUILayout.BeginScrollView(mainScrollPos);
            GUILayout.Label("动作播放器", new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter
            });
            GUILayout.Label("让模型动起来，更方便地调试模型", new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            });
            GUILayout.Space(10);

            var runFlag = Application.isPlaying;
            EditorGUILayout.HelpBox(runFlag ? "编辑模型前记得先停止运行哦" : "先点击下方的按钮运行吧~", MessageType.Info);
            GUILayout.Space(10);

            if (GUILayout.Button(runFlag ? "停止" : "运行"))
            {
                EditorApplication.ExecuteMenuItem("Edit/Play");
            }
            if (runFlag != isRunning)
            {
                isRunning = runFlag;
                ReloadAnimation();
                var animatorList = FindObjectsOfType<Animator>();
                for (var i = 0; i < animatorList.Length; i++)
                {
                    var animator = animatorList[i];
                    if (animator.transform.parent != null) continue;
                    if (animator.isHuman)
                    {
                        avatar = animator;
                        break;
                    }
                }
            }
            if (runFlag)
            {
                GUILayout.Space(10);
                avatar = (Animator)EditorGUILayout.ObjectField("选择模型", avatar, typeof(Animator), true);
                if (avatar == null)
                {
                    GUILayout.Space(10);
                    EditorGUILayout.HelpBox("先选择一个模型吧~", MessageType.Warning);
                }

                GUILayout.Space(10);
                var index = 0;
                foreach (var animPair in animationMap)
                {
                    if (index % 4 == 0)
                    {
                        if (index != 0)
                            GUILayout.EndHorizontal();
                        GUILayout.BeginHorizontal();
                    }
                    if (GUILayout.Button(animPair.Key))
                        PlayAnimation(avatar, animPair.Value);
                    index++;
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void ReloadAnimation()
        {
            if (animationMap == null)
                animationMap = new Dictionary<string, string>();
            else
                animationMap.Clear();

            var animationDir = GetAssetsPath("Assets/Animation/");
            if (!Directory.Exists(animationDir)) return;
            var files = new DirectoryInfo(animationDir).GetFiles();
            foreach (var file in files)
            {
                if (file.Name.EndsWith(".anim"))
                {
                    var name = file.Name;
                    name = name.Substring(0, name.IndexOf("."));
                    animationMap.Add(name, animationDir + file.Name);
                }
            }
        }

        static void PlayAnimation(Animator animator, string path)
        {
            if (animator == null || !Application.isPlaying)
                return;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var animController = new AnimatorController();
            animController.AddLayer("action");
            animController.layers[0].stateMachine.AddState("action");
            var motion = AssetDatabase.LoadAssetAtPath<Motion>(path);
            animController.layers[0].stateMachine.states[0].state.motion = motion;
            animator.runtimeAnimatorController = animController;
        }
    }
}
#endif