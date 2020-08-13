using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using Newtonsoft.Json;

namespace CodeType.Classes
{
    public static class GitHub
    {
        public static List<CodeRepo> CodeRepos = new List<CodeRepo>
        {
            new CodeRepo
            {
                Name = "Flask",
                Language = CodeLanguage.Python,
                RepoPath = "pallets/flask/contents/src/flask"
            },
            new CodeRepo
            {
                Name = "ASP.NET Core MVC",
                Language = CodeLanguage.CSharp,
                RepoPath = "dotnet/aspnetcore/contents/src/Mvc/Mvc.Core/src",
                UseFolders = true
            },
            new CodeRepo
            {
                Name = "Linux",
                Language = CodeLanguage.C,
                RepoPath = "torvalds/linux/contents/kernel"
            },
            new CodeRepo
            {
                Name = "QMK",
                Language = CodeLanguage.C,
                RepoPath = "qmk/qmk_firmware/contents/quantum"
            },
            new CodeRepo
            {
                Name = "Express",
                Language = CodeLanguage.JavaScript,
                RepoPath = "expressjs/express/contents/lib",
                UseFolders = true
            }
        };
        
        /// <summary>
        /// Get code source to be used in the test.
        /// </summary>
        /// <param name="client">An instance of HttpClient to use to make API calls.</param>
        /// <param name="jsRuntime">An IJSRuntime to use for JavaScript interop.</param>
        /// <param name="repo">A CodeRepo to get code from.</param>
        /// <param name="lines">The number of lines of code to get.</param>
        /// <param name="fromStart">Whether to get the code from the start of the file or to get it from a random location.</param>
        /// <param name="includeComments">Whether or not to include single-line comments in the code source.</param>
        /// <returns>A list of strings, where each item is a line in the source.</returns>
        public static async Task<List<string>> GetCodeSource(HttpClient client, IJSRuntime jsRuntime, CodeRepo repo, int lines, bool fromStart, bool includeComments)
        {
            Random rnd = new Random();

            string fileContents = await GetRandomRepoFile(client, rnd, repo);

            List<string> initialLines = repo.Language == CodeLanguage.Python
                ? await ProcessLinesPython(jsRuntime, fileContents, includeComments)
                : await ProcessLinesC(jsRuntime, fileContents, includeComments,
                    repo.Language.Enum);

            if (initialLines.Count > lines)
            {
                if (fromStart)
                {
                    return initialLines.Take(lines).ToList();
                }

                int start = rnd.Next(0, initialLines.Count - lines);
                return initialLines.GetRange(start, lines);
            }

            // We need more lines
            return initialLines.Concat(await GetCodeSource(client, jsRuntime, repo,
                lines - initialLines.Count, fromStart, includeComments)).ToList();
        }

        private static async Task<string> GetRandomRepoFile(HttpClient client, Random rnd, CodeRepo repo, string subdirectory = "")
        {
            string jsonFilesInRepo =
                await client.GetStringAsync("https://api.github.com/repos/" + repo.RepoPath + subdirectory);
            List<dynamic> filesInRepo = JsonConvert.DeserializeObject<List<dynamic>>(jsonFilesInRepo)
            .Where(repoItem => (repo.UseFolders || repoItem.type == "file") && (repoItem.type == "dir" ||
                ((string) repoItem.name).EndsWith(repo.Language.FileExtension))).ToList();
            dynamic randomRepoItem = filesInRepo[rnd.Next(filesInRepo.Count)];
            
            if (randomRepoItem.type == "dir")
            {
                return await GetRandomRepoFile(client, rnd, repo, subdirectory + "/" + (string) randomRepoItem.name);
            }
            
            string repoFilePath = randomRepoItem._links.self;
            string jsonFileContents = await client.GetStringAsync(repoFilePath);
            dynamic fileContents = JsonConvert.DeserializeObject(jsonFileContents);
            if (fileContents is null)
            {
                throw new JsonException("Deserialization returned null object");
            }
            string fileString = (string) fileContents.content;
            // Remove non-unicode characters - for whatever reason some of the C# files had zero-width spaces in them
            return Encoding.ASCII.GetString(
                Encoding.Convert(
                    Encoding.UTF8, Encoding.GetEncoding(
                        Encoding.ASCII.EncodingName,
                        new EncoderReplacementFallback(string.Empty),
                        new DecoderExceptionFallback()),
                    Encoding.UTF8.GetBytes(Base64Decode(fileString))));
        }

        /// <summary>
        /// Process Python code to be used in the test.
        /// </summary>
        private static async Task<List<string>> ProcessLinesPython(IJSRuntime jsRuntime, string fileContents, bool includeComments)
        {
            // The escaped regex isn't easy to read
            // Here's the original:
            // ((((?!""").)*)"""(((?!""")[\s\S])*)"""(((?!""").)*))+
            // This removes any lines with multi-line comments from the source
            string removeLongCommentsRegex = @"((((?!"""""").)*)""""""(((?!"""""")[\s\S])*)""""""(((?!"""""").)*))+";
            // Run the regex in JS for better performance
            string newFileContents = await JsRegexRemoveMatches(jsRuntime, removeLongCommentsRegex, fileContents);
            if (!includeComments)
            {
                newFileContents = await JsRegexRemoveMatches(jsRuntime, "(#.*)+", newFileContents);
            }
            List<string> fileLines = newFileContents
                .Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.RemoveEmptyEntries).ToList();
            return fileLines
                .Skip(fileLines.FindIndex(line => !line.Contains("import")))
                .Where(line => line.TrimStart().Length != 0)
                .ToList();
        }

        /// <summary>
        /// Process C code (or code of C-style languages) to be used in the test.
        /// </summary>
        private static async Task<List<string>> ProcessLinesC(IJSRuntime jsRuntime, string fileContents,
            bool includeComments, CodeLanguageEnum language)
        {
            // Regex to remove /* */ comments
            // (\/\*(((?!\/\*)[\s\S])*)\*\/)+
            string newFileContents = await JsRegexRemoveMatches(jsRuntime, "(\\/\\*(((?!\\/\\*)[\\s\\S])*)\\*\\/)+", fileContents);
            if (!includeComments)
            {
                newFileContents = await JsRegexRemoveMatches(jsRuntime, "(\\/\\/.*)+", newFileContents);
            }
            newFileContents = newFileContents.Replace("\t", "    ");
            List<string> fileLines = newFileContents
                .Split(new[] {"\r\n", "\r", "\n"}, StringSplitOptions.RemoveEmptyEntries).ToList();
            
            IEnumerable<string> newFileLines = language switch
            {
                CodeLanguageEnum.C => fileLines.Skip(fileLines.FindIndex(line => !line.Contains("#include"))),
                CodeLanguageEnum.CSharp => fileLines.Skip(fileLines.FindIndex(line => !line.Contains("using "))),
                CodeLanguageEnum.JavaScript => fileLines.Skip(fileLines.FindIndex(line => !line.Contains(" require("))),
                _ => throw new ArgumentOutOfRangeException()
            };
            
            return newFileLines
                .Where(line => line.TrimStart().Length != 0)
                // Some lines have a bunch of spaces at the end, and then a comment
                .Select(line => line.TrimEnd())
                .ToList();
        }
        
        private static string Base64Decode(string base64EncodedData) {
            byte[] base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }

        /// <summary>
        /// Remove matches of a regex from contents using JavaScript.
        /// </summary>
        private static async Task<string> JsRegexRemoveMatches(IJSRuntime jsRuntime, string regex, string contents)
        {
            return await jsRuntime.InvokeAsync<string>("regexRemoveMatches", regex, contents);
        }
    }
}