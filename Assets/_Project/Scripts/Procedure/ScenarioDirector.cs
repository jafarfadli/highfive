using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SeoulFirstWeek.Audio;
using SeoulFirstWeek.Conversation;
using SeoulFirstWeek.Core;
using SeoulFirstWeek.Player;
using SeoulFirstWeek.Telemetry;
using SeoulFirstWeek.UI;
using UnityEngine;

namespace SeoulFirstWeek.Procedure
{
    /// <summary>
    /// Runs one level inside a location: queue ticket, waiting, then the counter conversation. A walk-in
    /// place (`queue.mode`: "walkin", the clinic) has no ticket: the player walks up to the reception,
    /// then sits and waits to be called by name, walks into the treatment room and back out to pay.
    /// Every staff line comes with a set of answers; the answer's result (advance / retry / block /
    /// fetch) from the procedure data decides what happens, so the data stays the source of truth.
    /// </summary>
    public class ScenarioDirector : MonoBehaviour
    {
        [Tooltip("Every procedure this location can run (file name = procedure id); the current level picks one.")]
        public TextAsset[] procedures;
        public FirstPersonController player;
        public TicketMachine ticketMachine;
        public QueueBoard[] boards;
        [Tooltip("One per service window.")]
        public CounterSpot[] counters;
        public QueueCrowd crowd;
        public NpcActor[] actors;
        public Transform[] anchors;
        public FetchStation[] stations;
        [Tooltip("Walk-in places: the treatment-room doors the player walks through when called.")]
        public RoomEntrance[] rooms;
        [Tooltip("Walk-in places: the waiting monitor that lists names (진료 대기 현황).")]
        public WaitingBoard waitingBoard;

        public bool Finished { get; private set; }

        /// <summary>The window this procedure calls the player to.</summary>
        public CounterSpot counter { get; private set; }

        /// <summary>The answers on screen, in display order (for editor play-tests).</summary>
        public List<ChoiceDef> ShownChoices { get; private set; } = new();

        // Stages before the conversation: reach the building (done on arrival), take a number (not at a
        // walk-in place), reach the counter.
        int LeadStages => proc.queue.WalkIn ? 2 : 3;

        static readonly VoiceSpec AnnouncerVoice = new() { Id = "pa", OpenAiVoice = "alloy", MacVoice = "Yuna", MacRate = 170 };
        static readonly VoiceSpec PlayerVoice = new() { Id = "player", OpenAiVoice = "nova", MacVoice = "Flo (Korean (South Korea))", MacRate = 175 };

        ProcedureDef proc;
        LevelDef level;
        GameSession session;
        VoiceService voice;
        Hud hud;
        ConversationPanel panel;
        PlayerInteractor interactor;
        AudioSource announcer, playerMouth;

        int progress = 1, mistakes;
        int stepIndex;
        Color accent;
        float startTime;
        readonly List<string> tips = new();
        List<(string text, string meaning)> lastLines = new();
        NpcActor actor;
        string location;
        string pin;
        bool speaking;
        readonly List<string> carried = new();
        CancellationTokenSource replayCts;
        CancellationToken ct;

        int TotalStages => LeadStages + proc.steps.Count;

