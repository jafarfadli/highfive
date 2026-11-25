"""
Pre-renders every line the game can speak with OmniVoice (k2-fsa, Korean) into
Assets/_Project/Resources/Voice/<key>.ogg, so the voices ship with the build and work on any
platform without a TTS engine or network.

    ~/.cache/seoul-sim/omnivoice-venv/bin/python -I Tools/voice/bake_voices.py [--force] [--only voice,...] [--redo-unclear]

--redo-unclear renders the lines listed in bake_report.txt again with twice as many takes.

Every character has its own voice: an anchor clip in Tools/voice/anchors/<voice>.wav, designed once
from a short description (gender, age, pitch) and kept, so re-bakes sound the same. Each line is
spoken in the anchor's voice and checked with Whisper; a take that Whisper doesn't hear as the
line is rendered again (up to TRIES times, the best take is kept, misses are listed in
Tools/voice/bake_report.txt).

Keys match SeoulFirstWeek.Conversation.BakedVoice.Key: the first 20 hex chars of
md5("<voice>|normal|<text>") or md5("<voice>|slow|<text>"). Re-run after editing dialogue;
lines already baked are skipped and files no longer used are removed.

Licence: the OmniVoice code is Apache-2.0, its model weights CC-BY-NC (non-commercial, because of
its training data). Fine for the competition demo; a commercial release must re-bake with a model
licensed for commercial use.
"""
import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PROCEDURES = ROOT / "Assets/_Project/Data/Procedures"
OUT = ROOT / "Assets/_Project/Resources/Voice"
ANCHORS = Path(__file__).resolve().parent / "anchors"
REPORT = Path(__file__).resolve().parent / "bake_report.txt"

# Each character's voice, described for OmniVoice's voice design (gender, age, pitch). Staff ids
# come from the procedure files; the descriptions follow the people built in the scenes.
VOICES = {
    "nurse": "female, young adult, moderate pitch",     # 김지은, clinic window 1
    "nurse2": "female, young adult, high pitch",        # 이수진, clinic window 2 (Part 2)
    "doctor": "male, middle-aged, low pitch",           # 박준호 원장
    "officer": "female, middle-aged, moderate pitch",   # 이서연 주무관, immigration window 3
    "clerk2": "female, young adult, moderate pitch",    # 한지민 주무관, immigration window 2 (Part 2)
    "clerk": "male, young adult, moderate pitch",       # 최민수 대리, bank window 2
    "teller3": "male, middle-aged, moderate pitch",     # 정우진 과장, bank foreign exchange (Part 2)
    "pa": "female, young adult, high pitch",            # the number-calling announcer
    "player": "male, young adult, moderate pitch",      # Minh, the player
}
ANCHOR_TEXT = "안녕하세요. 무엇을 도와드릴까요? 천천히 말씀해 주세요."
SLOW = 0.72                      # speed factor of the "R · Hear again" replay
RATE = 24000                     # OmniVoice's sample rate
TARGET_RMS = 0.2                 # loudness of the voiced parts after normalising
TRIES = 3                        # takes per line before keeping the best one
GOOD = 0.12                      # character error rate Whisper may show for a take to pass
PLAYER_NAME_KO = "민"            # GameSession.PlayerNameKo
DEFAULT_CALL = "딩동. {0}번 고객님, {1}번 창구로 오세요."  # QueueDef.callLine
QUEUE_NUMBERS = range(1, 151)
COUNTERS = range(1, 7)
PIN_MISMATCH = "비밀번호가 일치하지 않아요. 한 번 더 눌러 주세요."  # ScenarioDirector.RunKeypad
FORM_BACK = "다 쓰셨어요? "        # ScenarioDirector.Fetch prefix


def key(voice, slow, text):
    raw = f"{voice}|{'slow' if slow else 'normal'}|{text.strip()}".encode("utf-8")
    return hashlib.md5(raw).hexdigest()[:20]


def segments(template):
    """Same split as BakedVoice.Segments."""
    return re.split(r"(?<=[.,?])\s+", template.strip())


# ------------------------------------------------------------------ reading numbers aloud

DIGITS = "영일이삼사오육칠팔구"


def sino(n):
    """Sino-Korean reading of a non-negative integer: 5300 -> 오천삼백."""
    if n == 0:
        return "영"
    out = ""
    for unit_value, unit in ((10 ** 8, "억"), (10 ** 4, "만")):
        if n >= unit_value:
            head = n // unit_value
            out += ("" if head == 1 and unit == "만" else sino(head)) + unit
            n %= unit_value
    for unit_value, unit in ((1000, "천"), (100, "백"), (10, "십")):
        d = n // unit_value
        if d:
            out += ("" if d == 1 else DIGITS[d]) + unit
        n %= unit_value
    if n:
        out += DIGITS[n]
    return out


