namespace CodeType.Classes
{
    // This is necessary so switch statements/expressions can be used
    public enum CodeLanguageEnum
    {
        Python,
        CSharp,
        C,
        JavaScript
    }

    public class CodeLanguage
    {
        /// <summary>
        /// The name that is displayed next to the repo name in the dropdown.
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// The file extension the language uses.
        /// </summary>
        public string FileExtension { get; set; }
        
        /// <summary>
        /// The matching CodeLanguageEnum.
        /// </summary>
        public CodeLanguageEnum Enum { get; set; }

        public static readonly CodeLanguage Python = new CodeLanguage
        {
            Name = "Python",
            FileExtension = "py",
            Enum = CodeLanguageEnum.Python
        };

        public static readonly CodeLanguage CSharp = new CodeLanguage
        {
            Name = "C#",
            FileExtension = "cs",
            Enum = CodeLanguageEnum.CSharp
        };

        public static readonly CodeLanguage C = new CodeLanguage
        {
            Name = "C",
            FileExtension = "c",
            Enum = CodeLanguageEnum.C
        };

        public static readonly CodeLanguage JavaScript = new CodeLanguage
        {
            Name = "JS",
            FileExtension = "js",
            Enum = CodeLanguageEnum.JavaScript
        };
    }
}