// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 内置任务：监听宿主完成某 data-task 来推进进度。是把“对话/世界事件”接入任务进度的最常用任务。

using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 当玩家完成指定 data-task 时推进进度的任务。
    /// 例：分支任务“和长老对话 3 次”= 一个本任务，RequiredQuantity=3，监听 <c>TalkTo_Elder</c>。
    /// 每次宿主 <see cref="INarrativeHost.CompleteDataTask(string, string, int)"/> 触发匹配串即 +1。
    /// </summary>
    [System.Serializable]
    public sealed class CompleteDataTaskQuestTask : QuestTask
    {
        [Tooltip("要监听的数据任务。")]
        [SerializeField] private DataTaskDefinition task;

        [Tooltip("任务参数，如 \"Elder\"。")]
        [SerializeField] private string argument;

        private string _target;

        public CompleteDataTaskQuestTask() { }

        public CompleteDataTaskQuestTask(DataTaskDefinition task, string argument, int requiredQuantity = 1)
        {
            this.task = task;
            this.argument = argument;
            // 用反射之外的方式设 RequiredQuantity 不方便（私有序列化字段），测试里可用带此参的构造。
            SetRequiredQuantityForConstruction(requiredQuantity);
        }

        protected override void OnBeginTask()
        {
            _target = task != null ? task.MakeTaskString(argument) : null;

            if (Context?.Host != null)
            {
                Context.Host.DataTaskCompleted += OnDataTaskCompleted;
            }
        }

        protected override void OnEndTask()
        {
            if (Context?.Host != null)
            {
                Context.Host.DataTaskCompleted -= OnDataTaskCompleted;
            }
        }

        private void OnDataTaskCompleted(string rawTaskString)
        {
            // 注：每次完成事件 +1（不按 quantity 批量），适配“逐次”类任务（击杀/对话计数）。
            if (!string.IsNullOrEmpty(_target) && rawTaskString == _target)
            {
                AddProgress(1);
            }
        }

        protected override string DefaultDescription()
        {
            return task != null ? $"Complete \"{task.MakeTaskString(argument)}\"" : "Complete <task>";
        }
    }
}
