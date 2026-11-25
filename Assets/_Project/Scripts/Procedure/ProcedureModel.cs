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
        /// </summary>
        public string mode = "ticket";
        /// <summary>Walk-in places: the patients called in before the player, by name, in order.</summary>
        public List<string> names = new();

        [JsonIgnore] public bool WalkIn => mode == "walkin";
    }

    /// <summary>One answer the player can pick. The result decides what the procedure does next.</summary>
    public class ChoiceDef
    {
        public string ko;
        public string rom;
        public LocText meaning;
        public string result = "retry"; // advance | retry | block | fetch | ask (asks to repeat: not a mistake)
        public string reply;            // what the staff member says back
        public LocText replyMeaning;
        public LocText note;            // why this answer doesn't work (shown after a wrong pick)
        public string give;             // document handed over with this answer
        public string needs;            // only offered when the player has this flag
        public string unless;           // hidden when the player has this flag
        public List<string> grants = new(); // flags or documents this answer earns

        [JsonIgnore] public bool Advances => result == "advance";
    }

    public class StepDef
    {
        public string id;
        public string staff;
        public string location = "counter";
        public string announce; // PA line when the player is called to a new location ({0} = player name)
        public LocText task;    // the player's to-do from this step on (shown in the quest panel)
        public string type = "choice"; // choice | keypad
        public string npc;
        public LocText meaning;
        public List<ChoiceDef> choices = new();
        public string ack;
        public string after;
        public LocText afterMeaning;
        public FetchDef fetch;
        public bool confirmPrevious;
        public List<string> grants = new();
        public List<string> takes = new();
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

        public StaffDef Staff(string id) => staff.Find(s => s.id == id) ?? staff[0];

        public static ProcedureDef Parse(string json) => JsonConvert.DeserializeObject<ProcedureDef>(json);
    }
}
