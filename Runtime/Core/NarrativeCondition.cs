// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 叙事条件基类。对应 UE 的 UNarrativeCondition（抽象、可蓝图扩展）。
// 用可 [SerializeReference] 的抽象类实现，让节点里能存一组多态条件。
// 简化：UE 的角色目标过滤(EConditionFilter/targets)与 party 策略在单机+无角色系统下不移植。

using System;
using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 叙事条件基类。子类覆盖 <see cref="CheckCondition"/> 返回真假；
    /// 外部调用 <see cref="IsMet"/>（已应用 <see cref="Not"/> 取反）。
    /// 对应 UE <c>UNarrativeCondition::CheckCondition</c> + <c>bNot</c>。
    /// </summary>
    [Serializable]
    public abstract class NarrativeCondition
    {
        [Tooltip("勾选后翻转本条件的结果。对应 UE bNot。")]
        [SerializeField] private bool negate = false;

        /// <summary>是否翻转结果。对应 UE bNot。</summary>
        public bool Not
        {
            get => negate;
            set => negate = value;
        }

        /// <summary>子类实现：本条件是否成立（未应用取反）。</summary>
        public abstract bool CheckCondition(NarrativeContext context);

        /// <summary>对外求值：应用 <see cref="Not"/> 后的最终结果。</summary>
        public bool IsMet(NarrativeContext context)
        {
            var result = CheckCondition(context);
            return negate ? !result : result;
        }

        /// <summary>编辑器里显示在节点上的文本。对应 UE GetGraphDisplayText。</summary>
        public virtual string GetDisplayText() => GetType().Name;
    }
}
