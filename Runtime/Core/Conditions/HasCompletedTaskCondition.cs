// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 内置条件：玩家是否已完成某 data-task 至少 N 次。对应 UE HasCompletedTask 一类条件的用法。

using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 条件：宿主的 <see cref="MasterTaskList"/> 里，指定 data-task 是否已完成至少 <c>Quantity</c> 次。
    /// 典型用法：对话选项“我杀过国王”只在 <c>HasCompleted(KillNPC, King)</c> 时出现。
    /// </summary>
    [System.Serializable]
    public sealed class HasCompletedTaskCondition : NarrativeCondition
    {
        [Tooltip("要检查的数据任务。")]
        [SerializeField] private DataTaskDefinition task;

        [Tooltip("任务参数，如 \"King\"。")]
        [SerializeField] private string argument;

        [Tooltip("需要完成的最少次数。")]
        [SerializeField, Min(1)] private int quantity = 1;

        public HasCompletedTaskCondition() { }

        public HasCompletedTaskCondition(DataTaskDefinition task, string argument, int quantity = 1)
        {
            this.task = task;
            this.argument = argument;
            this.quantity = quantity;
        }

        public override bool CheckCondition(NarrativeContext context)
        {
            var tasks = context?.Host?.MasterTasks;
            if (tasks == null || task == null)
            {
                return false;
            }

            return tasks.HasCompleted(task, argument, quantity);
        }

        public override string GetDisplayText()
        {
            return task != null
                ? $"Has completed \"{task.MakeTaskString(argument)}\" x{quantity}"
                : "Has completed <task>";
        }
    }
}
