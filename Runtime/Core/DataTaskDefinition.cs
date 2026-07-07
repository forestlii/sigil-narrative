// (c) 2026 Likeon — Sigil Narrative. C# rewrite of Narrative Pro design (Narrative Tools). License: see LICENSE.md (draft).
// 轻量“数据任务”定义。对应 UE 的 UNarrativeDataTask（继承 UDataAsset），这里落成 ScriptableObject。
// 一个数据任务 = 任务名(TaskName) + 参数(Argument)，如 "KillNPC" + "King"。用 MakeTaskString 拍成一个原始串，
// 供任务状态机推进 / 存档记录 / 对话条件查询（"是否杀过 King" 之类）。见 [[MasterTaskList]]。

using System.Text;
using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 数据任务定义（对应 UE <c>UNarrativeDataTask</c>）。轻量标记，本身无逻辑：
    /// 用 <see cref="MakeTaskString"/> 把 “任务名 + 参数” 拍成一个规范化原始串，
    /// 由 <see cref="MasterTaskList"/> 记录完成次数、被任务/对话查询。
    /// </summary>
    [CreateAssetMenu(fileName = "DataTask", menuName = "Sigil/Narrative/Data Task", order = 0)]
    public class DataTaskDefinition : ScriptableObject
    {
        [Tooltip("任务名，描述这是什么任务，如 \"KillNPC\"、\"FindItem\"。对应 UE TaskName。")]
        [SerializeField] private string taskName;

        [Tooltip("参数的显示名（仅编辑器提示用，如 \"Item Name\"）。对应 UE ArgumentName。")]
        [SerializeField] private string argumentName;

        [Tooltip("默认参数，自动填充用。对应 UE DefaultArgument。")]
        [SerializeField] private string defaultArgument;

        [Tooltip("任务描述（编辑器提示）。对应 UE TaskDescription。")]
        [TextArea, SerializeField] private string taskDescription;

        [Tooltip("分类，仅用于编辑器里组织。对应 UE TaskCategory。")]
        [SerializeField] private string taskCategory;

        /// <summary>任务名，如 "KillNPC"。</summary>
        public string TaskName => taskName ?? string.Empty;

        /// <summary>参数显示名（编辑器提示）。</summary>
        public string ArgumentName => argumentName ?? string.Empty;

        /// <summary>默认参数。</summary>
        public string DefaultArgument => defaultArgument ?? string.Empty;

        /// <summary>任务描述（编辑器提示）。</summary>
        public string TaskDescription => taskDescription ?? string.Empty;

        /// <summary>分类（编辑器组织用）。</summary>
        public string TaskCategory => taskCategory ?? string.Empty;

        /// <summary>
        /// 把任务名与参数拍成一个规范化原始串，供状态机 / 存档 / 查询使用。
        /// 对应 UE <c>UNarrativeDataTask::MakeTaskString</c>：<c>(TaskName + '_' + Argument)</c> 转小写、再删去所有空格。
        /// 例：TaskName="Talk To"、Argument="King Bob" → "talkto_kingbob"。
        /// 注意：即使参数为空也会带上下划线（"kill_"），与 UE 行为一致。
        /// </summary>
        public string MakeTaskString(string argument)
        {
            return Normalize(TaskName, argument);
        }

        /// <summary>
        /// 纯字符串规范化：<c>(taskName + '_' + argument)</c> 转小写、删去所有空格。
        /// 抽成 static 便于脱离 ScriptableObject 单测，且供别处按名构造任务串。
        /// </summary>
        public static string Normalize(string taskName, string argument)
        {
            var raw = ((taskName ?? string.Empty) + "_" + (argument ?? string.Empty)).ToLowerInvariant();

            // 对齐 UE 的 RemoveSpacesInline：去掉空格字符。
            var sb = new StringBuilder(raw.Length);
            foreach (var c in raw)
            {
                if (c != ' ')
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}
