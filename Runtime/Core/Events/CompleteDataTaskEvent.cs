// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 内置事件：到达节点时完成一个 data-task。对应 UE 用 NarrativeEvent 调 CompleteNarrativeDataTask 的做法。

using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 事件：执行时在宿主上完成指定 data-task（写入 <see cref="MasterTaskList"/> 并广播）。
    /// 常用于把对话/任务节点接进任务进度：例如某对话行触发 <c>TalkTo_Elder</c>，从而推进任务。
    /// </summary>
    [System.Serializable]
    public sealed class CompleteDataTaskEvent : NarrativeEvent
    {
        [Tooltip("要完成的数据任务。")]
        [SerializeField] private DataTaskDefinition task;

        [Tooltip("任务参数，如 \"Elder\"。")]
        [SerializeField] private string argument;

        [Tooltip("完成次数。")]
        [SerializeField, Min(1)] private int quantity = 1;

        public CompleteDataTaskEvent() { }

        public CompleteDataTaskEvent(DataTaskDefinition task, string argument, int quantity = 1)
        {
            this.task = task;
            this.argument = argument;
            this.quantity = quantity;
        }

        public override void Execute(NarrativeContext context)
        {
            context?.Host?.CompleteDataTask(task, argument, quantity);
        }

        public override string GetDisplayText()
        {
            return task != null
                ? $"Complete \"{task.MakeTaskString(argument)}\" x{quantity}"
                : "Complete <task>";
        }
    }
}
