// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 叙事存档的 JSON 序列化 + 文件读写门面。对应 UE 用 USaveGame/FArchive 那层，这里换成 DTO+JSON。
// 逻辑（Capture/Restore 叙事状态）在宿主 NarrativeComponent 上；本类只管 DTO ↔ JSON ↔ 文件。

using UnityEngine;

namespace Likeon.Narrative
{
    /// <summary>
    /// 把 <see cref="NarrativeSaveData"/> 与 JSON / 文件互转。文件读写走注入的 <see cref="IFileSystem"/>
    /// （默认磁盘），便于 EditMode 里用内存假实现测试。
    /// </summary>
    public sealed class NarrativeSaveManager
    {
        private readonly IFileSystem _fileSystem;

        /// <summary>用默认磁盘文件系统构造。</summary>
        public NarrativeSaveManager() : this(null) { }

        /// <summary>注入文件系统构造（传 null 用 <see cref="DiskFileSystem"/>）。</summary>
        public NarrativeSaveManager(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem ?? new DiskFileSystem();
        }

        /// <summary>把存档 DTO 序列化成 JSON。</summary>
        public static string ToJson(NarrativeSaveData data, bool prettyPrint = true)
        {
            return data == null ? string.Empty : JsonUtility.ToJson(data, prettyPrint);
        }

        /// <summary>把 JSON 反序列化成存档 DTO；空串返回 null。</summary>
        public static NarrativeSaveData FromJson(string json)
        {
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<NarrativeSaveData>(json);
        }

        /// <summary>把存档写到文件（JSON）。data 为 null 时不写、返回 false。</summary>
        public bool Save(string path, NarrativeSaveData data)
        {
            if (data == null || string.IsNullOrEmpty(path))
            {
                return false;
            }

            _fileSystem.WriteAllText(path, ToJson(data));
            return true;
        }

        /// <summary>从文件读回存档；文件不存在或路径为空返回 null。</summary>
        public NarrativeSaveData Load(string path)
        {
            if (string.IsNullOrEmpty(path) || !_fileSystem.Exists(path))
            {
                return null;
            }

            return FromJson(_fileSystem.ReadAllText(path));
        }

        /// <summary>文件是否存在。</summary>
        public bool Exists(string path)
        {
            return !string.IsNullOrEmpty(path) && _fileSystem.Exists(path);
        }
    }
}
