// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 叙事宿主接口。对应 UE 的 UTalesComponent 对外能力的一个精简契约。
// 用接口而非基类，条件/事件与运行时逻辑只依赖它，不依赖具体 MonoBehaviour——既解耦又便于测试与替身。

using System;

namespace Likeon.Narrative
{
    /// <summary>
    /// 叙事宿主对外契约：条件/事件运行时通过它查询与改写叙事状态。
    /// 具体实现是 <see cref="NarrativeComponent"/>（MonoBehaviour）。
    /// M1 只覆盖 data-task 面；任务查询等随 M3 扩充。
    /// </summary>
    public interface INarrativeHost
    {
        /// <summary>玩家“做过什么”的持久记录（data-task 完成次数）。</summary>
        MasterTaskList MasterTasks { get; }

        /// <summary>data-task 完成时触发，携带规范化后的原始任务串。对应 UE OnNarrativeDataTaskCompleted。</summary>
        event Action<string> DataTaskCompleted;

        /// <summary>用一个 <see cref="DataTaskDefinition"/> + 参数记录完成。返回是否成功记录。</summary>
        bool CompleteDataTask(DataTaskDefinition task, string argument, int quantity = 1);

        /// <summary>用任务名 + 参数记录完成（免资产版）。对应 UE CompleteNarrativeDataTask 的字符串重载。</summary>
        bool CompleteDataTask(string taskName, string argument, int quantity = 1);
    }
}
