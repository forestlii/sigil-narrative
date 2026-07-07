// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 叙事节点基类——对话树节点与任务状态机节点的共同基类。对应 UE 的 UNarrativeNodeBase。
// 持有 ID、一组条件、一组事件；提供“条件是否满足”与“按阶段处理事件”。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 对话/任务节点的共同基类。对应 UE <c>UNarrativeNodeBase</c>：
    /// <see cref="Id"/> + <see cref="Conditions"/> + <see cref="Events"/>，
    /// 以及 <see cref="AreConditionsMet"/> / <see cref="ProcessEvents"/>。
    /// </summary>
    [Serializable]
    public class NarrativeNodeBase
    {
        [Tooltip("节点 ID，可空。用于按 ID 引用节点（对话/任务图内唯一）。对应 UE ID。")]
        [SerializeField] private string id;

        [Tooltip("本节点只在这些条件全部满足时可用。对应 UE NarrativeNodeBase.Conditions。")]
        [SerializeReference] private List<NarrativeCondition> conditions = new List<NarrativeCondition>();

        [Tooltip("到达本节点时要触发的事件。对应 UE NarrativeNodeBase.Events。")]
        [SerializeReference] private List<NarrativeEvent> events = new List<NarrativeEvent>();

        /// <summary>节点 ID。</summary>
        public string Id
        {
            get => id ?? string.Empty;
            set => id = value;
        }

        /// <summary>本节点的条件。</summary>
        public IReadOnlyList<NarrativeCondition> Conditions => conditions;

        /// <summary>本节点的事件。</summary>
        public IReadOnlyList<NarrativeEvent> Events => events;

        /// <summary>追加一个条件（代码构建节点用；null 忽略）。</summary>
        public void AddCondition(NarrativeCondition condition)
        {
            if (condition != null)
            {
                (conditions ??= new List<NarrativeCondition>()).Add(condition);
            }
        }

        /// <summary>追加一个事件（代码构建节点用；null 忽略）。</summary>
        public void AddEvent(NarrativeEvent narrativeEvent)
        {
            if (narrativeEvent != null)
            {
                (events ??= new List<NarrativeEvent>()).Add(narrativeEvent);
            }
        }

        /// <summary>
        /// 本节点的所有条件是否都满足（空列表视为满足）。
        /// 对应 UE <c>AreConditionsMet</c>。
        /// </summary>
        public bool AreConditionsMet(NarrativeContext context)
        {
            if (conditions == null)
            {
                return true;
            }

            foreach (var condition in conditions)
            {
                if (condition != null && !condition.IsMet(context))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 处理本节点在给定阶段（Start/End）该触发的事件：先按阶段过滤，再检查事件自身前置条件，最后执行。
        /// 对应 UE <c>ProcessEvents(..., EEventRuntime)</c>。
        /// </summary>
        public void ProcessEvents(NarrativeContext context, EEventRuntime phase)
        {
            if (events == null)
            {
                return;
            }

            foreach (var narrativeEvent in events)
            {
                if (narrativeEvent == null || !narrativeEvent.RunsAt(phase))
                {
                    continue;
                }

                if (!narrativeEvent.ConditionsMet(context))
                {
                    continue;
                }

                narrativeEvent.Execute(context);
            }
        }
    }
}
