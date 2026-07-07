// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// IFileSystem 的默认磁盘实现（System.IO）。NarrativeSaveManager 不注入时用它。
// 典型路径用 Application.persistentDataPath 拼（由调用方给出），本类只负责读写。

using System.IO;

namespace Likeon.Narrative
{
    /// <summary>基于 <see cref="System.IO"/> 的默认文件系统实现。</summary>
    public sealed class DiskFileSystem : IFileSystem
    {
        /// <inheritdoc/>
        public bool Exists(string path) => File.Exists(path);

        /// <inheritdoc/>
        public string ReadAllText(string path) => File.ReadAllText(path);

        /// <inheritdoc/>
        public void WriteAllText(string path, string contents)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, contents);
        }
    }
}
