// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 玩家“做过什么”的持久记录。对应 UE UTalesComponent 里的 MasterTaskList（TMap<FString,int32>）。
// 记录每个 data-task 原始串被完成的次数，是任务系统与对话系统的耦合点，也是存档核心之一。
// 纯 C#（不依赖 UnityEngine），便于 EditMode 单测。见 [[DataTaskDefinition]]。

using System.Collections.Generic;

namespace Likeon.Narrative
{
    /// <summary>
    /// 已完成 data-task 的累计记录：原始任务串 → 完成次数。
    /// 对应 UE <c>UTalesComponent::MasterTaskList</c> 以及 CompleteNarrativeDataTask / HasCompletedTask 一族。
    /// 任务分支靠它推进；对话条件靠它查询（“是否杀过 King”）；存档时整表序列化。
    /// </summary>
    public sealed class MasterTaskList
    {
        private readonly Dictionary<string, int> _completed = new Dictionary<string, int>();

        /// <summary>只读视图，供存档序列化。</summary>
        public IReadOnlyDictionary<string, int> Entries => _completed;

        /// <summary>不同任务串的数量。</summary>
        public int DistinctCount => _completed.Count;

        /// <summary>
        /// 记录一次（或多次）data-task 完成，累加次数并返回累加后的总次数。
        /// 对应 UE CompleteNarrativeDataTask 里对 MasterTaskList 的 Find/+=（或 Add）。
        /// </summary>
        /// <param name="rawTaskString">已由 <see cref="DataTaskDefinition.MakeTaskString"/> 规范化的原始串。</param>
        /// <param name="quantity">本次完成次数，默认 1。</param>
        public int CompleteTask(string rawTaskString, int quantity = 1)
        {
            if (string.IsNullOrEmpty(rawTaskString))
            {
                return 0;
            }

            _completed.TryGetValue(rawTaskString, out var current);
            var total = current + quantity;
            _completed[rawTaskString] = total;
            return total;
        }

        /// <summary>用一个 <see cref="DataTaskDefinition"/> + 参数记录完成。</summary>
        public int CompleteTask(DataTaskDefinition task, string argument, int quantity = 1)
        {
            return task == null ? 0 : CompleteTask(task.MakeTaskString(argument), quantity);
        }

        /// <summary>
        /// 该任务串被完成过多少次（没有则 0）。
        /// 对应 UE <c>GetNumberOfTimesTaskWasCompleted</c>。
        /// </summary>
        public int GetCount(string rawTaskString)
        {
            if (string.IsNullOrEmpty(rawTaskString))
            {
                return 0;
            }

            _completed.TryGetValue(rawTaskString, out var count);
            return count;
        }

        /// <summary>用一个 <see cref="DataTaskDefinition"/> + 参数查询完成次数。</summary>
        public int GetCount(DataTaskDefinition task, string argument)
        {
            return task == null ? 0 : GetCount(task.MakeTaskString(argument));
        }

        /// <summary>
        /// 是否已完成该任务串至少 <paramref name="quantity"/> 次。
        /// 对应 UE <c>HasCompletedTask</c>（次数 &gt;= Quantity）。
        /// </summary>
        public bool HasCompleted(string rawTaskString, int quantity = 1)
        {
            return GetCount(rawTaskString) >= quantity;
        }

        /// <summary>用一个 <see cref="DataTaskDefinition"/> + 参数查询是否已完成足够次数。</summary>
        public bool HasCompleted(DataTaskDefinition task, string argument, int quantity = 1)
        {
            return task != null && GetCount(task.MakeTaskString(argument)) >= quantity;
        }

        /// <summary>清空全部记录。</summary>
        public void Clear() => _completed.Clear();

        /// <summary>
        /// 直接写入一条记录（读档时用，绕过累加语义）。
        /// </summary>
        public void RestoreEntry(string rawTaskString, int count)
        {
            if (!string.IsNullOrEmpty(rawTaskString))
            {
                _completed[rawTaskString] = count;
            }
        }
    }
}