def speakable(text):
    """What the voice engine should read: numbers, phone numbers and visa codes spelled out in Hangul."""
    t = text.replace(" · ", ", ")  # Logbook word pairs such as "기침 · 콧물"
    # Hangul counts as a word character, so match on digit edges rather than word boundaries
    t = re.sub(r"(?<!\d)0\d{1,2}-\d{3,4}-\d{4}(?!\d)",
               lambda m: ", ".join("".join("공" if c == "0" else DIGITS[int(c)] for c in part) for part in m.group().split("-")), t)
    t = re.sub(r"(?<![A-Za-z])D-(\d)(?!\d)", lambda m: "디" + {"2": "투", "4": "포"}.get(m.group(1), sino(int(m.group(1)))), t)
    t = re.sub(r"(\d+)\.(\d+)", lambda m: sino(int(m.group(1))) + " 점 " + " ".join(DIGITS[int(c)] for c in m.group(2)), t)
    t = re.sub(r"\d{1,3}(?:,\d{3})+", lambda m: m.group().replace(",", ""), t)
    t = re.sub(r"\d+", lambda m: sino(int(m.group())), t)
    return t


# ------------------------------------------------------------------ what the game can say

def collect():
    jobs = {}

    def add(voice, text, slow_too=False):
        if not text or not text.strip() or text.strip().startswith("["):
            return
        text = text.strip()
        for slow in ((False, True) if slow_too else (False,)):
            jobs[key(voice, slow, text)] = (voice, slow, text)

    for path in sorted(PROCEDURES.glob("*.json")):
        proc = json.loads(path.read_text(encoding="utf-8"))
        for step in proc["steps"]:
            v = step["staff"]
            add(v, step.get("npc"), True)
            add(v, step.get("after"), True)
            if step.get("type") == "keypad":
                add(v, step.get("ack") or "네.", True)
                if step.get("confirmPrevious"):
                    add(v, PIN_MISMATCH, True)
            if step.get("fetch"):
                add(v, FORM_BACK + step["npc"], True)
            for choice in step.get("choices", []):
                add(v, choice.get("reply"), True)
                add("player", choice.get("ko"))
            if step.get("announce"):
                # the player's name call, and at a walk-in clinic the patients called in before them
                for name in [PLAYER_NAME_KO] + proc.get("queue", {}).get("names", []):
                    for seg in segments(step["announce"]):
                        add("pa", seg.format(name))
        # the Logbook plays the phrase card in the player's voice and key words in the announcer's
        for phrase in proc.get("phrases", []):
            add("player", phrase.get("ko"))
        for word in proc.get("glossary", []):
            add("pa", word.get("ko"))
        call = proc.get("queue", {}).get("callLine", DEFAULT_CALL)
        for seg in segments(call):
            if "{0}" in seg:
                for n in QUEUE_NUMBERS:
                    add("pa", seg.replace("{0}", str(n)))
            elif "{1}" in seg:
                for c in COUNTERS:
                    add("pa", seg.replace("{1}", str(c)))
            else:
                add("pa", seg)
    return jobs


# ------------------------------------------------------------------ rendering

class Engine:
    def __init__(self):
        import torch
        from omnivoice import OmniVoice
        from transformers import pipeline
        self.device = "mps" if torch.backends.mps.is_available() else "cuda:0" if torch.cuda.is_available() else "cpu"
        self.model = OmniVoice.from_pretrained("k2-fsa/OmniVoice", device_map=self.device, dtype=torch.float32)
        self.asr = pipeline("automatic-speech-recognition", model="openai/whisper-small", device=self.device)

    def say(self, text, **kw):
        import numpy as np
        a = self.model.generate(text=text, **kw)[0]
        a = a.cpu().numpy() if hasattr(a, "cpu") else a
        return np.asarray(a, dtype=np.float32).squeeze()

    def error(self, audio, text):
        """Character error rate of Whisper's transcript against the line (Hangul only, digits read out)."""
        import librosa
        y = librosa.resample(audio, orig_sr=RATE, target_sr=16000)
        heard = self.asr({"raw": y, "sampling_rate": 16000}, generate_kwargs={"language": "korean"})["text"]
        return cer(speakable(text), speakable(heard)), heard.strip()

    def anchor(self, voice):
        """The character's reference clip: designed once (the clearest of several takes), then kept."""
        import numpy as np
        import soundfile as sf
        path = ANCHORS / f"{voice}.wav"
        if path.exists():
            return path
        ANCHORS.mkdir(parents=True, exist_ok=True)
        best = None
        for _ in range(6):
            y = self.say(ANCHOR_TEXT, instruct=VOICES[voice])
            e, _ = self.error(y, ANCHOR_TEXT)
            # a take that never pauses is breath noise, however Whisper reads it
            score = e + (0.5 if float(np.mean(np.abs(y) > 0.02)) > 0.8 else 0)
            if best is None or score < best[0]:
                best = (score, y)
            if score < 0.05:
                break
        sf.write(str(path), best[1], RATE)
        print(f"  designed {voice} ({VOICES[voice]}), anchor error {best[0]:.2f}", flush=True)
        return path


