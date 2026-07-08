// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 任务状态。对应 UE 的 UQuestState：一个状态持有若干分支；激活时激活所有分支。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 任务状态机里的一个状态。对应 UE <c>UQuestState</c>（继承 <c>UNarrativeNodeBase</c>）。
    /// <see cref="NodeType"/> 决定到达此状态时任务是进行中/成功/失败；
    /// 继承自 <see cref="NarrativeNodeBase"/> 的 <see cref="NarrativeNodeBase.Events"/> 在到达/离开此状态时触发
    /// （进入发 Start、离开发 End，见 <see cref="Quest.EnterState"/>），可用于“到达即给奖励/开对话”等状态副作用。
    /// 注：继承而来的 <see cref="NarrativeNodeBase.Conditions"/> 目前不参与状态流转判定（进入目标状态不做条件门控）。
    /// </summary>
    [Serializable]
    public sealed class QuestState : NarrativeNodeBase
    {
        [Tooltip("状态类型：Regular/Success/Failure。对应 UE StateNodeType。")]
        [SerializeField] private EStateNodeType nodeType = EStateNodeType.Regular;

        [Tooltip("此状态下的分支。对应 UE Branches。")]
        [SerializeReference] private List<QuestBranch> branches = new List<QuestBranch>();

        public QuestState() { }

        public QuestState(string id, EStateNodeType nodeType = EStateNodeType.Regular)
        {
            Id = id;
            this.nodeType = nodeType;
        }

        public EStateNodeType NodeType => nodeType;

        public IReadOnlyList<QuestBranch> Branches => branches;

        public void AddBranch(QuestBranch branch)
        {
            if (branch != null)
            {
                (branches ??= new List<QuestBranch>()).Add(branch);
            }
        }

        /// <summary>从模板克隆出干净的运行时副本（保留 id/nodeType，逐个克隆分支）。</summary>
        internal QuestState CloneForRuntime()
        {
            var clone = (QuestState)MemberwiseClone(); // 拷贝 id/nodeType 等私有字段
            clone.branches = new List<QuestBranch>();
            if (branches != null)
            {
                foreach (var branch in branches)
                {
                    if (branch != null)
                    {
                        clone.branches.Add(branch.CloneForRuntime());
                    }
                }
            }

            return clone;
        }

        internal void Activate(Quest quest, NarrativeContext context)
        {
            if (branches == null)
            {
                return;
            }

            foreach (var branch in branches)
            {
                branch?.Activate(quest, context);
            }
        }

        internal void Deactivate()
        {
            if (branches == null)
            {
                return;
            }

            foreach (var branch in branches)
            {
                branch?.Deactivate();
            }
        }
    }
}
