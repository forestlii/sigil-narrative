// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 叙事宿主组件。对应 UE 的 UTalesComponent（挂在 PlayerController 上）。
// 这里做成 MonoBehaviour + 实现 INarrativeHost——不强制用户继承任何基类，加到任意 GameObject 上即可。
// M1 只覆盖 data-task 面（记录/查询/事件）；对话、任务、存档随后续里程碑扩充。

using System;
using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 叙事系统的宿主组件——开始对话/任务、完成 data-task、查询叙事状态的入口。
    /// 对应 UE <c>UTalesComponent</c>。加到你的玩家 GameObject 上即可，无需继承基类。
    /// </summary>
    [AddComponentMenu("Sigil/Narrative/Narrative Component")]
    public class NarrativeComponent : MonoBehaviour, INarrativeHost
    {
        private readonly MasterTaskList _masterTasks = new MasterTaskList();

        /// <inheritdoc/>
        public MasterTaskList MasterTasks => _masterTasks;

        /// <inheritdoc/>
        public event Action<string> DataTaskCompleted;

        /// <inheritdoc/>
        public bool CompleteDataTask(DataTaskDefinition task, string argument, int quantity = 1)
        {
            if (task == null)
            {
                return false;
            }

            return CompleteRawTask(task.MakeTaskString(argument), quantity);
        }

        /// <inheritdoc/>
        public bool CompleteDataTask(string taskName, string argument, int quantity = 1)
        {
            if (string.IsNullOrEmpty(taskName))
            {
                return false;
            }

            return CompleteRawTask(DataTaskDefinition.Normalize(taskName, argument), quantity);
        }

        private bool CompleteRawTask(string rawTaskString, int quantity)
        {
            if (string.IsNullOrEmpty(rawTaskString))
            {
                return false;
            }

            _masterTasks.CompleteTask(rawTaskString, quantity);
            DataTaskCompleted?.Invoke(rawTaskString);
            return true;
        }

        /// <summary>构造一个以本宿主为根的 <see cref="NarrativeContext"/>。</summary>
        public NarrativeContext MakeContext(GameObject target = null)
        {
            return new NarrativeContext(this, target);
        }
    }
}