def clean(s):
    return "".join(c for c in s if "\uac00" <= c <= "\ud7a3")


def cer(ref, hyp):
    r, h = clean(ref), clean(hyp)
    d = list(range(len(h) + 1))
    for i, rc in enumerate(r, 1):
        prev, d[0] = d[0], i
        for j, hc in enumerate(h, 1):
            prev, d[j] = d[j], min(d[j] + 1, d[j - 1] + 1, prev + (rc != hc))
    return d[len(h)] / max(1, len(r))


def render(engine, anchor, voice, slow, text, path, tries=TRIES):
    """Speaks a line in the character's voice, keeps the clearest of up to `tries` takes; returns its error."""
    import librosa
    import numpy as np
    import soundfile as sf

    best = None
    for _ in range(tries):
        audio = engine.say(speakable(text), ref_audio=str(anchor), ref_text=ANCHOR_TEXT, speed=SLOW if slow else 1.0)
        e, heard = engine.error(audio, text)
        if best is None or e < best[0]:
            best = (e, audio, heard)
        if e <= GOOD:
            break
    e, audio, heard = best
    audio, _ = librosa.effects.trim(audio, top_db=40)
    voiced = audio[np.abs(audio) >= 0.02]
    if voiced.size:
        rms = float(np.sqrt(np.mean(voiced ** 2)))
        peak = float(np.max(np.abs(audio)))
        audio = audio * min(TARGET_RMS / rms, 6.0, 0.97 / peak)
    pad = np.zeros(int(RATE * 0.06), dtype=np.float32)
    audio = np.concatenate([pad, audio, pad])
    sf.write(str(path), audio, RATE, format="OGG", subtype="VORBIS")
    return e, heard


def main():
    force = "--force" in sys.argv
    only = None
    if "--only" in sys.argv:
        only = set(sys.argv[sys.argv.index("--only") + 1].split(","))
    jobs = collect()
    OUT.mkdir(parents=True, exist_ok=True)
    # lines the last report found unclear, to render again with more takes
    redo = set()
    if "--redo-unclear" in sys.argv and REPORT.exists():
        for line in REPORT.read_text(encoding="utf-8").splitlines():
            m = re.match(r"\s*[\d.]+\s+\[(\w+)( slow)?\]\s(.+?)\s{2}->", line)
            if m:
                redo.add(key(m.group(1), bool(m.group(2)), m.group(3)))

    # drop takes for lines that no longer exist (and their Unity .meta files)
    removed = 0
    for f in OUT.glob("*.ogg"):
        if f.stem not in jobs:
            f.unlink()
            meta = f.with_suffix(".ogg.meta")
            if meta.exists():
                meta.unlink()
            removed += 1

    todo = [(k, v) for k, v in jobs.items()
            if (force or k in redo or not (OUT / f"{k}.ogg").exists()) and (only is None or v[0] in only)]
    # normal-speed lines first, so a stopped run leaves the lines players hear first
    todo.sort(key=lambda kv: (kv[1][1], kv[1][0]))
    print(f"{len(jobs)} lines, {len(todo)} to render, {removed} stale removed", flush=True)
    if not todo:
        return

    engine = Engine()
    anchors = {v: engine.anchor(v) for v in sorted({v for _, (v, _, _) in todo})}
    misses = []
    for i, (k, (voice, slow, text)) in enumerate(todo, 1):
        anchor = anchors.get(voice) or anchors.setdefault(voice, engine.anchor(voice))
        e, heard = render(engine, anchor, voice, slow, text, OUT / f"{k}.ogg", TRIES * 2 if k in redo else TRIES)
        if e > GOOD:
            misses.append(f"{e:.2f}  [{voice}{' slow' if slow else ''}] {text}  ->  {heard}")
        if i % 20 == 0 or i == len(todo):
            print(f"  {i}/{len(todo)}  [{voice}{' slow' if slow else ''}] {text}", flush=True)
    REPORT.write_text("\n".join(misses) + ("\n" if misses else ""), encoding="utf-8")
    print(f"done; {len(misses)} lines above error {GOOD} (see {REPORT.name})", flush=True)


if __name__ == "__main__":
    main()
