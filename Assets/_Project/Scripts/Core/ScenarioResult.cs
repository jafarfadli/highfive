using System.Collections.Generic;

namespace SeoulFirstWeek.Core
{
    public enum ScenarioOutcome { Completed, Blocked, Abandoned }

    public class PhraseLine
    {
        public string Korean;
        public string Romanization;
        public string Meaning;
    }

    public class ScenarioResult
    {
        public string ScenarioId;
        public string Title;
        public ScenarioOutcome Outcome;
        public int StepsCompleted;
        public int StepsTotal;
        public int Mistakes;
        public float Seconds;
        public string Lesson;
        public bool Verified;
        public List<string> Granted = new();
        public List<string> Tips = new();
        public List<PhraseLine> PhraseCard = new();

        public float Progress => StepsTotal > 0 ? StepsCompleted / (float)StepsTotal : 0f;
    }
}
