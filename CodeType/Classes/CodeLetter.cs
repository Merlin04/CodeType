namespace CodeType.Classes
{
    public enum LetterState
    {
        Correct,
        Incorrect,
        NotTyped
    }
    
    public class CodeLetter
    {
        public char Letter { get; set; }
        public LetterState State { get; set; }
    }
}