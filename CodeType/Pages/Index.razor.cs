using System;
using System.Collections.Generic;
using CodeType.Classes;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace CodeType.Pages
{
    public partial class Index
    {
        [Inject] private IJSRuntime JSRuntime { get; set; }
        [Inject] private HttpClient Client { get; set; }
        
        /// <summary>
        /// The position of the cursor on the current line.
        /// </summary>
        private int CursorPosition { get; set; }

        /// <summary>
        /// All of the letters in the test code. Each item in the outer list is a line, and each item in the line is a letter.
        /// </summary>
        private List<List<CodeLetter>> Letters { get; set; } = new List<List<CodeLetter>>();
        private int CurrentLine { get; set; }
        
        /// <summary>
        /// The source for the current test. Each item in the list is a line.
        /// </summary>
        private List<string> SampleSource { get; set; } = new List<string>();
        
        /// <summary>
        /// The number of spaces before the current line, used to render the indentation without making it part of the code that needs to be typed.
        /// </summary>
        private int IndentationLevel { get; set; }
        private bool TestActive { get; set; }
        private double CharactersPerMinute { get; set; }
        private double WordsPerMinute { get; set; }
        private bool IsLoading { get; set; } = true;
        private bool AutoAdvanceLine { get; set; }
        private bool DisableNavigation { get; set; }
        private bool IncludeComments { get; set; }
        private bool FromStart { get; set; }
        private string CodeSource { get; set; } = GitHub.CodeRepos[0].Name;
        private int TestLines { get; set; }

        private bool _compatibilityMode;

        /// <summary>
        /// If enabled, the test will use a timer to check every two seconds if the test has been finished instead of checking every time a key is pressed.
        /// </summary>
        private bool CompatibilityMode
        {
            get => _compatibilityMode;
            set
            {
                _compatibilityMode = value;
                
                if (CompatibilityMode)
                {
                    CheckIfOverTimer.Enabled = true;
                }
            }
        }
        
        /// <summary>
        /// The time of the last keypress, in units of ticks. Used for compatibility mode.
        /// </summary>
        private long LastKeyTimestamp { get; set; }
        private Timer CheckIfOverTimer { get; set; } = new Timer(2000);
        
        private Stopwatch TestStopwatch { get; set; } = new Stopwatch();

        protected override async Task OnInitializedAsync()
        {
            await InitJSObjectReference();

            CheckIfOverTimer.Elapsed += CheckIfOver;
            CheckIfOverTimer.AutoReset = true;

            TestLines = 20;
            
            await InitTest();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await JSRuntime.InvokeVoidAsync("initFomanticElements");
            }
        }
        
        /// <summary>
        /// Pass the object reference to JavaScript so it can call the C# code on a key press.
        /// </summary>
        private async Task InitJSObjectReference()
        {
            await JSRuntime.InvokeVoidAsync("initObjectReference", DotNetObjectReference.Create(this));
        }

        
        /// <summary>
        /// Called by the JavaScript whenever a key is pressed.
        /// </summary>
        /// <param name="key">The key property of the keydown event.</param>
        [JSInvokable("JSKeyDown")]
        public void JSKeyDown(string key)
        {
            if (TestActive)
            {
                ProcessKey(key);
            }
        }
        
        /// <summary>
        /// Process a keypress.
        /// </summary>
        /// <param name="key">The key property of the keydown event.</param>
        private void ProcessKey(string key)
        {
            if (!DisableNavigation)
            {
                if (key == "ArrowLeft" && CursorPosition != 0)
                {
                    CursorPosition--;
                }
                else if (key == "ArrowRight" && CursorPosition != Letters[CurrentLine].Count)
                {
                    CursorPosition++;
                }
                else if (key == "ArrowUp" && CurrentLine != 0)
                {
                    CurrentLine--;
                    InitLine();
                    UpdateCursorPosition();
                }
                else if (key == "ArrowDown" && CurrentLine != Letters.Count - 1)
                {
                    CurrentLine++;
                    InitLine();
                    UpdateCursorPosition();
                }
            }
            
            if (key == "Enter" && CurrentLine != Letters.Count - 1 && (!DisableNavigation || !Letters[CurrentLine].Select(letter => letter.State == LetterState.Correct).Contains(false)))
            {
                CursorPosition = 0;
                CurrentLine++;
                InitLine();
            }
            else
            {
                // A typing key was pressed
                if (key == "Backspace")
                {
                    if (CursorPosition != 0)
                    {
                        DeleteLetter(CursorPosition - 1);
                        CursorPosition--;
                    }
                    else if (CurrentLine != 0)
                    {
                        CurrentLine--;
                        InitLine();
                        CursorPosition = Letters[CurrentLine].FindLastIndex(letter => letter.State != LetterState.NotTyped) + 1;
                    }
                }
                else if (key == "Delete" && CursorPosition != Letters[CurrentLine].Count)
                {
                    DeleteLetter(CursorPosition);
                }
                else if(key.ToList() is { } charList && charList.Count == 1)
                {
                    if (!TestStopwatch.IsRunning)
                    {
                        TestStopwatch.Start();
                    }
                    
                    if (CursorPosition != Letters[CurrentLine].Count && charList[0] == Letters[CurrentLine][CursorPosition].Letter && Letters[CurrentLine][CursorPosition].State == LetterState.NotTyped)
                    {
                        Letters[CurrentLine][CursorPosition].State = LetterState.Correct;
                        if (AutoAdvanceLine && CursorPosition == Letters[CurrentLine].Count - 1 && CurrentLine != Letters.Count - 1 && !Letters[CurrentLine].Select(letter => letter.State == LetterState.Correct).Contains(false))
                        {
                            CursorPosition = 0;
                            CurrentLine++;
                            InitLine();
                        }
                        else
                        {
                            CursorPosition++;
                        }
                    }
                    else
                    {
                        Letters[CurrentLine].Insert(CursorPosition, new CodeLetter
                        {
                            Letter = charList[0],
                            State = LetterState.Incorrect
                        });
                        CursorPosition++;
                    }
                    if (CompatibilityMode)
                    {
                        LastKeyTimestamp = TestStopwatch.ElapsedTicks;
                    }
                }

                if (!CompatibilityMode)
                {
                    Task.Run(CheckIfOver);
                }
            }
            
            StateHasChanged();
        }

        /// <summary>
        /// Delete a letter from Letters.
        /// </summary>
        /// <param name="letterIndex">The index of the letter to delete.</param>
        private void DeleteLetter(int letterIndex)
        {
            switch (Letters[CurrentLine][letterIndex].State)
            {
                case LetterState.Correct:
                {
                    Letters[CurrentLine][letterIndex].State = LetterState.NotTyped;
                    break;
                }
                case LetterState.Incorrect:
                {
                    Letters[CurrentLine].RemoveAt(letterIndex);
                    break;
                }
            }
        }

        /// <summary>
        /// Update the cursor position after changing lines.
        /// </summary>
        private void UpdateCursorPosition()
        {
            if (Letters[CurrentLine].Count is {} newLineCount && CursorPosition > newLineCount)
            {
                CursorPosition = newLineCount;
            }
        }
        
        /// <summary>
        /// Check if the test is complete (all letters are correct).
        /// </summary>
        private bool IsTestOver()
        {
            return !Letters.SelectMany(i => i).Select(letter => letter.State == LetterState.Correct).Contains(false);
        }
        
        /// <summary>
        /// Set the indentation level for the current line.
        /// </summary>
        private void InitLine()
        {
            IndentationLevel = SampleSource[CurrentLine].Length - SampleSource[CurrentLine].TrimStart().Length;
        }

        /// <summary>
        /// Get sample code to be used in the test.
        /// </summary>
        /// <returns>A list of strings, where each item is a line.</returns>
        private async Task<List<string>> GetSampleSource()
        {
            try
            {
                return await GitHub.GetCodeSource(Client, JSRuntime,
                    GitHub.CodeRepos.Find(repo => repo.Name == CodeSource), TestLines, FromStart, IncludeComments);
            }
            catch (Exception ex)
            {
                if (ex is HttpRequestException && ex.Message.Contains("403"))
                {
                    // User has probably reached the GitHub API rate limit
                    return new List<string>
                    {
                        "403 Error",
                        "The GitHub API imposes a rate limit, which means that the app can only get sample",
                        "code 20-30 times per hour. You have reached that limit, wait a bit and try again."
                    };
                }

                throw;
            }
        }
        
        /// <summary>
        /// Start/reset the test.
        /// </summary>
        /// <param name="getNewSource">Whether to get new code or to use the contents of SampleSource.</param>
        /// <returns></returns>
        private async Task InitTest(bool getNewSource=true)
        {
            IsLoading = true;
            StateHasChanged();

            if (getNewSource)
            {
                SampleSource = await GetSampleSource();
            }

            Letters = SampleSource.Select(lineSource => lineSource.TrimStart().ToList().Select(letter => new CodeLetter
            {
                Letter = letter,
                State = LetterState.NotTyped
            }).ToList()).ToList();
            
            CursorPosition = 0;
            CurrentLine = 0;
            InitLine();
            TestActive = true;
            IsLoading = false;
            LastKeyTimestamp = 0;
            TestStopwatch.Reset();

            if (CompatibilityMode)
            {
                CheckIfOverTimer.Enabled = true;
            }
        }

        private void CheckIfOver(object sender, ElapsedEventArgs e)
        {
            CheckIfOver();
        }
        
        /// <summary>
        /// Check if the test should be over; if it should, end the test and calculate results.
        /// </summary>
        private void CheckIfOver()
        {
            if (!IsTestOver()) return;
            TestStopwatch.Stop();
            CheckIfOverTimer.Enabled = false;

            CharactersPerMinute = Math.Round(GetCharacterLength() / (CompatibilityMode ? TimeSpan.FromTicks(LastKeyTimestamp) : TestStopwatch.Elapsed).TotalMinutes, 2);
            // 4.7 is average English word length
            WordsPerMinute = Math.Round(CharactersPerMinute / 4.7);
            
            TestActive = false;
            StateHasChanged();
        }

        /// <summary>
        /// Get number of characters in SampleSource.
        /// </summary>
        private int GetCharacterLength()
        {
            return SampleSource.SelectMany(lineSource => lineSource.TrimStart()).Count();
        }
    }
}