// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 对话资产：把 DialogueGraph 包成 ScriptableObject，作为 Unity 里可编辑/可加载的对话资源入口。
// 对应 UE 里 DialogueBlueprint 编译出的 Dialogue 模板——第一版跳过图编辑器，直接编辑扁平图。

using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 一段对话的 Unity 资产。持有一张扁平 <see cref="DialogueGraph"/>；运行时交给
    /// <see cref="DialogueController"/> 推进。
    /// </summary>
    [CreateAssetMenu(fileName = "Dialogue", menuName = "Sigil/Narrative/Dialogue", order = 10)]
    public sealed class DialogueAsset : ScriptableObject
    {
        [SerializeField] private DialogueGraph graph = new DialogueGraph();

        /// <summary>本资产的对话图。</summary>
        public DialogueGraph Graph => graph;
    }
}
