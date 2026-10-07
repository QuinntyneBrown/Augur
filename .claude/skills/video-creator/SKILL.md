---
name: video-creator
description: Create a narrated video (slide deck + MP3 narration + captioned 1080p MP4) from text files - a transcript script, a timed HTML slide deck and an outline - using free edge-tts for the audio and a headless browser plus ffmpeg for the video. Use when asked to make, script, record, regenerate or fix a video, tutorial, walkthrough, demo, explainer, training lesson, screencast or slide deck for this repository or product.
---

# Video creator

A video is **narration audio + a slide deck timed to it + captions**, all generated from text files checked into one folder. You author three text files; tooling turns them into media.

```
docs/videos/NN-kebab-topic/
  script.md      transcript the audio is synthesized from            (you write)
  slides.html    one <section> per slide, each cued to a script phrase (you write)
  README.md      outline: purpose, objectives, assets on screen, run sheet (you write)
  NN-kebab-topic.mp3   free edge-tts synthesis                        (generated)
  NN-kebab-topic.mp4   slides rendered to PNG + audio + captions      (generated)
```

Paths above are the default. If the repo already has a videos/lessons folder or tooling, follow it instead. Large media should go through Git LFS if `.gitattributes` configures it; `.cache/` folders are never committed.

Before writing, read any existing video in the repo (tone, length, structure) and the code/docs the video covers. Every file path, name, number and quote on screen or in the script must be verified against the repository, never recalled.

## Workflow

1. **Scope.** One topic and one audience per video. Pick the next free `NN` and a kebab-case folder. Define the single question or outcome the video delivers. Update an existing video instead of duplicating a topic.
2. **Research** the source material and note exact paths and short excerpts for slides.
3. **Write `script.md`** (format below). Default target ~5-10 minutes (~150 words per minute); longer only if asked.
4. **Write `README.md`** (outline below).
5. **Write `slides.html`** (structure below), cueing each slide to a verbatim phrase in the script.
6. **Validate offline** with the repo's audio tool in dry-run mode (script format, word count and estimated duration; edge-tts has no synthesis charge). If no tool exists yet, create the two small tools described under "Tooling" before continuing.
7. **Synthesize audio** with free `edge-tts` (Microsoft Edge online read-aloud). It needs Python, the `edge-tts` package and internet access; no API key, Azure subscription or paid Speech service. Install with `python -m pip install edge-tts` using the same interpreter as the audio tool (`PYTHON` tells the generator which one). Edge is the generator's default engine; the only other engine is offline Piper (`--engine piper`), and there is no Azure engine. Synthesis also writes a timing manifest (per-section and per-paragraph start/end, in `.cache/`) that the video is synced to. Keep acronyms and code identifiers in a pronunciation lexicon (`pronunciations.json`) as plain-text spoken replacements and check them with a pronunciation test.
8. **Build the video** (needs Chrome/Edge - `EDGE_PATH` overrides - and ffmpeg with libx264 - `FFMPEG_PATH` overrides):
   - `--check`: every `data-cue` is found; prints the schedule.
   - `--slides-only`: renders PNGs to `.cache/<folder>/`. View them before encoding; overflowing code, clipped tables and unreadable text are the common failures.
   - full build: encodes the 1920x1080 MP4 with burned-in captions.
9. **Verify:** `ffprobe -v error -show_entries format=duration:stream=codec_name,width,height <mp4>` gives 1920x1080, h264 + audio, duration about equal to the MP3. Spot-check frames at cue times with `ffmpeg -ss <t> -i <mp4> -frames:v 1 <scratch>.png`.
10. **Register** the video in the folder's index README, if one exists.
11. **Commit** only the text files and media; no secrets, no `.cache/`.

## `script.md` format

- First line `# NN · Title`.
- `## Section` headings group narration; synthesize each paragraph separately and keep sections under ~9 minutes of speech.
- Plain paragraphs and `-` list items are spoken by the narrator. `**Name:** ...` paragraphs for a second speaker (e.g. `**Interviewer:**`, `**Host:**`) use a second voice.
- `[pause 5s]` on its own line inserts silence.
- Inline `` `code` `` is allowed and spoken via the pronunciation lexicon.
- **Not allowed:** tables, fenced code blocks, links, HTML. It must make sense with eyes closed: describe code, don't read it. Spell numbers as they should be spoken when it matters.

