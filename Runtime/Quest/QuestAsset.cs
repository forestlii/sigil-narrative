// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 任务资产：把一张 Quest 状态图（起始状态 + 状态集）包成 ScriptableObject，作为 Unity 里可编辑/可复用的模板。
// 对应 UE 里 QuestBlueprint 编译出的 UQuest 类——第一版跳过图编辑器，直接编辑扁平状态集。
// 关键：资产是「纯净模板」，运行进度存在 State/Branch/Task 的运行时字段里，所以每次开始任务都要克隆一份再跑。

using System.Collections.Generic;
using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 一个任务的 Unity 资产（状态机模板）。对应 UE <c>UQuest</c> 蓝图类。
    /// 用 <see cref="CreateRuntimeQuest"/> 克隆出干净的运行时 <see cref="Quest"/>；模板本身不被运行时改动。
    /// </summary>
    [CreateAssetMenu(fileName = "Quest", menuName = "Sigil/Narrative/Quest", order = 11)]
    public sealed class QuestAsset : ScriptableObject
    {
        [Tooltip("任务的稳定身份 ID：存档按它定位任务、读档按它找回资产。留空则回退用资产名。\n" +
                 "对应 UE 里用 QuestClass 当身份——单机存档需要一个跨会话稳定的字符串键。建议显式填写并保持不变。")]
        [SerializeField] private string questId;

        [Tooltip("起始状态 ID。Begin 时若不指定则从此状态进入。对应 UE 的 QuestStartState。")]
        [SerializeField] private string startStateId;

        /// <summary>
        /// 任务的稳定身份 ID（存档/读档的键）。留空则回退到资产名（<see cref="Object.name"/>）。
        /// ⚠ 一旦有存档就别再改它，否则旧存档里的该任务会认不回来。对应 UE 用 QuestClass 当身份键。
        /// </summary>
        public string QuestId => string.IsNullOrEmpty(questId) ? name : questId;

        [Tooltip("状态集。用 SerializeReference 规避深层嵌套序列化的深度上限。")]
        [SerializeReference] private List<QuestState> states = new List<QuestState>();

        /// <summary>起始状态 ID。</summary>
        public string StartStateId
        {
            get => startStateId ?? string.Empty;
            set => startStateId = value;
        }

        /// <summary>模板里的状态集（只读；运行时请用 <see cref="CreateRuntimeQuest"/> 拿克隆副本）。</summary>
        public IReadOnlyList<QuestState> States => states;

        /// <summary>
        /// 在代码里构造一个任务资产模板（用于程序化生成任务、样例、测试——正常玩法里在 Inspector 里编辑资产）。
        /// 返回的是内存中的 <see cref="ScriptableObject"/>，未落盘。
        /// </summary>
        public static QuestAsset Create(string questId, string startStateId, IEnumerable<QuestState> states)
        {
            var asset = CreateInstance<QuestAsset>();
            asset.questId = questId;
            asset.startStateId = startStateId;
            asset.states = states != null ? new List<QuestState>(states) : new List<QuestState>();
            if (!string.IsNullOrEmpty(questId))
            {
                asset.name = questId;
            }

            return asset;
        }

        /// <summary>
        /// 克隆出一个干净的运行时 <see cref="Quest"/>：深拷贝所有状态/分支/任务，进度清零，
        /// 并回填 <see cref="Quest.SourceAsset"/> 指回本资产。模板自身不被改动。
        /// </summary>
        public Quest CreateRuntimeQuest(NarrativeContext context)
        {
            var cloned = new List<QuestState>();
            if (states != null)
            {
                foreach (var state in states)
                {
                    if (state != null)
                    {
                        cloned.Add(state.CloneForRuntime());
                    }
                }
            }

            return new Quest(startStateId, cloned, context) { SourceAsset = this };
        }
    }
}
