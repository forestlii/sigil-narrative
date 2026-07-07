// Copyright (c) 2026 Likeon. Licensed under the MIT License.
// 文件读写抽象，供 NarrativeSaveManager 注入。默认走磁盘（DiskFileSystem）；测试可注入内存假实现，
// 从而在 EditMode 里验证存/读逻辑而不碰真实文件系统（对应设计 D3“注入 IFileSystem”）。

namespace Likeon.Narrative
{
    /// <summary>文件读写的最小抽象。单机存档只需要“存在性 + 读全文 + 写全文”。</summary>
    public interface IFileSystem
    {
        /// <summary>指定路径是否存在文件。</summary>
        bool Exists(string path);

        /// <summary>读取整个文件为字符串。</summary>
        string ReadAllText(string path);

        /// <summary>把字符串整体写入文件（覆盖），必要时创建父目录。</summary>
        void WriteAllText(string path, string contents);
    }
}
