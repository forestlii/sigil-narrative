// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 内置条件：检查某个任务是否处于指定状态（进行中/已成功/已失败/已结束/已参与过）。
// 对应 UE 里用蓝图条件节点查 IsQuestSucceeded 等的常见用法（如“仅当任务 X 已完成才显示此对话”）。

using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 条件：宿主上指定 <see cref="QuestAsset"/> 是否处于 <see cref="Query"/> 所选状态。
    /// 只依赖 <see cref="INarrativeHost"/> 的任务查询面。典型用法：对话选项“任务完成后才出现”。
    /// 从未开始过的任务对所有查询都返回 <c>false</c>。
    /// </summary>
    [System.Serializable]
    public sealed class QuestStateCondition : NarrativeCondition
    {
        [Tooltip("要检查的任务资产。")]
        [SerializeField] private QuestAsset quest;

        [Tooltip("要检查任务的哪种状态。")]
        [SerializeField] private EQuestStateQuery query = EQuestStateQuery.Succeeded;

        public QuestStateCondition() { }

        public QuestStateCondition(QuestAsset quest, EQuestStateQuery query = EQuestStateQuery.Succeeded)
        {
            this.quest = quest;
            this.query = query;
        }

        /// <summary>被检查的任务资产。</summary>
        public QuestAsset Quest => quest;

        /// <summary>检查的状态种类。</summary>
        public EQuestStateQuery Query => query;

        public override bool CheckCondition(NarrativeContext context)
        {
            var host = context?.Host;
            if (host == null || quest == null)
            {
                return false;
            }

            switch (query)
            {
                case EQuestStateQuery.InProgress: return host.IsQuestInProgress(quest);
                case EQuestStateQuery.Succeeded: return host.IsQuestSucceeded(quest);
                case EQuestStateQuery.Failed: return host.IsQuestFailed(quest);
                case EQuestStateQuery.Finished: return host.IsQuestFinished(quest);
                case EQuestStateQuery.StartedOrFinished: return host.IsQuestStartedOrFinished(quest);
                default: return false;
            }
        }

        public override string GetDisplayText()
        {
            var name = quest != null ? quest.QuestId : "<quest>";
            return $"Quest \"{name}\" is {query}";
        }
    }
}
