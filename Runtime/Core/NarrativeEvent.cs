// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 叙事事件基类。对应 UE 的 UNarrativeEvent（抽象、可蓝图扩展）。
// 事件在节点进入/离开时触发（EEventRuntime），可带前置条件，读档时按 RefireOnLoad 决定是否重放。
// 简化：UE 的角色目标过滤(EEventFilter/targets)与 party 策略不移植。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 叙事事件基类。子类覆盖 <see cref="Execute"/> 写具体逻辑（给物品、开任务、完成 data-task…）。
    /// 对应 UE <c>UNarrativeEvent::ExecuteEvent</c> + <c>EventRuntime</c> + <c>bRefireOnLoad</c> + <c>Conditions</c>。
    /// </summary>
    [Serializable]
    public abstract class NarrativeEvent
    {
        [Tooltip("事件在节点的哪个阶段执行。对应 UE EventRuntime。")]
        [SerializeField] private EEventRuntime runtime = EEventRuntime.Start;

        [Tooltip("仅任务：读档时是否重新触发。像“给 500 XP”这类应设为 false，避免读档重复发放。对应 UE bRefireOnLoad。")]
        [SerializeField] private bool refireOnLoad = true;

        [Tooltip("仅当这些条件全部满足时事件才触发。对应 UE NarrativeEvent.Conditions。")]
        [SerializeReference] private List<NarrativeCondition> conditions = new List<NarrativeCondition>();

        /// <summary>执行时机。对应 UE EventRuntime。</summary>
        public EEventRuntime Runtime
        {
            get => runtime;
            set => runtime = value;
        }

        /// <summary>读档是否重放（仅任务语义）。对应 UE bRefireOnLoad。</summary>
        public bool RefireOnLoad
        {
            get => refireOnLoad;
            set => refireOnLoad = value;
        }

        /// <summary>事件的前置条件（全满足才触发）。</summary>
        public IReadOnlyList<NarrativeCondition> Conditions => conditions;

        /// <summary>追加一个前置条件（代码构建用；null 忽略）。</summary>
        public void AddCondition(NarrativeCondition condition)
        {
            if (condition != null)
            {
                (conditions ??= new List<NarrativeCondition>()).Add(condition);
            }
        }

        /// <summary>子类实现：事件的具体逻辑。对应 UE ExecuteEvent。</summary>
        public abstract void Execute(NarrativeContext context);

        /// <summary>本事件是否应在给定阶段触发（Both 在 Start 与 End 都触发）。</summary>
        public bool RunsAt(EEventRuntime phase)
        {
            return runtime == phase || runtime == EEventRuntime.Both;
        }

        /// <summary>前置条件是否全部满足（空列表视为满足）。</summary>
        public bool ConditionsMet(NarrativeContext context)
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

        /// <summary>编辑器里显示在节点上的文本。对应 UE GetGraphDisplayText。</summary>
        public virtual string GetDisplayText() => GetType().Name;

        /// <summary>对话选项后缀提示文本（如“(Begin Quest)”）。对应 UE GetHintText。</summary>
        public virtual string GetHintText() => string.Empty;
    }
}
