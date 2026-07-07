// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 任务分支。对应 UE 的 UQuestBranch：一组任务 + 一个目标状态；任务全完成即取该分支、跳到目标状态。

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 任务状态机里的一条分支。对应 UE <c>UQuestBranch</c>。
    /// 激活时对所有任务 BeginTask；某任务达标时检查是否全部完成，若是则让 Quest 取此分支。
    /// </summary>
    [Serializable]
    public sealed class QuestBranch
    {
        [Tooltip("分支 ID（存档按 ID 定位分支）。")]
        [SerializeField] private string id;

        [Tooltip("是否在 UI 里隐藏（用于隐藏逻辑分支）。对应 UE bHidden。")]
        [SerializeField] private bool hidden;

        [Tooltip("取该分支后前往的目标状态 ID。对应 UE DestinationState。")]
        [SerializeField] private string destinationStateId;

        [Tooltip("需要完成的任务（全部完成才取此分支；顺序无关）。对应 UE QuestTasks。")]
        [SerializeReference] private List<QuestTask> tasks = new List<QuestTask>();

        private Quest _quest;

        public string Id
        {
            get => id ?? string.Empty;
            set => id = value;
        }

        public bool Hidden => hidden;

        public string DestinationStateId
        {
            get => destinationStateId ?? string.Empty;
            set => destinationStateId = value;
        }

        public IReadOnlyList<QuestTask> Tasks => tasks;

        public void AddTask(QuestTask task)
        {
            if (task != null)
            {
                (tasks ??= new List<QuestTask>()).Add(task);
            }
        }

        internal void Activate(Quest quest, NarrativeContext context)
        {
            _quest = quest;
            if (tasks == null)
            {
                return;
            }

            foreach (var task in tasks)
            {
                task?.Begin(this, context);
            }
        }

        internal void Deactivate()
        {
            if (tasks == null)
            {
                return;
            }

            foreach (var task in tasks)
            {
                task?.End();
            }
        }

        /// <summary>所有任务是否都完成（可选任务恒视为完成）。对应 UE AreTasksComplete。</summary>
        public bool AreTasksComplete()
        {
            if (tasks == null)
            {
                return true;
            }

            foreach (var task in tasks)
            {
                if (task != null && !task.IsComplete)
                {
                    return false;
                }
            }

            return true;
        }

        // 任务进度变化/达标时的上行回调。
        internal void OnTaskProgressChanged(QuestTask task, int oldProgress, int newProgress)
        {
            _quest?.NotifyTaskProgress(this, task, oldProgress, newProgress);
        }

        internal void OnTaskReachedCompletion(QuestTask task)
        {
            // 对齐 UE UQuestBranch::OnQuestTaskComplete：先（若全完成）取分支，再广播任务完成。
            if (AreTasksComplete())
            {
                _quest?.TakeBranch(this);
            }

            _quest?.NotifyTaskCompleted(this, task);
        }
    }
}