Suggested section order (adapt to the topic): untitled intro paragraph (what and why) → the questions/outcomes → concept sections → how it applies in this repo (exact paths) → the recommended approach in steps → pitfalls → optional Q&A/practice → recap ("things to remember", then a one-sentence preview of what's next).

Voice: second person, conversational, concrete, opinionated with trade-offs stated aloud. No filler or marketing tone. Time-sensitive facts carry "as of <month year>".

## `README.md` (outline) structure

```markdown
# NN · Title

> **Runtime:** ~N min · **Audience:** ... · **Prerequisites:** ...

**Video:** [NN-topic.mp4](NN-topic.mp4) · [Slides](slides.html) · **Audio:** [NN-topic.mp3](NN-topic.mp3) · [Transcript](script.md)

## Why this video exists
## Learning objectives     (bulleted "By the end, the viewer can:")
## Key questions           (| Question | What a strong answer includes |)
## Code / assets on screen (| File | What to show |)
## Run sheet               (| Time | Segment | Content |, mm:ss-mm:ss summing to the runtime)
## Demo commands           (exact, copy-pasteable)
## Pitfalls
## References              (official docs; no invented URLs)
```

## `slides.html` structure

Use one shared stylesheet and runtime (`assets/slides.css`, `assets/slides.js`) next to the video folders; copy the head and footer of an existing deck. Never inline new global styles.

```html
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Video NN Slides</title>
<link rel="stylesheet" href="../assets/slides.css">
</head>
<body data-video-number="NN" data-video-title="Title" data-parts="Introduction|Concepts|...|Recap">

<section id="title" data-part="0">
  <div class="kicker">Product · Video NN</div>
  <h1>Title</h1>
  <p class="lead">One-line subtitle</p>
</section>

<section id="concepts" data-part="1" data-cue="Phrase copied verbatim from script.md">
  <h2>Heading</h2>
  <ul><li>...</li></ul>
</section>

<script src="../assets/slides.js"></script>
</body>
</html>
```

Rules:

- **Every slide except the first has a `data-cue`**: a phrase copied *verbatim* from `script.md` (a heading or the start of a sentence), unique in the script, with cues in **narration order**. A slide appears when narration reaches its cue.
- `data-part` indexes `data-parts` (header label and progress bar); parts line up with `##` sections.
- Roughly one slide every 20-40 seconds. Cue changes inside a section are estimated from word counts (within a second or two); section boundaries are exact.
- Slides support the narration, they don't transcribe it: a heading, 3-5 short bullets, a diagram, or a short code excerpt.
- Code: `<pre class="code" data-lang="cs|ts|json|yaml|sql|sh|md|xml" data-mark="2,5">` (escape `<`, `>`, `&`); add `tight` for longer excerpts. Keep excerpts to ~16 lines, copied from the real file.
- Progressive builds: `<template id="x">` with `class="item"` children, then `<section data-template="x" data-show="3">` (reveal) or `data-highlight="2-4">` (focus).
- Screen recordings of the real UI: `<section data-clip="clips/x.mp4">` with an `<aside class="clip-notes">` plays a 1408x792 recording (`<video class="clip phone">` for 390 wide) made by `tools/video-record/record-clips.mjs` from the folder's `clips/clips.mjs`; `data-clip-delay="s"` holds its first frame. Give each clip slide at least as much narration as the clip runs (the builder speeds clips up to fit and warns above x1.6). See `docs/videos/README.md`.
- Reuse existing classes in `slides.css` (e.g. `kicker`, `lead`, `quote`, `muted`, `small`, `cols`, `card`, `file`, `tag`, `steps`, `question`, `recap`). Add a class only if none fits, and check it doesn't change other decks.
- If no shared stylesheet/runtime exists, create them first: 1920x1080 slides, one visible at a time, `?slide=N` or hash navigation so the renderer can screenshot each slide, header label and progress bar from `data-parts`.

## Tooling

Keep two small scripts under `tools/` (language of the repo's choice; a .NET single-file app, Node or Python all work):

- **Audio generator** (`tools/video-audio/`): parses `script.md` strictly; `--dry-run` validates and estimates length without network. Otherwise it uses the `edge-tts` Python package or `python -m edge_tts` CLI with plain text per paragraph: a neural narrator voice (e.g. `en-US-AndrewMultilingualNeural`) and a second voice for other speakers (e.g. `en-US-AvaMultilingualNeural`). Check available voices with `python -m edge_tts --list-voices`. Edge does not support custom SSML: apply `pronunciations.json` as spoken text replacements before synthesis, synthesize speaker paragraphs separately, and insert `[pause 5s]` as ffmpeg-generated silence. Normalize clips to a common audio format, measure their actual durations with ffprobe, concatenate clips and silence into the MP3, and write the timing manifest from those durations.
- **Video builder** (`tools/video-build/`): opens `slides.html` in headless Chrome/Edge, resolves each `data-cue` to a time using the manifest (section boundaries exact, in-section by word count), screenshots each slide at 1920x1080, writes an SRT/ASS caption file from the script, and runs ffmpeg (libx264, aac) to produce the MP4 with burned-in captions. Supports `--check` and `--slides-only`.

Prefer an existing equivalent if the repo already has one. In this repository, run from the repo root (Edge is the default engine, so `--engine edge` is optional):

```powershell
# Use the same Python interpreter for installation and the generator.
$env:PYTHON = "python"
python -m pip install edge-tts
node tools/video-audio/generate-audio.mjs docs/videos/NN-kebab-topic --dry-run
node tools/video-audio/generate-audio.mjs --say "Saturdaze" --out .cache/pronunciation-test.wav
node tools/video-audio/generate-audio.mjs docs/videos/NN-kebab-topic
```

Replace `NN-kebab-topic` with the actual folder. `EDGE_VOICE` and `EDGE_VOICE_2` override the narrator and second-speaker voices. For package usage and supported options, see the [edge-tts documentation](https://github.com/rany2/edge-tts).

## Quality checklist

- [ ] Every path, identifier, number and quote checked against the repo.
- [ ] Audio dry-run clean; estimated length matches the target.
- [ ] Cue check clean; no slide shorter than ~4 s or longer than ~90 s.
- [ ] Rendered PNGs reviewed: nothing clipped or overflowing at 1920x1080.
- [ ] MP4 is 1080p with audio and captions; duration matches the MP3.
- [ ] Index README updated, if present.
- [ ] No secrets or `.cache/` files committed; media via LFS if configured.

## When the tooling can't run

Behind a TLS-intercepting proxy, `edge-tts` fails with a certificate error because it trusts only Python's `certifi` bundle, not `SSL_CERT_FILE`; append the proxy CA to the file `python -c "import certifi; print(certifi.where())"` prints and retry.

If `dotnet`/Node/Python, the `edge-tts` package, internet access, a browser or ffmpeg isn't available, still deliver the three text files, run whatever validation is possible, and tell the user precisely which commands remain and what they need. Never fabricate an MP3/MP4 or claim media was built or checked when it wasn't.
