using System;

namespace CodeType.Classes
{
    public class CodeRepo
    {
        public string Name { get; set; }
        public string RepoPath { get; set; }
        public CodeLanguage Language { get; set; }
        public bool UseFolders { get; set; }
    }
}