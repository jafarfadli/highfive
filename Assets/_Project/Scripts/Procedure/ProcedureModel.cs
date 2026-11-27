using System.Collections.Generic;
using Newtonsoft.Json;
using SeoulFirstWeek.Core;

namespace SeoulFirstWeek.Procedure
{
    /// <summary>Text keyed by language code ("en", "vi", "id").</summary>
    public class LocText : Dictionary<string, string>
    {
        public string Get()
        {
            var code = GameSession.Exists ? GameSession.I.LangCode : Loc.Code(Settings.Language);
            if (TryGetValue(code, out var s) && !string.IsNullOrEmpty(s)) return s;
            return TryGetValue("en", out var en) ? en : "";
        }
    }

    public class PhraseDef
    {
        public string ko;
        public string rom;
        public LocText meaning;
        public string needs;  // a phrase that only fits one situation (sit:…) is shown only for it
        public string unless;
    }

    public class StaffDef
    {
        public string id;
        public string name;
        public string role;
        public LocText roleText;
        public string openaiVoice = "coral";
        public string macVoice = "Yuna";
        public int macRate = 185;
    }

    public class FetchDef
    {
        public string document;
        public string station;
        public LocText objective;
    }

    public class QueueDef
    {
        public int counter = 2;
        public int ahead = 3;
        public float interval = 6f;
        public string callLine = "딩동. {0}번 고객님, {1}번 창구로 오세요.";
        /// <summary>
        /// "ticket" (default): take a 번호표 and wait for the number board, as at a bank or a public office.
        /// "walkin": walk straight up to the reception, then sit and wait to be called by name, as at a
        /// neighbourhood clinic.
        /// "none": no counter at all; the first conversation starts wherever the player is (a phone call).
        /// </summary>
        public string mode = "ticket";
        /// <summary>Walk-in places: the patients called in before the player, by name, in order.</summary>
        public List<string> names = new();

        [JsonIgnore] public bool WalkIn => mode == "walkin";
        [JsonIgnore] public bool None => mode == "none";
    }

    /// <summary>One answer the player can pick. The result decides what the procedure does next.</summary>
    public class ChoiceDef
    {
        public string ko;
        public string rom;
        public LocText meaning;
        // advance | retry | block | fetch | ask (asks to repeat: not a mistake) | branch (a fair thing to say that
        // isn't the expected answer: the staff member answers it, the answer leaves the list and the same question
        // stays, so the talk turns aside and comes back; never a mistake)
        public string result = "retry";
        public string reply;            // what the staff member says back
        public LocText replyMeaning;
        public LocText note;            // why this answer doesn't work (shown after a wrong pick)
        public string give;             // document handed over with this answer
        public string needs;            // only offered when the player has this flag
        public string unless;           // hidden when the player has this flag
        public List<string> grants = new(); // flags or documents this answer earns

        [JsonIgnore] public bool Advances => result == "advance";
    }

    /// <summary>A place in the world that is the wrong one for a `go` step: using it is a mistake with a lesson.</summary>
    public class WrongTurnDef
    {
        public string station;   // the waypoint's id
        public string line;      // what the step's staff member says (optional)
        public LocText lineMeaning;
        public LocText note;     // the lesson
    }

    public class StepDef
    {
        public string id;
        public string staff;
        public string location = "counter";
        public string announce; // PA line when the player is called to a new location ({0} = player name); a go step plays it when it starts
        public LocText task;    // the player's to-do from this step on (shown in the quest panel)
        /// <summary>
        /// choice: a staff line and answers. keypad: the PIN pad (or a phone's dial pad with <see cref="expect"/>).
        /// go: no conversation; the player goes somewhere in the world (a <see cref="target"/> waypoint).
        /// </summary>
        public string type = "choice";
        public string npc;
        public LocText meaning;
        public List<ChoiceDef> choices = new();
        public string ack;
        public string after;          // a closing line; after a go step, a PA line (the in-car announcement)
        public LocText afterMeaning;
        public FetchDef fetch;
        public bool confirmPrevious;
        public List<string> grants = new();
        public List<string> takes = new();
        // go steps
        public string target;         // the waypoint that completes the step
        public List<WrongTurnDef> wrong = new();
        // keypad steps used as a phone
        public string expect;         // the only number accepted (e.g. "119")
        public LocText prompt;        // the title on the pad ("Dial 119")
        public LocText expectNote;    // the lesson for a wrong number

        // a personalised visit: the step only happens in a situation that sets (or doesn't set) this flag
        public string needs;
        public string unless;

        [JsonIgnore] public bool IsGo => type == "go";
    }

    /// <summary>A lesson for one situation, shown on the results screen (why the bill was what it was).</summary>
    public class NoteDef
    {
        public string needs;
        public string unless;
        public LocText note;
    }

    public class ProcedureDef
    {
        public string id;
        public bool verified;
        public List<string> sources = new();
        public string titleKo;
        public LocText title;
        public LocText briefing;
        public LocText completeNote;
        public LocText blockedLesson;
        public List<StaffDef> staff = new();
        public QueueDef queue = new();
        public List<StepDef> steps = new();
        public List<string> grants = new();
        public List<string> carry = new(); // documents the player brings to this visit (named in the story)
        public List<PhraseDef> phrases = new();
        public List<PhraseDef> glossary = new(); // key words for the Logbook
        public List<NoteDef> notes = new();      // lessons for the situation played (personalised visits)

        public StaffDef Staff(string id) => staff.Find(s => s.id == id) ?? staff[0];

        public static ProcedureDef Parse(string json) => JsonConvert.DeserializeObject<ProcedureDef>(json);
    }
}