        async void Start()
        {
            ct = destroyCancellationToken;
            try
            {
                await Run();
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void OnDestroy()
        {
            replayCts?.Cancel();
            if (panel) panel.ReplayRequested -= Replay;
            // the papers brought for this visit stay with it; they don't join the saved inventory
            if (GameSession.Exists)
                foreach (var doc in carried) GameSession.I.TakeDocument(doc);
        }

        async Task Run()
        {
            session = GameSession.I;
            voice = session.Voice;
            // the part being played decides the procedure; played straight from the editor, the first one
            var wanted = session.CurrentLevel?.procedure;
            var json = procedures.FirstOrDefault(p => p && p.name == wanted) ?? procedures[0];
            proc = ProcedureDef.Parse(json.text);
            level = session.EnsureLevel(proc.id);
            counter = counters.FirstOrDefault(c => c && c.counterNumber == proc.queue.counter) ?? counters[0];
            if (counter.chair)
                foreach (var c in counter.chair.GetComponentsInChildren<Collider>()) c.enabled = false;
            interactor = player.GetComponent<PlayerInteractor>();
            announcer = gameObject.AddComponent<AudioSource>();
            announcer.spatialBlend = 0;
            playerMouth = gameObject.AddComponent<AudioSource>();
            playerMouth.spatialBlend = 0;
            startTime = Time.time;

            // documents picked up during an earlier attempt don't carry over
            foreach (var s in proc.steps)
                if (s.fetch != null && session.HasDocument(s.fetch.document))
                    session.TakeDocument(s.fetch.document);
            // what the player brought from home for this visit (the story names it)
            foreach (var doc in proc.carry.Where(d => !session.HasDocument(d)))
            {
                session.GiveDocument(doc, false, false);
                carried.Add(doc);
            }

            hud = Hud.Create();
            panel = ConversationPanel.Create(hud, player);
            panel.ReplayRequested += Replay;
            PauseMenu.Create(player);
            CursorState.Gameplay(true);
            foreach (var a in actors)
            {
                var def = proc.Staff(a.staffId);
                a.Voice = new VoiceSpec { Id = def.id, OpenAiVoice = def.openaiVoice, MacVoice = def.macVoice, MacRate = def.macRate };
            }
            accent = level.Place != null ? level.Place.Color : Palette.Accent;
            panel.Accent = accent;
            hud.SetQuest(level.title.Get(), $"{proc.titleKo} · {Loc.T("part_n", level.level)}", accent);
            UpdateProgress();
            hud.ShowHelp(Loc.T("controls"), 12f);
            hud.Banner(proc.titleKo, level.FullTitle, 2.6f);
            if (proc.carry.Count > 0)
                Toasts.Show(Loc.T("brought"), string.Join(" · ", proc.carry.Select(Documents.Korean)), 6f);
            if (!proc.verified) Debug.Log($"[Procedure] '{proc.id}' is a draft (verified: false). Check it against: {string.Join(", ", proc.sources)}");

            var q = proc.queue;
            if (q.WalkIn) await WalkUp();
            else await TakeNumber();

            // 4. the conversation
            await EnterConversation(proc.steps[0].location, false);
            var carry = new List<(string, string)>();
            for (stepIndex = 0; stepIndex < proc.steps.Count; stepIndex++)
            {
                var step = proc.steps[stepIndex];
                hud.SetTask(TaskFor(stepIndex));
                if (step.location != location)
                {
                    await Say(carry);
                    carry.Clear();
                    if (q.WalkIn) await WalkTo(step);
                    else await MoveTo(step);
                }
                if (stepIndex + 1 < proc.steps.Count) Prefetch(proc.steps[stepIndex + 1]);

                var outcome = await RunStep(step, carry);
                if (outcome.Blocked)
                {
                    await Finish(ScenarioOutcome.Blocked);
                    return;
                }
                Advance();
                foreach (var g in step.grants) session.Grant(g);
                foreach (var t in step.takes) session.TakeDocument(t);
                carry = outcome.Reaction;
                if (!string.IsNullOrEmpty(step.after)) carry.Add((step.after, step.afterMeaning?.Get()));
            }
            await Say(carry);
            await Finish(ScenarioOutcome.Completed);
        }

        /// <summary>A bank or public office: take a number, wait for the board and the PA, walk to the counter.</summary>
        async Task TakeNumber()
        {
            var q = proc.queue;
            // 1. queue number
            hud.SetTask(Loc.T("take_ticket"));
            await ticketMachine.WaitAsync(ct);
            Advance();

            int serving = 40 + UnityEngine.Random.Range(0, 50);
            int mine = serving + q.ahead + 1;
            foreach (var b in boards) b.Show(serving, 1);
            hud.ShowTicket(mine, serving);
            Prefetch(proc.steps[0]);

            // 2. wait for the number board
            while (serving < mine - 1)
            {
                hud.SetTask(Loc.T("obj_wait_n", mine.ToString("000")));
                await Awaitable.WaitForSecondsAsync(q.interval + UnityEngine.Random.Range(-1f, 1.5f), ct);
                serving++;
                int other = OtherCounter(q.counter);
                foreach (var b in boards) b.Show(serving, other);
                hud.ShowTicket(mine, serving);
                crowd?.CallTo(other);
                _ = Announce(0.55f, q.callLine, serving, other);
            }
            await Awaitable.WaitForSecondsAsync(q.interval * 0.7f, ct);
            foreach (var b in boards) b.Show(mine, q.counter);
            hud.ShowTicket(mine, mine);
            hud.Banner($"{mine}번 고객님, {q.counter}번 창구로 오세요", Loc.T("obj_go_counter", q.counter), 4f);
            await Announce(1f, q.callLine, mine, q.counter);

            // 3. walk to the counter
            hud.SetTask(Loc.T("obj_go_counter", q.counter));
            counter.staffName = proc.Staff(proc.steps[0].staff).name;
            counter.counterNumber = q.counter;
            await counter.WaitAsync(ct);
            hud.HideTicket();
            Advance();
        }

        /// <summary>
        /// A neighbourhood clinic: no ticket and no number board. The player walks straight up to the
        /// reception; other patients register at the other counter and wait in the seats.
        /// </summary>
        async Task WalkUp()
        {
            var q = proc.queue;
            hud.SetTask(TaskFor(0));
            counter.staffName = proc.Staff(proc.steps[0].staff).name;
            counter.counterNumber = q.counter;
            if (crowd) crowd.reception = Spot(OtherCounter(q.counter));
            if (waitingBoard) waitingBoard.Queue(q.names);
            Prefetch(proc.steps[0]);
            await counter.WaitAsync(ct);
            Advance();
        }

        /// <summary>
        /// Walk-in places change rooms the way patients do. Registered, the player sits and waits while the
        /// patients ahead are called in by name; called themselves, they walk into the treatment room.
        /// Afterwards they come back out and walk to the reception to pay.
        /// </summary>
        async Task WalkTo(StepDef step)
        {
            var q = proc.queue;
            panel.Show(false);
            if (step.location == "counter")
            {
                var from = rooms?.FirstOrDefault(r => r && r.location == location);
                await Overlay.I.FadeTo(1, 0.35f);
                if (from && from.outside) player.Teleport(from.outside.position, from.outside.eulerAngles.y);
                await ExitConversation();
                await Overlay.I.FadeTo(0, 0.5f);
                hud.SetTask(TaskFor(stepIndex));
                await counter.WaitAsync(ct);
                await EnterConversation(step.location, false);
                return;
            }
            var room = rooms?.FirstOrDefault(r => r && r.location == step.location);
            if (!room)
            {
                await MoveTo(step);
                return;
            }
            await ExitConversation();
            hud.SetTask(Loc.T("obj_wait_name"));
            if (waitingBoard) waitingBoard.Join(session.PlayerNameKo);
            var door = room.approach ? room.approach : room.transform;
            // the patients ahead of the player go in first
            foreach (var name in q.names)
            {
                await Awaitable.WaitForSecondsAsync(q.interval + UnityEngine.Random.Range(-1f, 1.5f), ct);
                if (waitingBoard) waitingBoard.Call(name);
                crowd?.CallToRoom(door, Spot(OtherCounter(q.counter)));
                _ = Announce(0.55f, step.announce, name);
            }
            await Awaitable.WaitForSecondsAsync(q.interval * 0.8f, ct);
            if (waitingBoard) waitingBoard.Call(session.PlayerNameKo);
            hud.Banner(string.Format(step.announce, session.PlayerNameKo), TaskFor(stepIndex), 7f);
            hud.SetTask(TaskFor(stepIndex));
            await Announce(1f, step.announce, session.PlayerNameKo);
            await room.WaitAsync(ct);
            await EnterConversation(step.location, true);
        }

        /// <summary>The customer spot in front of a counter (1-based), for visitors.</summary>
        Transform Spot(int counterNumber) =>
            crowd && crowd.counterSpots != null && crowd.counterSpots.Length > 0
                ? crowd.counterSpots[Mathf.Clamp(counterNumber - 1, 0, crowd.counterSpots.Length - 1)]
                : null;

        int OtherCounter(int mine)
        {
            // the counters this room really has (a two-counter clinic never calls "3번 창구")
            int n = crowd && crowd.counterSpots != null && crowd.counterSpots.Length > 1 ? crowd.counterSpots.Length : 3;
            int c;
            do c = UnityEngine.Random.Range(1, n + 1); while (c == mine && n > 1);
            return c;
        }

        // ---------------------------------------------------------------- progress

        void Advance()
        {
            progress++;
            UpdateProgress();
        }

        void UpdateProgress() => hud.SetProgress(progress, TotalStages);

        /// <summary>The to-do for a step: the latest `task` in the procedure up to it, else "talk to the …".</summary>
        string TaskFor(int index)
        {
            for (int i = index; i >= 0; i--)
                if (proc.steps[i].task != null) return proc.steps[i].task.Get();
            var staff = proc.Staff(proc.steps[index].staff);
            return Loc.T("talk_to_role", (staff.roleText?.Get() ?? staff.role).ToLowerInvariant());
        }

        // ---------------------------------------------------------------- steps

        struct StepOutcome
        {
            public bool Blocked;
            public List<(string, string)> Reaction;
        }

        bool Offered(ChoiceDef c) =>
            (string.IsNullOrEmpty(c.needs) || session.Has(c.needs)) &&
            (string.IsNullOrEmpty(c.unless) || !session.Has(c.unless));

        async Task<StepOutcome> RunStep(StepDef step, List<(string text, string meaning)> carry)
        {
            var staff = proc.Staff(step.staff);
            actor = actors.FirstOrDefault(a => a.staffId == step.staff) ?? actor;
            panel.SetSpeaker(staff.name, staff.roleText?.Get() ?? staff.role);
            panel.ClearFeedback();
            await Say(new List<(string, string)>(carry) { (step.npc, step.meaning?.Get()) });

            if (step.type == "keypad") return await RunKeypad(step);

            var tried = new List<ChoiceDef>();
            var choices = Shuffle(step.choices.Where(Offered));
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var views = choices.Select(c => new ChoiceView
                {
                    Korean = c.ko,
                    Romanization = c.rom,
                    Meaning = c.meaning?.Get(),
                    DocKorean = string.IsNullOrEmpty(c.give) ? null : Documents.Korean(c.give),
                    DocLocal = string.IsNullOrEmpty(c.give) ? null : Documents.LocalName(c.give),
                    Tried = tried.Contains(c),
                }).ToList();
                ShownChoices = choices;
                var pick = choices[await panel.Choose(views, ct)];
                panel.ClearFeedback();
                if (!string.IsNullOrEmpty(pick.give)) Sfx.Play(ProceduralAudio.Paper, 0.7f);
                await SpeakAsPlayer(pick.ko);

                switch (pick.result)
                {
                    case "advance":
                        Sfx.Play(ProceduralAudio.Blip, 0.5f);
                        foreach (var g in pick.grants) session.Grant(g);
                        return new StepOutcome { Reaction = Lines((pick.reply, pick.replyMeaning?.Get())) };

                    case "block":
                        await Say(Lines((pick.reply, pick.replyMeaning?.Get())));
                        return new StepOutcome { Blocked = true };

                    case "ask":
                        // asking to repeat or explain is a good move, not a mistake
                        tried.Add(pick);
                        await Say(Lines((pick.reply, pick.replyMeaning?.Get())));
                        continue;

                    case "fetch":
                        await Say(Lines((pick.reply, pick.replyMeaning?.Get())));
                        await Fetch(step);
                        tried.Clear();
                        choices = Shuffle(step.choices.Where(Offered));
                        continue;

                    default: // retry
                        mistakes++;
                        tried.Add(pick);
                        var note = pick.note?.Get();
                        if (!string.IsNullOrEmpty(note) && !tips.Contains(note)) tips.Add(note);
                        Sfx.Play(ProceduralAudio.Soft, 0.5f);
                        panel.ShowFeedback(note ?? Loc.T("try_another"));
                        await Say(Lines((pick.reply, pick.replyMeaning?.Get())));
                        // keep the question on screen so the player can re-read it
                        panel.ShowNpcLine(step.npc, step.meaning?.Get());
                        lastLines = Lines((step.npc, step.meaning?.Get()));
                        if (choices.All(c => tried.Contains(c) || c.result == "retry")) tried.Clear();
                        continue;
                }
            }
        }

        async Task<StepOutcome> RunKeypad(StepDef step)
        {
            while (true)
            {
                var code = await panel.Keypad(ct);
                if (step.confirmPrevious && code != pin)
                {
                    mistakes++;
                    panel.ShowFeedback(Loc.T("pin_mismatch"));
                    await Say(Lines(("비밀번호가 일치하지 않아요. 한 번 더 눌러 주세요.", Loc.T("pin_mismatch"))));
                    continue;
                }
                pin = code;
                Sfx.Play(ProceduralAudio.Blip, 0.5f);
                return new StepOutcome { Reaction = Lines((step.ack ?? "네.", null)) };
            }
        }

        static List<(string, string)> Lines((string text, string meaning) line) =>
            string.IsNullOrWhiteSpace(line.text) ? new List<(string, string)>() : new List<(string, string)> { line };

        static List<ChoiceDef> Shuffle(IEnumerable<ChoiceDef> choices) =>
            choices.OrderBy(_ => UnityEngine.Random.value).ToList();

        async Task Fetch(StepDef step)
        {
            var f = step.fetch;
            await ExitConversation();
            var station = stations.First(s => s.stationId == f.station);
            hud.SetTask(f.objective?.Get());
            await station.WaitAsync(ct);
            hud.SetTask(Loc.T("return_counter", proc.queue.counter));
            await counter.WaitAsync(ct);
            hud.SetTask(TaskFor(stepIndex));
            await EnterConversation(location, false);
            await Say(Lines(("다 쓰셨어요? " + step.npc, step.meaning?.Get())));
        }

        // ---------------------------------------------------------------- speech

        async Task Say(List<(string text, string meaning)> lines)
        {
            lines = lines.Where(l => !string.IsNullOrWhiteSpace(l.text)).ToList();
            if (lines.Count == 0) return;
            StopReplay();
            lastLines = lines;
            speaking = true;
            try
            {
                var clips = lines.Select(l => voice.SpeakAsync(l.text, actor.Voice, false, ct)).ToList();
                for (int i = 0; i < lines.Count; i++)
                {
                    var clip = await clips[i];
                    panel.ShowNpcLine(lines[i].text, lines[i].meaning);
                    await actor.PlayAsync(clip, lines[i].text, ct);
                }
            }
            finally
            {
                speaking = false;
            }
        }

        /// <summary>The player's chosen answer, read aloud so they hear how it sounds.</summary>
        async Task SpeakAsPlayer(string korean)
        {
            if (string.IsNullOrWhiteSpace(korean) || korean.StartsWith("[")) return;
            var clip = await voice.SpeakAsync(korean, PlayerVoice, false, ct);
            if (!clip) return;
            playerMouth.clip = clip;
            playerMouth.Play();
            AudioTap.Note(clip, playerMouth.volume);
            await Awaitable.WaitForSecondsAsync(clip.length + 0.15f, ct);
        }

        void Replay()
        {
            if (speaking || lastLines.Count == 0 || !actor) return;
            StopReplay();
            replayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            _ = ReplaySlow(lastLines[^1], replayCts.Token);
        }

        async Task ReplaySlow((string text, string meaning) line, CancellationToken token)
        {
            try
            {
                var clip = await voice.SpeakAsync(line.text, actor.Voice, true, token);
                await actor.PlayAsync(clip, line.text, token);
            }
            catch (OperationCanceledException) { }
        }

        void StopReplay()
        {
            if (replayCts == null) return;
            replayCts.Cancel();
            replayCts.Dispose();
            replayCts = null;
            if (actor) actor.StopVoice();
        }

        /// <summary>PA announcement: stitched from baked pieces when available, otherwise synthesised whole.</summary>
        async Task Announce(float volume, string template, params object[] args)
        {
            var text = string.Format(template, args);
            Sfx.Play(ProceduralAudio.Chime, 0.6f * volume);
            await Awaitable.WaitForSecondsAsync(0.9f, ct);
            var clip = BakedVoice.Compose(AnnouncerVoice.Id, template, args) ?? await voice.SpeakAsync(text, AnnouncerVoice, false, ct);
            if (!clip) return;
            announcer.PlayOneShot(clip, volume);
            AudioTap.Note(clip, volume * announcer.volume);
            await Awaitable.WaitForSecondsAsync(clip.length, ct);
        }

        void Prefetch(StepDef step)
        {
            var a = actors.FirstOrDefault(x => x.staffId == step.staff);
            if (a) voice.Prefetch(step.npc, a.Voice, ct);
            foreach (var c in step.choices)
            {
                if (!string.IsNullOrEmpty(c.ko) && !c.ko.StartsWith("[")) voice.Prefetch(c.ko, PlayerVoice, ct);
                if (a && !string.IsNullOrEmpty(c.reply)) voice.Prefetch(c.reply, a.Voice, ct);
            }
        }

        // ---------------------------------------------------------------- modes

        async Task EnterConversation(string loc, bool fade)
        {
            location = loc;
            var seat = loc == "counter" ? Anchor($"Seat_counter_{counter.counterNumber}") ?? Anchor("Seat_counter") : Anchor("Seat_" + loc);
            // sitting down at a low counter or on the doctor's stool happens behind a short fade
            bool sit = seat && seat.GetComponent<PlayerSeat>();
            fade |= sit && !player.Seated;
            if (fade) await Overlay.I.FadeTo(1, 0.35f);
            if (seat) player.Teleport(seat.position, seat.eulerAngles.y);
            player.SetSeated(sit, fade);
            player.MovementEnabled = false;
            player.LookEnabled = false;
            interactor.Enabled = false;
            hud.SetPrompt(null);
            hud.SetCrosshair(false);
            hud.HideHelp();
            // a name call or "go to counter N" banner would sit over the staff member and the answers
            hud.DismissBanner();
            CursorState.Gameplay(false);
            var target = actors.FirstOrDefault(a => a.staffId == proc.steps.First(s => s.location == loc).staff);
            if (target)
            {
                actor = target;
                target.rig.lookTarget = player.Camera.transform;
                // aim a little below the face so it sits above the answer panel
                var face = target.face ? target.face.position : target.transform.position + Vector3.up * 1.5f;
                player.FocusOn(face - Vector3.up * 0.32f, fade ? 0.01f : 0.6f);
            }
            if (fade) await Overlay.I.FadeTo(0, 0.5f);
            panel.Show(true);
        }

        async Task ExitConversation()
        {
            panel.Show(false);
            player.SetSeated(false);
            player.MovementEnabled = true;
            player.LookEnabled = true;
            interactor.Enabled = true;
            CursorState.Gameplay(true);
            await Task.CompletedTask;
        }

        async Task MoveTo(StepDef step)
        {
            panel.Show(false);
            await Awaitable.WaitForSecondsAsync(1.2f, ct);
            if (!string.IsNullOrEmpty(step.announce))
                await Announce(0.9f, step.announce, session.PlayerNameKo);
            await EnterConversation(step.location, true);
        }

        Transform Anchor(string n) => anchors.FirstOrDefault(a => a && a.name == n);

        // ---------------------------------------------------------------- end

        async Task Finish(ScenarioOutcome outcome)
        {
            Finished = true;
            await ExitConversation();
            player.MovementEnabled = false;
            interactor.Enabled = false;

            var result = new ScenarioResult
            {
                ScenarioId = level.id,
                Title = $"{proc.titleKo} · {level.FullTitle}",
                Outcome = outcome,
                StepsCompleted = progress,
                StepsTotal = TotalStages,
                Mistakes = mistakes,
                Seconds = Time.time - startTime,
                Verified = proc.verified,
                Lesson = outcome == ScenarioOutcome.Blocked ? proc.blockedLesson?.Get() : proc.completeNote?.Get(),
                Tips = tips.Take(4).ToList(),
                PhraseCard = proc.phrases.Select(p => new PhraseLine { Korean = p.ko, Romanization = p.rom, Meaning = p.meaning?.Get() }).ToList(),
            };

            if (outcome == ScenarioOutcome.Completed)
            {
                session.Grant(level.DoneFlag);
                foreach (var g in proc.grants)
                {
                    result.Granted.Add(g);
                    session.Grant(g);
                }
                Sfx.Play(ProceduralAudio.Success);
                hud.Banner(Loc.T("outcome_completed"), level.title.Get(), 3f);
            }
            else
            {
                Sfx.Play(ProceduralAudio.Soft);
                hud.Banner(Loc.T("outcome_blocked"), proc.blockedLesson?.Get(), 4f);
            }

            session.LastResult = result;
            session.History.Add(result);
            TelemetryLogger.Log(result);
            await Awaitable.WaitForSecondsAsync(3.5f, ct);
            SceneFlow.Load(SceneFlow.Results);
        }
    }
}
